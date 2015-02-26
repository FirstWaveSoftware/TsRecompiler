
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;


class Arguments {

	private List<String> _ignores;

	public Boolean Usage { get; private set; }
	public Boolean Debug { get; private set; }
	public Boolean Watch { get; private set; }
	public IEnumerable<String> Ignores { get; private set; }

	public Arguments(String[] args) {
		this._ignores = new List<String>();
		this.Ignores = this._ignores;
		var unknowns = new List<String>();
		for (Int32 i = 0; i < args.Length; ++i) {
			switch (args[i]) {
				case "--ignore":
					var dir = args[++i];
					if (!dir.EndsWith(Path.DirectorySeparatorChar.ToString()))
						dir += Path.DirectorySeparatorChar;
					this._ignores.Add(dir);
					break;
				case "--debug":
					this.Debug = true;
					break;
				case "--watch":
					this.Watch = true;
					break;
				case "-h":
				case "--help":
					this.Usage = true;
					break;
				default:
					unknowns.Add(args[i]);
					break;
			}
		}
		if (0 < unknowns.Count)
			throw new CodedException(1, new ArgumentException(String.Format("{0} unexpected arguments: {1}", unknowns.Count, String.Join(", ", unknowns))));
		if (this.Debug)
			foreach (var prop in this.GetType().GetProperties())
				Console.Error.WriteLine("{0}: {1}", prop.Name,
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
			Console.Error.WriteLine(resolver(attrib));
	}

	public static void PrintUsage() {
		var assembly = typeof(Arguments).Assembly;
		PrintAttribute<AssemblyTitleAttribute>(assembly, a => a.Title);
		PrintAttribute<AssemblyFileVersionAttribute>(assembly, a => String.Format("version {0}", a.Version));
		PrintAttribute<AssemblyDescriptionAttribute>(assembly, a => a.Description);
		PrintAttribute<AssemblyCopyrightAttribute>(assembly, a => a.Copyright);
		Console.Error.WriteLine(@"
Usage:
tsrc [--watch] [--ignore <dir>]+
tsrc (--help | -h)
");
	}

}
