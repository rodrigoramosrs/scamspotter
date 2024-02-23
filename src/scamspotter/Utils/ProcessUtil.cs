using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScamSpotter.Utils
{
    static internal class ProcessUtil
    {
        #region windows specific dll import
        // Define constants for Win32 API
        const int TH32CS_SNAPPROCESS = 0x00000002;
        const uint PROCESS_QUERY_INFORMATION = 0x0400;

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll")]
        static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll")]
        static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);
        #endregion

        #region linux specific dll import
        [DllImport("libc")]
        static extern int fork();

        [DllImport("libc")]
        static extern int getppid();

        // Import the kill function from libc
        [DllImport("libc", SetLastError = true)]
        static extern int kill(int pid, int sig);

        // Import the libc function to read from a directory
        [DllImport("libc")]
        private static extern IntPtr opendir(string name);

        // Import the libc function to read from a directory stream
        [DllImport("libc")]
        private static extern IntPtr readdir(IntPtr dirp);

        // Import the libc function to close a directory stream
        [DllImport("libc")]
        private static extern int closedir(IntPtr dirp);

        // Structure representing a directory entry
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct dirent
        {
            public IntPtr d_ino;
            public IntPtr d_off;
            public ushort d_reclen;
            public byte d_type;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string d_name;
        }

        #endregion

        internal static void KillChildProcess(int parentId)
        {
            Console.WriteLine($"List of child processes of process ID {parentId}:");

            // Call the platform-specific function to enumerate processes
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                KillChildProcessesWindows(parentId);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                KillChildProcessesLinux(parentId);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new PlatformNotSupportedException("Kill child processes on macOS is not supported in this example.");
            }
            else
            {
                Console.WriteLine("Platform not supported.");
            }
        }

        static void KillChildProcessesWindows(int parentId)
        {
            // Create a snapshot of all processes
            IntPtr snapshotHandle = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshotHandle != IntPtr.Zero)
            {
                PROCESSENTRY32 pe32 = new PROCESSENTRY32();
                pe32.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

                // Get the first process information
                if (Process32First(snapshotHandle, ref pe32))
                {
                    do
                    {
                        if (pe32.th32ParentProcessID != parentId) continue;

                        //Killing child process
                        try
                        {
                            Process.GetProcessById((int)pe32.th32ProcessID).Kill(true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                            
                        }
                        
                    } while (Process32Next(snapshotHandle, ref pe32));
                }

                // Close the snapshot handle
                CloseHandle(snapshotHandle);
            }
        }

        static void KillChildProcessesLinux(int parentId)
        {
            Console.WriteLine($"Listing child processes for parent process with ID: {parentId}");

            // Open the /proc directory
            IntPtr dirPtr = opendir("/proc");
            if (dirPtr == IntPtr.Zero)
            {
                Console.WriteLine("Failed to open /proc directory.");
                return;
            }

            try
            {
                // Read entries from the /proc directory
                dirent dirEntry;
                while (true)
                {
                    IntPtr result = readdir(dirPtr);
                    if (result == IntPtr.Zero)
                        break;

                    dirEntry = Marshal.PtrToStructure<dirent>(result);
                    if (dirEntry.d_type == 4 && int.TryParse(dirEntry.d_name, out int processId))
                    {
                        // Check if the parent process ID matches the specified parent ID
                        int ppid = GetUnixParentProcessId(processId);
                        if (ppid == parentId)
                        {
                            string processName = GeUnixtProcessName(processId);
                            Process.GetProcessById(ppid).Kill(true);
                            Console.WriteLine($"Child Process ID: {processId}, Name: {processName}");
                        }
                    }
                }
            }
            finally
            {
                // Close the directory
                closedir(dirPtr);
            }
        }

        // Method to get the parent process ID of a given process ID
        private static int GetUnixParentProcessId(int processId)
        {
            try
            {
                // Read the /proc/<pid>/stat file to get process information
                string statFile = $"/proc/{processId}/stat";
                string[] statContents = File.ReadAllText(statFile).Split(' ');
                return int.Parse(statContents[3]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting parent process ID for process {processId}: {ex.Message}");
                return -1;
            }
        }

        // Method to get the process name of a given process ID
        private static string GeUnixtProcessName(int processId)
        {
            try
            {
                // Read the /proc/<pid>/comm file to get the process name
                string commFile = $"/proc/{processId}/comm";
                return File.ReadAllText(commFile).Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting process name for process {processId}: {ex.Message}");
                return "Unknown";
            }
        }

    }
}