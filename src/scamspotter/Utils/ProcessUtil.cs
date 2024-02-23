using System.Diagnostics;

namespace ScamSpotter.Utils
{
    static internal class ProcessUtil
    {

        internal static void KillProcessAndChildren(string processName)
        {
            // Get all processes with the specified name
            var processes = Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                // Kill the process and its children
                //KillProcessTree(process);
                process.Kill();
            }
        }
    }
}
