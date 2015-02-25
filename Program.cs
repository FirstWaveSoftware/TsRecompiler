﻿
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.ComponentModel;



class Program {

	private const String response_file_name = ".tsrc";
	private const String tsc_arguments = "--module commonjs --sourceMap --target ES5 @" + response_file_name;

	public static Int32 Main(String[] args) {
		Arguments arguments = null;
		try {
			arguments = new Arguments(args);
			if (arguments.Usage)
				Arguments.PrintUsage();
			else
				new Program(arguments).Run();
			return 0;
		} catch (CodedException ex) {
			Console.ForegroundColor = ConsoleColor.Red;
			if ((null != arguments) && arguments.Debug)
				Console.Error.WriteLine(ex.StackTrace);
			else {
				var inner_message = (null != ex.InnerException) ? ex.InnerException.Message : null;
				var message = String.IsNullOrEmpty(inner_message) ? ex.Message : String.Format("{0}: {1}", ex.Message, inner_message);
				Console.Error.WriteLine(message);
			}
			return ex.StatusCode;
		} catch (Exception ex) {
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine(ex.Message);
			return 1;
		} finally {
			Console.ResetColor();
		}
	}

	private Arguments args;

	private Program(Arguments args) {
		this.args = args;
	}

	private void Run() {
		Console.Error.WriteLine("Performing initial compilation...");
		Compile(ListTypings(), ListSources());
		if (this.args.Watch) {
			var fsw = new FileSystemWatcher() {
				Path = ".",
				Filter = "*.ts",
				IncludeSubdirectories = true,
				EnableRaisingEvents = true
			};
			var change_list = new List<String>();
			Boolean typing_changed;
			while (true) {
				change_list.Clear();
				typing_changed = false;
				Console.Error.WriteLine("Waiting for changes...");
				Wait(fsw, change_list, Timeout.Infinite, ref typing_changed);
				while (Wait(fsw, change_list, 500, ref typing_changed));
				if ((0 < change_list.Count) || typing_changed)
					Compile(ListTypings(), typing_changed ? ListSources() : change_list);
			}
		}
	}

	private Boolean Wait(FileSystemWatcher fsw, List<String> change_list, Int32 timeout, ref Boolean is_typing) {
		WaitForChangedResult change = fsw.WaitForChanged(WatcherChangeTypes.Created | WatcherChangeTypes.Changed, timeout);
		String file = change.Name;
		if (args.Debug)
			Console.Error.WriteLine("Change: {0}, Name: {1}, TimedOut: {2}", change.ChangeType, change.Name, change.TimedOut);
		if (!change.TimedOut && !String.IsNullOrEmpty(file) && !IsIgnored(file)) {
			if (file.EndsWith(".d.ts")) {
				if (args.Debug)
					Console.Error.WriteLine("Typing file changed: {0}", file);
				is_typing = true;
			} else {
				if (args.Debug)
					Console.Error.WriteLine("Source file changed: {0}", file);
				change_list.Add(file);
			}
		}
		return !change.TimedOut;
	}

	private Boolean IsIgnored(String file) {
		foreach (String dir in args.Ignores)
			if (file.StartsWith(dir)) {
				if (args.Debug)
					Console.Error.WriteLine("Ignored: {0}", file);
				return true;
			}
		return false;
	}

	private IEnumerable<String> ListSources() {
		foreach (String enumerated in Directory.EnumerateFiles(".", "*.ts", SearchOption.AllDirectories)) {
			String file = enumerated.Substring(2);
			if (!file.EndsWith(".d.ts") && !IsIgnored(file)) {
				if (args.Debug)
					Console.Error.WriteLine("ListSources(): {0}", file);
				yield return file;
			}
		}
	}

	private IEnumerable<String> ListTypings() {
		foreach (String enumerated in Directory.EnumerateFiles(".", "*.d.ts", SearchOption.AllDirectories)) {
			String file = enumerated.Substring(2);
			if (!IsIgnored(file)) {
				if (args.Debug)
					Console.Error.WriteLine("ListTypings(): {0}", file);
				yield return file;
			}
		}
	}

	private void Compile(IEnumerable<String> typings, IEnumerable<String> sources) {
		String cwd = Directory.GetCurrentDirectory();
		StreamWriter response_file = null;
		try {
			// compile repsonse file
			response_file = new StreamWriter(File.OpenWrite(response_file_name));
			foreach (String file in typings)
				response_file.WriteLine(file);
			foreach (String file in sources)
				response_file.WriteLine(file);
			response_file.Close();
			response_file = null;
			// start tsc
			if (args.Debug)
				Console.Error.WriteLine("Starting tsc {0}", tsc_arguments);
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
					Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine(line);
				Console.ResetColor();
			}
			compiler.WaitForExit();
			Console.Error.WriteLine("Compilation complete at {0}", DateTime.Now.ToString());
			if (0 != compiler.ExitCode)
				throw new CodedException(compiler.ExitCode);
		} finally {
			if (null != response_file)
				response_file.Close();
			File.Delete(response_file_name);
		}
	}

}
