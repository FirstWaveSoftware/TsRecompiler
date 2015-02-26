
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;


static class ProcessInfo {

	[DllImport("kernel32.dll")]
	static extern IntPtr GetCurrentProcess();

	[DllImport("ntdll.dll", SetLastError = true)]
	static extern unsafe Int32 NtQueryInformationProcess(
		IntPtr processHandle,
		Int32 processInformationClass,
		void* processInformation,
		Int32 processInformationLength,
		out Int32 returnLength);

	static readonly Int32 process_info_size = 6 * IntPtr.Size;
	const Int32 process_info_idx_parent = 5;

	public static Int32 GetParentProcessId() {
		var process_info = new IntPtr[process_info_size];
		Int32 ret_length;
		unsafe {
			fixed (void* process_info_ptr = process_info)
				if (0 != NtQueryInformationProcess(GetCurrentProcess(), 0, process_info_ptr, process_info_size, out ret_length))
					throw new Win32Exception("NtQueryInformationProcess");
		}
		return process_info[process_info_idx_parent].ToInt32();
	}

}
