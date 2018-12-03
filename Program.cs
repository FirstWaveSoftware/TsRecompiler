
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;


class Program
{

	private const String response_file_name = ".tsrc";

	public static readonly Logging Logger = new Logging("[tsrc]");

	public static Int32 Main( String[] args )
	{
		try
		{
			Arguments arguments = new Arguments( args );
			if( arguments.Usage )
			{
				Arguments.PrintUsage();
			}
			else if( arguments.Version )
			{
				Arguments.PrintVersion();
			}
			else
			{
				new Program( arguments ).Run();
			}

			return 0;
		}
		catch( CodedException codex )
		{
			PrintException( codex );
			return codex.StatusCode;
		}
		catch( Exception ex )
		{
			PrintException( ex );
			return 1;
		}
	}

	private static void PrintException( Exception ex )
	{
		while( null != ex )
		{
			if( !String.IsNullOrEmpty( ex.Message ) )
			{
				Logger.Error( ex.Message );
				Logger.Debug( ex.StackTrace );
			}
			ex = ex.InnerException;
		}
	}

	private readonly Arguments args;
	private readonly Process parent_process;
	private readonly String tsc_arguments;
	private readonly HashSet<String> typing_files = new HashSet<String>();

	private Program( Arguments args )
	{
		this.args = args;
		Logger.EnableDebug = this.args.Debug;
		// Obtain handle to parent process immediately; minimise window of opportunity for reuse of PPID.
		if( this.args.MonitorParent )
		{
			Logger.Debug( "Attempting to obtain parent process ID..." );
			this.parent_process = Process.GetProcessById( ProcessInfo.GetParentProcessId() );
			Logger.Debug( "Will exit with parent process {0} ({1})", this.parent_process.Id, this.parent_process.ProcessName );
		}
		// Build tsc arguments
		var tsc_args = new StringBuilder();
		if( !String.IsNullOrEmpty( this.args.Module ) )
		{
			tsc_args.AppendFormat( "--module {0} ", this.args.Module );
		}

		if( this.args.SourceMap )
		{
			tsc_args.Append( "--sourceMap " );
		}

		if( !String.IsNullOrEmpty( this.args.Target ) )
		{
			tsc_args.AppendFormat( "--target {0} ", this.args.Target );
		}

		tsc_args.AppendFormat( "@{0}", response_file_name );
		this.tsc_arguments = tsc_args.ToString();
	}

	private Boolean Continue { get { return !this.args.MonitorParent || !this.parent_process.HasExited; } }

	private void Run()
	{
		if( this.args.Watch )
		{
			Logger.Debug( "Watch mode enabled" );
			this.Watch();
		}
		else
		{
			this.PopulateTypings();
			this.Compile( this.ListSources() );
		}
	}

	private void PopulateTypings()
	{
		foreach( String file in ListTypings() )
		{
			this.typing_files.Add( file );
		}
	}

	private void Watch()
	{
		Boolean changes_detected = true, typing_changed;
		var changeset = new HashSet<String>();
		using( var watcher = new Watcher() )
		{
			this.PopulateTypings();
			this.Compile( this.ListSources() );
			while( this.Continue )
			{
				changeset.Clear();
				typing_changed = false;
				if( changes_detected )
				{
					Logger.Log( "Waiting for changes..." );
				}

				if( changes_detected = watcher.WaitFileChanges(
					this.args.WatchTimeout,
					( file, change_type ) => this.ProcessChangedFile( file, change_type, changeset, ref typing_changed )
				) && ((0 < changeset.Count) || typing_changed) )
				{
					this.Compile( typing_changed ? this.ListSources() : changeset );
				}
			}
		}
	}

	private void ProcessChangedFile( String file, WatcherChangeTypes change_type, ISet<String> changeset, ref Boolean typing_changed )
	{
		if( String.IsNullOrEmpty( file ) || this.IsIgnored( file ) )
		{
			return;
		}

		Boolean logit;
		if( file.EndsWith( ".d.ts" ) )
		{
			// Don't incur recompilation if new typing file appeared (it's probably empty anyway)
			if( 0 != (change_type & WatcherChangeTypes.Created) )
			{
				logit = this.typing_files.Add( file );
			}
			// Any change or deletion of typing file incurs full recompilation
			else
			{
				if( 0 != (change_type & WatcherChangeTypes.Deleted) )
				{
					this.typing_files.Remove( file );
				}

				if( logit = changeset.Add( file ) )
				{
					typing_changed = true;
				}
			}
			if( logit )
			{
				Logger.Log( "Typing {0}: {1}", change_type, file );
			}
		}
		else
		{
			// Don't bother recompiling deleted source file
			if( 0 != (change_type & WatcherChangeTypes.Deleted) )
			{
				logit = changeset.Remove( file );
			}
			// Add source file to changeset for recompilation
			else
			{
				logit = changeset.Add( file );
			}

			if( logit )
			{
				Logger.Log( "Source {0}: {1}", change_type, file );
			}
		}
	}

	private Boolean IsIgnored( String file )
	{
		foreach( String dir in this.args.Ignores )
		{
			if( file.StartsWith( dir ) )
			{
				Logger.Debug( "Ignored file changed: {0}", file );
				return true;
			}
		}

		return false;
	}

	private IEnumerable<String> FilterSources( IEnumerable<String> files )
	{
		foreach( String file in files )
		{
			if( !file.EndsWith( ".d.ts" ) )
			{
				yield return file;
			}
		}
	}

	private IEnumerable<String> ListSources()
	{
		foreach( String enumerated in Directory.EnumerateFiles( ".", "*.ts", SearchOption.AllDirectories ) )
		{
			String file = enumerated.Substring( 2 );
			if( !file.EndsWith( ".d.ts" ) && !this.IsIgnored( file ) )
			{
				Logger.Debug( "ListSources(): {0}", file );
				yield return file;
			}
		}
	}

	private IEnumerable<String> ListTypings()
	{
		foreach( String enumerated in Directory.EnumerateFiles( ".", "*.d.ts", SearchOption.AllDirectories ) )
		{
			String file = enumerated.Substring( 2 );
			if( !this.IsIgnored( file ) )
			{
				Logger.Debug( "ListTypings(): {0}", file );
				yield return file;
			}
		}
	}

	private void LogProcessLine( string line )
	{
		String cwd = Directory.GetCurrentDirectory();

		// Remove current directory from start of line (relative is tidier)
		if( line.StartsWith( cwd ) )
		{
			line = line.Substring( cwd.Length + 1 );
		}

		// Print errors in red
		if( line.Contains( "error" ) )
		{
			Logger.Error( line );
		}
		else
		{
			Logger.Log( line );
		}
	}

	private bool LogProcessOutput( Process process )
	{
		bool result = false;

		while( process.StandardError.Peek() != -1 )
		{
			LogProcessLine( process.StandardError.ReadLine() );
			result = true;
		}
		while( process.StandardOutput.Peek() != -1 )
		{
			LogProcessLine( process.StandardOutput.ReadLine() );
			result = true;
		}

		return result;
	}


	private void Compile( IEnumerable<String> sources )
	{
		StreamWriter response_file = null;
		try
		{
			Int32 nr_sources = 0;
			// Compile response file
			response_file = new StreamWriter( File.OpenWrite( response_file_name ) );
			foreach( String file in this.typing_files )
			{
				response_file.WriteLine( file );
			}

			foreach( String file in FilterSources( sources ) )
			{
				response_file.WriteLine( file );
				++nr_sources;
			}
			response_file.Close();
			response_file = null;
			if( 0 == nr_sources )
			{
				Logger.Log( "Nothing to compile" );
				return;
			}
			// Start tsc
			Logger.Log( "Compiling TypeScript{0}...", this.args.Debug ? String.Format( " (tsc {0})", this.tsc_arguments ) : String.Empty );
			DateTime time_start = DateTime.Now;
			var pi = new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = "/c tsc " + this.tsc_arguments,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true
			};
			Logger.Debug( "\"{0}\" {1}" ,pi.FileName, pi.Arguments );
			using( var compiler = Process.Start( pi ) )
			{
				// Process tsc output
				while( !compiler.HasExited )
				{
					if( !LogProcessOutput( compiler ) )
					{
						Thread.Sleep( 20 );
					}
				}
				LogProcessOutput( compiler );

				// Compute and print duration
				DateTime time_stop = DateTime.Now;
				TimeSpan duration = time_stop - time_start;
				Logger.Log(
					"Compilation completed {0} (in {1} seconds)",
					(0 == compiler.ExitCode) ? "successfully" : "with errors",
					duration.ToString( @"s\.f" ) );
				Logger.Log();
				// Throw up if not watching (looping)
				if( (0 != compiler.ExitCode) && !this.args.Watch )
				{
					throw new CodedException( compiler.ExitCode );
				}
			}
		}
		finally
		{
			if( null != response_file )
			{
				response_file.Close();
			}

			File.Delete( response_file_name );
		}
	}

}
