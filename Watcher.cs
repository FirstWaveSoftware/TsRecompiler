
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;


class Watcher : IDisposable {

	BlockingCollection<String> changes;
	FileSystemWatcher fsr;

	public Watcher(String pattern = "*.ts") {
		this.changes = new BlockingCollection<String>();
		this.fsr = new FileSystemWatcher() {
			Path = ".",
			Filter = pattern,
			IncludeSubdirectories = true
		};
		this.fsr.Changed += this.OnFileSystemEvent;
		this.fsr.Created += this.OnFileSystemEvent;
		this.fsr.Deleted += this.OnFileSystemEvent;
		this.fsr.Renamed += this.OnFileSystemEvent;
		this.fsr.EnableRaisingEvents = true;
	}

	public void Dispose() {
		if (null != this.fsr) {
			this.fsr.Dispose();
			this.fsr = null;
		}
		if (null != this.changes) {
			this.changes.Dispose();
			this.changes = null;
		}
	}

	private void OnFileSystemEvent(Object sender, FileSystemEventArgs args) {
		this.changes.Add(args.Name);
	}

	public void WaitFileChanges(TimeSpan timeout, Action<String> callback) {
		String name;
		if (this.changes.TryTake(out name, Timeout.Infinite))
			callback(name);
		while (this.changes.TryTake(out name, timeout))
			callback(name);
	}

}
