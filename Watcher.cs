
using System;
using System.Collections.Concurrent;
using System.IO;


class Watcher : IDisposable {

	#region struct ChangeRecord
	struct ChangeRecord {

		public readonly String filename;
		public readonly WatcherChangeTypes change;

		public ChangeRecord(String filename, WatcherChangeTypes change) {
			this.filename = filename;
			this.change = change;
		}

	}
	#endregion

	private BlockingCollection<ChangeRecord> changes;
	private FileSystemWatcher fsr;

	public Watcher(String pattern = "*.ts") {
		this.changes = new BlockingCollection<ChangeRecord>(new ConcurrentQueue<ChangeRecord>());
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
		this.changes.Add(new ChangeRecord(args.Name, args.ChangeType));
	}

	public Boolean WaitFileChanges(Int32 timeout, Action<String, WatcherChangeTypes> callback) {
		ChangeRecord item;
		if (!this.changes.TryTake(out item, timeout))
			return false;
		callback(item.filename, item.change);
		while (this.changes.TryTake(out item, timeout))
			callback(item.filename, item.change);
		return true;
	}

}
