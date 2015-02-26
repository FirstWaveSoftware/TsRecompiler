
using System;
using System.IO;


class Logging {

	private static Boolean AnsiColour { get; set; }

	static Logging() {
		AnsiColour = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"));
	}

	public Logging(String prefix = null) {
		this.Prefix = prefix;
	}

	public Boolean EnableDebug { get; set; }
	public Boolean PrintTimestamp { get; set; }
	public String Prefix { get; private set; }

	public void Log(String message = null, params Object[] args) {
		WriteLine(Console.Out, message, args);
	}

	public void Error(String message = null, params Object[] args) {
		WriteLine(Console.Error, message, args, true);
	}

	public void Debug(String message = null, params Object[] args) {
		if (this.EnableDebug)
			WriteLine(Console.Error, message, args);
	}

	private void WriteLine(TextWriter stream, String message, Object[] args, Boolean error = false) {
		if (String.IsNullOrEmpty(message)) {
			stream.WriteLine();
			return;
		}
		if (error) Console.ForegroundColor = ConsoleColor.Red;
		if (!String.IsNullOrEmpty(this.Prefix)) {
			if (!error) Console.ForegroundColor = ConsoleColor.Cyan;
			stream.Write(this.Prefix);
			stream.Write(' ');
			if (!error) Console.ResetColor();
		}
		if (this.PrintTimestamp) {
			stream.Write(DateTime.Now.ToString("HH:mm:ss.fff"));
			stream.Write(' ');
		}
		if ((null != args) && (0 < args.Length))
			message = String.Format(message, args);
		stream.WriteLine(message);
		if (error) Console.ResetColor();
	}

}
