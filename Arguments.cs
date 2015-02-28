
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;


class Arguments {

	#region class ParameterNameAttribute
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
	class ParameterNameAttribute : Attribute {
		public String Name { get; set; }
		public ParameterNameAttribute(String name) {
			this.Name = name;
		}
	}
	#endregion

	#region class ParameterPermittedAttribute
	[AttributeUsage(AttributeTargets.Property)]
	class ParameterPermittedAttribute : Attribute {
		public Object[] Values { get; private set; }
		public ParameterPermittedAttribute(params Object[] values) {
			this.Values = values;
		}
	}
	#endregion

	#region class ParameterDescriptionAttribute
	[AttributeUsage(AttributeTargets.Property)]
	class ParameterDescriptionAttribute : Attribute {
		public String Description { get; private set; }
		public ParameterDescriptionAttribute(String description) {
			this.Description = description;
		}
	}
	#endregion

	#region Properties

	[ParameterName("--version"), ParameterName("-v")]
	[ParameterDescription("Print the version of this program.")]
	public Boolean Version { get; private set; }

	[ParameterName("--help"), ParameterName("-h")]
	[ParameterDescription("Print detailed usage information.")]
	public Boolean Usage { get; private set; }

	[ParameterName("--debug")]
	[ParameterDescription("Print additional information about program operation.")]
	public Boolean Debug { get; private set; }

	[ParameterName("--watch")]
	[ParameterDescription("Watch the filesystem for changes to relevant files. Will not exit if errors are encountered.")]
	public Boolean Watch { get; private set; }

	[ParameterName("--watch-timeout")]
	[ParameterDescription("Number of milliseconds to wait for additional file changes before starting recompilation. Has no effect if not watching.")]
	public Int32 WatchTimeout { get; private set; } = 500;

	[ParameterName("--monitor-parent")]
	[ParameterDescription("Monitor the process that spawned this one and terminate when it does. Useful when running as part of a continuous build task.")]
	public Boolean MonitorParent { get; private set; }

	[ParameterName("--ignore")]
	[ParameterDescription("Specify a directory or file to be ignored by this program. Specify multiple times if desired.")]
	public IEnumerable<String> Ignores { get; private set; }

	[ParameterName("--target"), ParameterPermitted("ES3", "ES5", "ES6")]
	[ParameterDescription("Specify ECMAScript target version.")]
	public String Target { get; private set; } = "ES5";

	[ParameterName("--source-map")]
	[ParameterDescription("Generate corresponding .js.map files.")]
	public Boolean SourceMap { get; private set; } = true;

	[ParameterName("--module"), ParameterPermitted("commonjs", "amd")]
	[ParameterDescription("Specify module code generation.")]
	public String Module { get; private set; }

	[ParameterName("--timestamp")]
	[ParameterDescription("Prefix all logged output with a timestamp.")]
	public Boolean Timestamp { get; private set; }

	#endregion

	/// <summary>Defaults constructor.</summary>
	private Arguments()
	{ }

	/// <summary>Parses the specified array of arguments - such as from the command line - and sets corresponding properties.</summary>
	public Arguments(String[] args) {
		// Enumerate instance properties, sanity check, and construct respective lookup objects
		var param_names = new Dictionary<String, PropertyInfo>();
		var param_lists = new Dictionary<String, IList>();
		var param_permitted = new Dictionary<String, Object[]>();
		Type enum_subtype;
		foreach (var prop in this.GetType().GetProperties()) {
			if (!IsSupportedType(prop.PropertyType))
				throw new NotSupportedException(String.Format("Type of parameter {0} is not supported", prop.Name));
			foreach (var name_attrib in prop.GetCustomAttributes<ParameterNameAttribute>())
				param_names[name_attrib.Name] = prop;
			if (GetEnumerableType(prop.PropertyType, out enum_subtype) && !param_lists.ContainsKey(prop.Name)) {
				var list = MakeList(enum_subtype);
				param_lists[prop.Name] = list;
				prop.SetValue(this, list);
			}
			var permitted = prop.GetCustomAttribute<ParameterPermittedAttribute>();
			if (null != permitted)
				param_permitted[prop.Name] = permitted.Values;
		}
		// Enumerate provided arguments array and set properties appropriately
		var unknowns = new List<String>();
		for (Int32 i = 0; i < args.Length; ++i) {
			String param_name = args[i];
			PropertyInfo param_prop;
			Object[] permitted;
			MethodInfo parser;
			if (!param_names.TryGetValue(param_name, out param_prop)) {
				unknowns.Add(param_name);
				continue;
			}
			param_permitted.TryGetValue(param_prop.Name, out permitted);
			// parameter that can be present multiple times, each instance adding to a list of values
			if (GetEnumerableType(param_prop.PropertyType, out enum_subtype)) {
				Object value = args[++i];
				if (GetParseMethod(enum_subtype, out parser))
					value = parser.Invoke(null, new Object[] { value });
				if ((null != permitted) && !permitted.Contains(value))
					throw new ArgumentException(String.Format(
						"Unsupported argument {0} for parameter {1}; must be one of {2}",
						value,
						param_name,
						String.Join(", ", permitted)
					));
				param_lists[param_prop.Name].Add(value);
			}
			// simple switch parameter w/ optional true/false value
			else if (typeof(Boolean) == param_prop.PropertyType) {
				param_prop.SetValue(this, ((i + 1) < args.Length) && !param_names.ContainsKey(args[i + 1]) ? Boolean.Parse(args[++i]) : true);
			}
			// any other type of single value parameter, supporting type parsing if available
			else {
				Object value = args[++i];
				if (GetParseMethod(param_prop.PropertyType, out parser))
					value = parser.Invoke(null, new Object[] { value });
				param_prop.SetValue(this, value);
			}
		}
		//
		if (0 < unknowns.Count)
			throw new ArgumentException(String.Format("{0} unexpected arguments: {1}", unknowns.Count, String.Join(", ", unknowns)));
		this.Validate();
		if (this.Debug) this.PrintPropertyValues();
	}

	/// <summary>Prints only the program version.</summary>
	public static void PrintVersion() {
		var assembly = typeof(Arguments).Assembly;
		var version = assembly.GetName().Version.ToString();
		Console.WriteLine(version);
	}

	/// <summary>Prints detailed program parameter information.</summary>
	public static void PrintUsage() {
		var defaults = new Arguments();
		var assembly = typeof(Arguments).Assembly;
		PrintAttribute<AssemblyTitleAttribute>(assembly, a => a.Title);
		PrintAttribute<AssemblyDescriptionAttribute>(assembly, a => a.Description);
		PrintVersion();
		PrintAttribute<AssemblyCopyrightAttribute>(assembly, a => a.Copyright);
		Type subtype;
		var usage = new StringBuilder();
		usage.AppendLine("Usage:");
		foreach (var prop in typeof(Arguments).GetProperties()) {
			String[] param_names = prop.GetCustomAttributes<ParameterNameAttribute>().Select((attrib) => attrib.Name).ToArray();
			if (0 == param_names.Length) continue;
			Object[] param_permitted = prop.GetCustomAttributes<ParameterPermittedAttribute>().Select((attrib) => attrib.Values).SingleOrDefault();
			String param_description = prop.GetCustomAttributes<ParameterDescriptionAttribute>().Select((attrib) => attrib.Description).SingleOrDefault();
			Object param_default = prop.GetValue(defaults);
			usage.Append("    ").Append(String.Join(" | ", param_names)).Append(" ");
			if (GetEnumerableType(prop.PropertyType, out subtype)) {
				usage.AppendFormat("<{0}> +", FriendlyTypeName(subtype));
			} else if (typeof(Boolean) == prop.PropertyType) {
				usage.Append("[True | False]");
			} else {
				usage.AppendFormat("<{0}>", FriendlyTypeName(prop.PropertyType));
			}
			usage.AppendLine();
			if (null != param_description)
				usage.Append("        ").AppendLine(param_description);
			if (null != param_permitted)
				usage.Append("        ").Append("One of: ").AppendLine(String.Join(", ", param_permitted));
			if (null != param_default)
				usage.Append("        ").Append("Default: ").AppendLine(param_default.ToString());
		}
		Console.Write(usage.ToString());
	}

	/// <summary>Perform more detailed validation of successfully parsed arguments.</summary>
	private void Validate() {
		if (this.MonitorParent && (PlatformID.Unix == Environment.OSVersion.Platform))
			throw new ArgumentException(String.Format("Monitoring the parent process is not appropriate for this platform ({0})", Environment.OSVersion.Platform));
	}

	private void PrintPropertyValues() {
		Console.Error.WriteLine("Operating parameters:");
		foreach (var prop in this.GetType().GetProperties()) {
			Console.Error.WriteLine("    {0}: {1}", prop.Name,
				IsSupportedEnumerableType(prop.PropertyType)
					? String.Join(", ", (IEnumerable<Object>)prop.GetValue(this))
					: prop.GetValue(this));
		}
	}

	private static void PrintAttribute<TAttrib>(Assembly assembly, Func<TAttrib, String> resolver) where TAttrib : Attribute {
		foreach (var attrib in assembly.GetCustomAttributes<TAttrib>())
			Console.WriteLine(resolver(attrib));
	}

	private static Boolean GetEnumerableType(Type type, out Type subtype) {
		var is_enumerable = IsEnumerableType(type);
		if (is_enumerable) {
			// sanity checks
			if (type.ContainsGenericParameters || (1 != type.GenericTypeArguments.Length))
				throw new NotSupportedException("Multi parameter type genericity not supported.");
			subtype = type.GenericTypeArguments[0];
			if (!IsSupportedType(subtype) || IsEnumerableType(subtype))
				throw new NotSupportedException("Type of multi parameter is not supported.");
		} else
			subtype = null;
		return is_enumerable;
	}

	private static Boolean IsEnumerableType(Type type) {
		return typeof(IEnumerable).IsAssignableFrom(type) && (typeof(String) != type);
	}

	private static Boolean IsSupportedType(Type type) {
		MethodInfo parser;
		return
			(typeof(String) == type) ||
			(type.IsValueType && GetParseMethod(type, out parser)) ||
			IsSupportedEnumerableType(type);
	}

	private static Boolean IsSupportedEnumerableType(Type type) {
		return typeof(IEnumerable<Object>).IsAssignableFrom(type) && (typeof(String) != type);
	}

	private static IList MakeList(Type subtype) {
		Type constructed_type = typeof(List<Object>).GetGenericTypeDefinition().MakeGenericType(new Type[] { subtype });
		var instance = Activator.CreateInstance(constructed_type);
		return (IList)instance;
	}

	private static Boolean GetParseMethod(Type type, out MethodInfo parse) {
		parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(String) }, null);
		return null != parse;
	}

	private static String FriendlyTypeName(Type type) {
		String[] names = type.FullName.Split('.');
		if ((2 == names.Length) && ("System" == names[0]))
			return names[1];
		return type.FullName;
	}

}
