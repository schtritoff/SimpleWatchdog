using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceProcess;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

// Application may run as exe or as windows service.
// It is designed to run and watchdog other applications.
// The list of applications to run and watch should be
// located in the settings.xml file.

// Articles:
// http://stackoverflow.com/questions/7764088/net-console-application-as-windows-service -
// http://stackoverflow.com/questions/11146381/whats-the-best-way-to-watchdog-a-desktop-application - 

namespace SimpleWatchdog
{
    class Program
    {
        public const string ServiceName = "SimpleWatchdogService";
        public static bool IsRunningAsService = true;
        static Settings settingsInfo;
        public static string settingsFilePath = @"settings.xml";
        public const string logFilePath = @"Log.txt";

        #region Nested class to support running as service

        public class Service : ServiceBase
        {
            public Service()
            {
                ServiceName = Program.ServiceName;
            }

            protected override void OnStart(string[] args)
            {
                Program.Start(args);
            }

            protected override void OnStop()
            {
                Program.Stop();
            }
        }
        #endregion

        #region Imports needed for daemonizing the program
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        #endregion

        static void Main(string[] args)
        {
            // Paths of log and settings files - in the same folder where exe is located, for demo simplicity
            settingsFilePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), settingsFilePath);
            Log.LogFileName = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), logFilePath);

            Log.LogMessage("Starting the " + ServiceName + "...", Log.LogSeverity.COOLINFO);

            if (!Environment.UserInteractive) // Environment.UserInteractive is normally true for console app and false for a service.
            {
                // running as service
                IsRunningAsService = true;
                using (var service = new Service())
                    ServiceBase.Run(service);
            }
            else
            {
                IsRunningAsService = false;

                #region HELP
                if (args.Length > 0 && (args[0] == "/?"))
                {
                    Console.WriteLine(" *************************************************");
                    Console.WriteLine(" *                                               *");
                    Console.WriteLine(" *    Simple Watchdog Utility                    *");
                    Console.WriteLine(" *                                               *");
                    Console.WriteLine(" *************************************************");
                    Console.WriteLine();
                    Console.WriteLine("Designed to run muiltiple applications reliably (run + watchdog + restart if failed).");
                    Console.WriteLine();
                    Console.WriteLine("May be executed as standalone application or as a service.");
                    Console.WriteLine("When run as standalone console application you can use argument '--daemonize' to hide console window.");
                    Console.WriteLine();
                    Console.WriteLine("List of applications and their command line parameters should be located in XML file " + settingsFilePath);
                    Console.WriteLine();
                    Console.WriteLine("Log file path is " + logFilePath);
                    Console.WriteLine();
                    Console.WriteLine("To install as a service, use sc command, for example: sc create SimpleWatchdog binPath= \"<Path>SimpleWatchdog.exe\" start= auto");
                    Console.WriteLine("To uninstall the service, use sc command, for example: sc delete SimpleWatchdog");
                    Console.WriteLine();
                    Console.WriteLine("Configuration XML file structure:");
                    Console.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                    Console.WriteLine("<Settings xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
                    Console.WriteLine("  <LoggingLevel>0-error, 1-warnings, 2-info, 3-debug</LoggingLevel>");
                    Console.WriteLine("  <WatchedProcessesList>");
                    Console.WriteLine("    <WatchedProcessInfo>");
                    Console.WriteLine("      <ProcessPath>full path to your process</ProcessPath>");
                    Console.WriteLine("      <CommandLineParams>command line params</CommandLineParams>");
                    Console.WriteLine("      <ShouldBeWatched>true or false. If true, the process will be restarted if it terminates.</ShouldBeWatched>");
                    Console.WriteLine("      <ProcessAlias>Process alias for log messages, will be used instead of the process path if defined. It is convenient if you have multiple processes with the same path but different params, and you wish to know which of them exited or started.</ProcessAlias>");
                    Console.WriteLine("      <IsBlockAndWaitUntilExit>True if the process should be executed and then we should wait for the process to terminate before we continue. Used for example if we have 5 processes in the list, and we need the first process to finish before we execute other processes in the list. Do not use together with ShouldBeWatched!</IsBlockAndWaitUntilExit>");
                    Console.WriteLine("    </WatchedProcessInfo>");
                    Console.WriteLine("    <WatchedProcessInfo>");
                    Console.WriteLine("       ... - list as many processes as you need.");
                    Console.WriteLine("    </WatchedProcessInfo>");
                    Console.WriteLine("  </WatchedProcessesList>");
                    Console.WriteLine("</Settings>");

                    return;
                }
                #endregion HELP

                // daemonize, src: https://stackoverflow.com/a/38221518/1155121
                if (args.Length > 0 && (args[0] == "--daemonize"))
                {
                    var handle = GetConsoleWindow();
                    ShowWindow(handle, SW_HIDE);
                }

                // running as console app
                Start(args);

                Log.LogMessage("Press any key to stop...", Log.LogSeverity.INFO);
                Console.ReadKey(true);

                Stop();
            }
        }




        /// <summary>
        /// Start the watchdog - load settings, start processes
        /// </summary>
        /// <param name="args"></param>
        private static void Start(string[] args)
        {
            // onstart code here
            string statusMsg;
            bool isSettingsOk = Settings.LoadSettings(settingsFilePath, out settingsInfo, out statusMsg);
            switch (settingsInfo.LoggingLevel)
            {
                case 0: Log.LoggingLevel = Log.LogSeverity.ERROR; break;
                case 1: Log.LoggingLevel = Log.LogSeverity.WARNING; break;
                case 2: Log.LoggingLevel = Log.LogSeverity.INFO; break;
                case 3: Log.LoggingLevel = Log.LogSeverity.DEBUG; break;
                default: Log.LoggingLevel = Log.LogSeverity.INFO; break;
            }
            Log.LogMessage(statusMsg, isSettingsOk ? Log.LogSeverity.INFO : Log.LogSeverity.ERROR);
            if (isSettingsOk)
            {
                // run the requested processes
                foreach (WatchedProcessInfo wpi in settingsInfo.WatchedProcessesList)
                    StartAndWatchProcess(wpi);
            }
            Log.LogMessage(ServiceName + " is running.", Log.LogSeverity.COOLINFO);
        }

        private static void Stop()
        {
            // onstop code here
            Log.LogMessage("Stopping the " + ServiceName + "...", Log.LogSeverity.INFO);
            // Check which process exited because the evtn does not give an indication
            foreach (WatchedProcessInfo wpi in settingsInfo.WatchedProcessesList)
            {
                // Stop the process
                if (wpi.Process != null && !wpi.Process.HasExited)
                {
                    Log.LogMessage("Killing process " + wpi.GetProcessPathOrAlias(), Log.LogSeverity.DEBUG);
                    wpi.Process.Exited -= process_Exited;
                    //wpi.Process.Kill(); // Regular kill
                    KillProcessAndChildren(wpi.ProcessID); // Recursive kill of process and all its child processes
                    Log.LogMessage("Process " + wpi.GetProcessPathOrAlias() + " killed.", Log.LogSeverity.DEBUG);
                }
            }
            Log.LogMessage(ServiceName + " service stopped.", Log.LogSeverity.INFO);
        }

        /// <summary>
        /// Start and set up watching for the specified process
        /// </summary>
        /// <param name="wpi"></param>
        static void StartAndWatchProcess(WatchedProcessInfo wpi)
        {
            Log.LogMessage("Starting process " + wpi.GetProcessPathOrAlias(), Log.LogSeverity.DEBUG);
            if (!File.Exists(wpi.GetProcessPathWithoutQuotes()))
            {
                Log.LogMessage("File " + wpi.GetProcessPathWithoutQuotes() + " not found!", Log.LogSeverity.ERROR);
                return;
            }

            try
            {
                // Start process
                ProcessStartInfo processStartInfo = new ProcessStartInfo(wpi.GetProcessPathWithoutQuotes(), wpi.CommandLineParams);
                processStartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(wpi.GetProcessPathWithoutQuotes());
                Process process = Process.Start(processStartInfo);
                wpi.ProcessID = process.Id;
                wpi.Process = process;
                Log.LogMessage("Process " + wpi.GetProcessPathOrAlias() + " started, ID=" + wpi.ProcessID, Log.LogSeverity.INFO);

                if (wpi.IsBlockAndWaitUntilExit)
                    wpi.Process.WaitForExit();

                if (wpi.ShouldBeWatched)
                {
                    wpi.Process.EnableRaisingEvents = true; // without that the process.Exited will not work, http://stackoverflow.com/questions/4504170/why-processs-exited-method-not-being-called
                    wpi.Process.Exited += process_Exited;
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("Process " + wpi.GetProcessPathOrAlias() + " start failed: " + ex.ToString(), Log.LogSeverity.ERROR);
            }
        }

        public static object _exitedEventLockObj = new object(); // is used to run only one thread of process_Exited event at a time

        /// <summary>
        /// Event fired in case of any one of the executed processes exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void process_Exited(object sender, EventArgs e)
        {
            lock (_exitedEventLockObj)
            {
                // Check which process exited because the evtn does not give an indication
                foreach (WatchedProcessInfo wpi in settingsInfo.WatchedProcessesList)
                {
                    // Restart the exited process
                    if (wpi.Process != null && wpi.Process.HasExited && wpi.ShouldBeWatched)
                    {
                        Log.LogMessage("Process " + wpi.GetProcessPathOrAlias() + " has exited, ID=" + wpi.ProcessID + ", will try to restart.", Log.LogSeverity.WARNING);
                        wpi.Process.Exited -= process_Exited;
                        StartAndWatchProcess(wpi);
                    }
                }
            }
        }

        /// <summary>
        /// Kill the given process and all its children
        /// </summary>
        /// <param name="pid"></param>
        private static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
              ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }
    }

}