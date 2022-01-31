using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWatchdog
{
    /// <summary>
    /// Class responsible for messages output to console window or to log file
    /// </summary>
    public class Log
    {
        /// <summary>
        /// Log severity enum - info, warning, error etc.
        /// </summary>
        public enum LogSeverity
        {
            DEBUG,      // less important information
            INFO,       // information
            COOLINFO,    // information about operation success
            WARNING,    // warning
            ERROR      // error
        }

        static LogSeverity m_loggingLevel = LogSeverity.ERROR;
        /// <summary>
        /// Minimal logging level definition
        /// </summary>
        public static LogSeverity LoggingLevel
        {
            get { return m_loggingLevel; }
            set { m_loggingLevel = value; }
        }

        public static object _consoleLockObj = new object();
        public static object _fileLockObj = new object();
        public static string LogFileName;

        public static void LogMessage(string str, LogSeverity severity)
        {
            if (severity >= m_loggingLevel)
                WriteToLogFile(str, severity);

            if (severity >= m_loggingLevel || severity > LogSeverity.DEBUG) // always write to console all except debug. Debug write only if requested.
                WriteToOutput(str, severity);
        }

        public static void WriteToLogFile(string display, LogSeverity severity)
        {
            lock (_fileLockObj)
            {
                File.AppendAllText(LogFileName, String.Format("{0} {1}: {2}{3}", DateTime.Now, severity, display, Environment.NewLine));

                // Delete the file if it is larger than ~1MB
                FileInfo fi = new FileInfo(LogFileName);
                if (fi.Length > 1000000) // ~1 MB
                    File.Delete(LogFileName);
            }
        }

        /// <summary>
        /// This is the only method that should be writing to output, all others should use it
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="severity"></param>
        public static void WriteToOutput(string display, LogSeverity severity)
        {
            lock (_consoleLockObj)
            {
                // Change color according to severity
                switch (severity)
                {
                    case LogSeverity.INFO:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case LogSeverity.WARNING:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogSeverity.DEBUG:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                    case LogSeverity.ERROR:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogSeverity.COOLINFO:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }

                // Write the message
                Console.WriteLine(display);

                // Change color back to default white
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static void WriteToDebug(string display)
        {
            Debug.WriteLine(display);
        }
    }
}
