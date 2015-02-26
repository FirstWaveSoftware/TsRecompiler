
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;


class Program {

	private static readonly TimeSpan watch_timeout = TimeSpan.FromMilliseconds(500);
	private const String response_file_name = ".tsrc";
	private const String tsc_arguments = "--module commonjs --sourceMap --target ES5 @" + response_file_name;
	
	public static readonly Logging Logger = new Logging("[tsrc]");

	public static Int32 Main(String[] args) {
		Arguments arguments = null;
		try {
			arguments = new Arguments(args);
			if (arguments.Usage)
				Arguments.PrintUsage();
			else if (arguments.Version)
				Arguments.PrintVersion(true);
			else
				new Program(arguments).Run();
			return 0;
		} catch (CodedException ex) {
			var inner_message = (null != ex.InnerException) ? ex.InnerException.Message : null;
			var message = String.IsNullOrEmpty(inner_message) ? ex.Message : String.Format("{0}: {1}", ex.Message, inner_message);
			Logger.Error("{0}{1}{2}", message, Environment.NewLine, ex.StackTrace);
			return ex.StatusCode;
		} catch (Exception ex) {
			Logger.Error(ex.Message);
			return 1;
		}
	}

	private Arguments args;
	private Process parent_process;

	private Program(Arguments args) {
		this.args = args;
		Logger.EnableDebug = this.args.Debug;
		if (this.args.MonitorParent) {
			Logger.Debug("Attempting to obtain parent process ID...");
			this.parent_process = Process.GetProcessById(ProcessInfo.GetParentProcessId());
			Logger.Log("Will exit with parent process {0} ({1})", this.parent_process.Id, this.parent_process.ProcessName);
		}
	}

	private Boolean Continue { get { return !this.args.MonitorParent || !parent_process.HasExited; } }

	private void Run() {
		Logger.Log("Performing initial compilation...");
		Compile(ListTypings(), ListSources());
		if (this.args.Watch) {
			Logger.Debug("Watch mode enabled");
			this.Watch();
		}
	}

	private void Watch() {
		Boolean changes_detected = true, typing_changed;
		var change_list = new List<String>();
		using (var watcher = new Watcher()) {
			while (this.Continue) {
				change_list.Clear();
				typing_changed = false;
				if (changes_detected)
					Logger.Log("Waiting for changes...");
				if (changes_detected = watcher.WaitFileChanges(watch_timeout, (file) => this.ProcessChangedFile(file, change_list, ref typing_changed)))
					if ((0 < change_list.Count) || typing_changed)
						Compile(ListTypings(), typing_changed ? ListSources() : change_list);
			}
		}
	}

	private void ProcessChangedFile(String file, List<String> change_list, ref Boolean is_typing) {
		if (!String.IsNullOrEmpty(file) && !IsIgnored(file)) {
			if (file.EndsWith(".d.ts")) {
				Logger.Debug("Typing file changed: {0}", file);
				is_typing = true;
			} else {
				Logger.Debug("Source file changed: {0}", file);
				change_list.Add(file);
			}
		}
	}

	private Boolean IsIgnored(String file) {
		foreach (String dir in this.args.Ignores)
			if (file.StartsWith(dir)) {
				Logger.Debug("Ignored file changed: {0}", file);
				return true;
			}
		return false;
	}

	private IEnumerable<String> ListSources() {
		foreach (String enumerated in Directory.EnumerateFiles(".", "*.ts", SearchOption.AllDirectories)) {
			String file = enumerated.Substring(2);
			if (!file.EndsWith(".d.ts") && !IsIgnored(file)) {
				Logger.Debug("ListSources(): {0}", file);
				yield return file;
			}
		}
	}

	private IEnumerable<String> ListTypings() {
		foreach (String enumerated in Directory.EnumerateFiles(".", "*.d.ts", SearchOption.AllDirectories)) {
			String file = enumerated.Substring(2);
			if (!IsIgnored(file)) {
				Logger.Debug("ListTypings(): {0}", file);
				yield return file;
			}
		}
	}

	private void Compile(IEnumerable<String> typings, IEnumerable<String> sources) {
		String cwd = Directory.GetCurrentDirectory();
		StreamWriter response_file = null;
		try {
			Int32 nr_files = 0;
			// compile repsonse file
			response_file = new StreamWriter(File.OpenWrite(response_file_name));
			foreach (String file in typings) {
				response_file.WriteLine(file);
				++nr_files;
			}
			foreach (String file in sources) {
				response_file.WriteLine(file);
				++nr_files;
			}
			response_file.Close();
			response_file = null;
			if (0 == nr_files) {
				Logger.Log("Nothing to compile");
				return;
			}
			// start tsc
			Logger.Debug("Starting tsc {0}", tsc_arguments);
			DateTime time_start = DateTime.Now;
			var compiler = Process.Start(new ProcessStartInfo() {
				FileName = "tsc",
				Arguments = tsc_arguments,
				UseShellExecute = false,
				RedirectStandardError = true
			});
			// process tsc output
			while (!compiler.StandardError.EndOfStream) {
				String line = compiler.StandardError.ReadLine();
				if (line.StartsWith(cwd))
					line = line.Substring(cwd.Length + 1);
				if (line.Contains("error"))
					Logger.Error(line);
				else
					Logger.Log(line);
			}
			DateTime time_stop = DateTime.Now;
			compiler.WaitForExit();
			TimeSpan duration = time_stop - time_start;
			if ((0 != compiler.ExitCode) && !this.args.Watch)
				throw new CodedException(compiler.ExitCode, message: "Compilation Failed");
			Logger.Log(
				"Compilation completed {0} (took {1} seconds)",
				(0 == compiler.ExitCode) ? "successfully" : "with errors",
				duration.ToString(@"s\.f"));
		} finally {
			if (null != response_file)
				response_file.Close();
			File.Delete(response_file_name);
		}
	}

}
