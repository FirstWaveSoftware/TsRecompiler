
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;


class Arguments {

	private List<String> _ignores;

	public Boolean Version { get; private set; }
	public Boolean Usage { get; private set; }
	public Boolean Debug { get; private set; }
	public Boolean Watch { get; private set; }
	public Boolean MonitorParent { get; private set; }
	public IEnumerable<String> Ignores { get; private set; }

	public Arguments(String[] args) {
		this._ignores = new List<String>();
		this.Ignores = this._ignores;
		var unknowns = new List<String>();
		for (Int32 i = 0; i < args.Length; ++i) {
			switch (args[i]) {
				case "--version":
					this.Version = true;
					break;
				case "-h":
				case "--help":
					this.Usage = true;
					break;
				case "--debug":
					this.Debug = true;
					break;
				case "--ignore":
					var dir = args[++i];
					if (!dir.EndsWith(Path.DirectorySeparatorChar.ToString()))
						dir += Path.DirectorySeparatorChar;
					this._ignores.Add(dir);
					break;
				case "--watch":
					this.Watch = true;
					break;
				case "--monitor-parent":
					if (PlatformID.Unix == Environment.OSVersion.Platform)
						throw new ArgumentException(String.Format("Monitoring the parent process is not appropriate for this platform ({0})", Environment.OSVersion.Platform));
					this.MonitorParent = true;
					break;
				default:
					unknowns.Add(args[i]);
					break;
			}
		}
		if (0 < unknowns.Count)
			throw new ArgumentException(String.Format("{0} unexpected arguments: {1}", unknowns.Count, String.Join(", ", unknowns)));
		if (this.Debug)
			foreach (var prop in this.GetType().GetProperties())
				Program.Logger.Debug("{0}: {1}", prop.Name,
					typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) ?
						String.Join(", ", Stringify((IEnumerable)prop.GetValue(this))) :
						prop.GetValue(this));
	}

	private static IEnumerable<String> Stringify(IEnumerable enumerable) {
		foreach (Object obj in enumerable)
			if (null != obj)
				yield return obj.ToString();
	}

	private static void PrintAttribute<TAttrib>(Assembly assembly, Func<TAttrib, String> resolver) where TAttrib : Attribute {
		foreach (var attrib in assembly.GetCustomAttributes<TAttrib>())
			Console.WriteLine(resolver(attrib));
	}

	public static void PrintVersion(Boolean title = false) {
		var assembly = typeof(Arguments).Assembly;
		var version = assembly.GetName().Version.ToString();
		if (title)
			PrintAttribute<AssemblyTitleAttribute>(assembly, a => a.Title);
		Console.WriteLine("Version: {0}", version);
	}

	public static void PrintUsage() {
		var assembly = typeof(Arguments).Assembly;
		PrintAttribute<AssemblyTitleAttribute>(assembly, a => a.Title);
		PrintVersion();
		PrintAttribute<AssemblyDescriptionAttribute>(assembly, a => a.Description);
		PrintAttribute<AssemblyCopyrightAttribute>(assembly, a => a.Copyright);
		Console.WriteLine(@"
Usage:
tsrc [--watch] [--ignore <dir>]+
tsrc (--help | -h)
");
	}

}
