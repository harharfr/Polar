using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FortniteLauncher
{
    internal class Program
    {
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint MEM_COMMIT = 0x1000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint MEM_RELEASE = 0x8000;
        private const uint INFINITE = 0xFFFFFFFF;
        private static string fortniteBasePath = string.Empty;
        private static string fortniteFullPath = string.Empty;
        private static string coolDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Curl.dll");
        private static string consoleDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Console.dll");
        private static string gameserverDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Gameserver.dll");
        private static bool keepRunning = true;

        [STAThread]
        private static void Main(string[] args)
        {
            string path = "config.json";
            if (File.Exists(path))
            {
                string[] strArray = File.ReadAllLines(path);
                fortniteBasePath = strArray.Length != 0 ? strArray[0] : string.Empty;
            }
            if (string.IsNullOrEmpty(fortniteBasePath))
            {
                Console.WriteLine("Please type the path to your OG Fortnite installation (e.g., C:\\FortniteVersion):");
                fortniteBasePath = Console.ReadLine();
                fortniteFullPath = Path.Combine(fortniteBasePath, "FortniteGame\\Binaries\\Win64");
            }
            else
            {
                fortniteFullPath = Path.Combine(fortniteBasePath, "FortniteGame\\Binaries\\Win64");
            }

            if (string.IsNullOrEmpty(fortniteFullPath) || !Directory.Exists(fortniteFullPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid input. Please provide a valid Fortnite base path.");
                Console.ResetColor();
                return;
            }
            else
            {
                File.WriteAllLines(path, new string[1] { fortniteBasePath });
                new Thread(new ThreadStart(SuspendProcessesPeriodically)).Start();
                LaunchAndInjectProcesses(fortniteFullPath);
            }

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Press 'Q' to close Fortnite...");
            while (true)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Q)
                {
                    keepRunning = false;
                    CloseFortnite();
                    break;
                }
            }
        }



        private static async void LaunchAndInjectProcesses(string path)
        {
            string[] process1Args = new string[]
            {
                "-AUTH_LOGIN=unused -AUTH_PASSWORD= -AUTH_TYPE=Epic -epicapp=Fortnite -epicenv=Prod -EpicPortal -skippatchcheck -nobe - forceeac -fromfl=eac -fltoken=5360b1637173878a5a9a4938 -nosplash -caldera=namlessisyapping"
            };


            string[] process2Args = new string[]
            {
                "-AUTH_LOGIN=server -AUTH_PASSWORD= -AUTH_TYPE=Epic -epicapp=Fortnite -epicenv=Prod -EpicPortal -skippatchcheck -nobe - forceeac -fromfl=eac -fltoken=5360b1637173878a5a9a4938 -nosplash -nosound -caldera=namlessisyapping -nullrhi"
            };

            Process process1 = StartProcess(path, "FortniteClient-Win64-Shipping", process1Args[0]);
            if (process1 != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Launched Fortnite");
                Console.ResetColor();

                await Task.Delay(500);

                InjectDLL(process1, coolDllPath);

                await Task.Delay(30000);

                InjectDLL(process1, consoleDllPath);
            }

            Process process2 = StartProcess(path, "FortniteClient-Win64-Shipping", string.Join(" ", process2Args));
            if (process2 != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Gameserver Launched");

                Console.ResetColor();
                InjectDLL(process2, coolDllPath);

                await Task.Delay(20000);
                InjectDLL(process2, gameserverDllPath);

            }
        }

        private static Process StartProcess(string path, string processName, string arguments)
        {
            string exePath = Path.Combine(path, processName + ".exe");

            if (!File.Exists(exePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(processName + " executable not found.");
                Console.ResetColor();
                return null;
            }
            try
            {
                Process process = Process.Start(exePath, arguments);
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.ResetColor();
                return process;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error launching " + processName + ": " + ex.Message);
                Console.ResetColor();
                return null;
            }
        }

        private static void InjectDLL(Process process, string dllPath)
        {
            if (!File.Exists(dllPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("DLL not found at " + dllPath);
                Console.ResetColor();
                return;
            }

            IntPtr processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);

            if (processHandle == IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to open process for DLL injection.");
                Console.ResetColor();
                return;
            }

            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            IntPtr allocMemAddress = VirtualAllocEx(processHandle, IntPtr.Zero, (IntPtr)(dllPath.Length + 1), MEM_COMMIT, PAGE_READWRITE);

            if (allocMemAddress == IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Memory allocation in the remote process failed.");
                Console.ResetColor();
                return;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(dllPath);

            if (WriteProcessMemory(processHandle, allocMemAddress, bytes, bytes.Length, out _))
            {
                CreateRemoteThread(processHandle, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("DLL injected successfully into process ID: " + process.Id);
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to write to process memory.");
                Console.ResetColor();
            }

            CloseHandle(processHandle);
        }

        private static void CloseFortnite()
        {
            string[] processNames = new string[]
            {
                "FortniteClient-Win64-Shipping",
                "FortniteClient-Win64-Shipping_EAC",
                "FortniteClient-Win64-Shipping_BE",
                "FortniteLauncher"
            };

            foreach (string processName in processNames)
            {
                Process[] processes = Process.GetProcessesByName(processName);
                foreach (Process process in processes)
                {
                    try
                    {
                        process.Kill();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{processName} with PID {process.Id} has been closed.");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error closing {processName} with PID {process.Id}: {ex.Message}");
                        Console.ResetColor();
                    }
                }
            }
        }

        private static void SuspendProcessesPeriodically()
        {
            string[] processNames = new string[3]
            {
                "FortniteClient-Win64-Shipping_EAC",
                "FortniteClient-Win64-Shipping_BE",
                "FortniteLauncher"
            };

            while (keepRunning)
            {
                foreach (string processName in processNames)
                {
                    if (!keepRunning) return;

                    Process[] processes = Process.GetProcessesByName(processName);
                    if (processes.Length == 0)
                    {
                        StartProcess(fortniteFullPath, processName, "");
                    }
                    else
                    {
                        foreach (Process process in processes)
                        {
                            try
                            {
                                SuspendProcess(process);
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Error suspending {processName} with PID {process.Id}: {ex.Message}");
                                Console.ResetColor();
                            }
                        }
                    }
                }
                Thread.Sleep(100);
            }
        }

        private static void SuspendProcess(Process process)
        {
            foreach (ProcessThread thread in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);

                if (pOpenThread != IntPtr.Zero)
                {
                    SuspendThread(pOpenThread);
                    CloseHandle(pOpenThread);
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [Flags]
        public enum ThreadAccess
        {
            TERMINATE = 0x0001,
            SUSPEND_RESUME = 0x0002,
            GET_CONTEXT = 0x0008,
            SET_CONTEXT = 0x0010,
            SET_INFORMATION = 0x0020,
            QUERY_INFORMATION = 0x0040,
            SET_THREAD_TOKEN = 0x0080,
            IMPERSONATE = 0x0100,
            DIRECT_IMPERSONATION = 0x0200
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
    }
}
