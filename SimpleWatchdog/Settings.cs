using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWatchdog
{
    /// <summary>
    /// Data structure for storing watchdog process settings
    /// </summary>
    [Serializable]
    public class Settings
    {
        /// <summary>
        /// Logging level = 0-error, 1-warnings, 2-info, 3-debug
        /// </summary>
        int loggingLevel = 1;
        public int LoggingLevel
        {
            get { return loggingLevel; }
            set { loggingLevel = value; }
        }

        List<WatchedProcessInfo> watchedProcessesList = new List<WatchedProcessInfo>();
        /// <summary>
        /// List of watched processes to start and to watch
        /// </summary>
        public List<WatchedProcessInfo> WatchedProcessesList
        {
            get { return watchedProcessesList; }
            set { watchedProcessesList = value; }
        }

        // src: https://sashadu.wordpress.com/2016/11/11/run-processes-reliably-process-watchdog-service/#comment-79
        public static bool LoadSettings(string filename, out Settings output, out string statusMsg)
        {
            bool isOk = true;
            statusMsg = String.Empty;
            output = null;
            try
            {
                System.Xml.Serialization.XmlSerializer deserializer = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                System.IO.TextReader textReader = new System.IO.StreamReader(filename);
                output = (Settings)(deserializer.Deserialize(textReader));
                textReader.Close();
            }
            catch (Exception ex)
            {
                statusMsg = ex.Message;
                isOk = false;
                throw;
            }
            return isOk;
        }

    }

    /// <summary>
    /// Data structure which holds information
    /// about the process which should be executed,
    /// watched and restarted if terminated.
    /// </summary>
    [Serializable]
    public class WatchedProcessInfo
    {
        /// <summary>
        /// Path to the executable
        /// </summary>
        public string ProcessPath { get; set; }

        /// <summary>
        /// Command line parameters
        /// </summary>
        public string CommandLineParams { get; set; }

        /// <summary>
        /// Process alias for log messages, will be used instead of the process path if defined.
        /// It is convenient if you have multiple processes with the same path but different params,
        /// and you wish to know which of them exited or started.
        /// </summary>

        public string ProcessAlias { get; set; }

        /// <summary>
        /// Returns process path or alias for usage in logs
        /// </summary>
        public string GetProcessPathOrAlias()
        {
            if (String.IsNullOrWhiteSpace(ProcessAlias))
                return ProcessPath;
            else
                return ProcessAlias;
        }

        /// <summary>
        /// Returns process path or alias for usage in logs
        /// </summary>
        public string GetProcessPathWithoutQuotes()
        {
            if (!String.IsNullOrWhiteSpace(ProcessPath))
            {
                return ProcessPath.Replace("\"", "");
            }
            else
            {
                return ProcessPath;
            }
        }

        bool shouldBeWatched = true;
        /// <summary>
        /// True if the process should be executed and also monitored, and restarted in case of unexpected termination.
        /// </summary>
        public bool ShouldBeWatched
        {
            get { return shouldBeWatched; }
            set { shouldBeWatched = value; }
        }

        bool isBlockAndWaitUntilExit = false;
        /// <summary>
        /// True if the process should be executed and then we should wait for the process to terminate before we continue.
        /// Used for example if we have 5 processes in the list, and we need the first process to finish before we execute other processes in the list.
        /// Do not use together with ShouldBeWatched!
        /// </summary>

        public bool IsBlockAndWaitUntilExit
        {
            get { return isBlockAndWaitUntilExit; }
            set { isBlockAndWaitUntilExit = value; }
        }

        [NonSerialized]
        int processID;
        /// <summary>
        /// Process ID - set during runtime
        /// </summary>
        [System.Xml.Serialization.XmlIgnore]
        public int ProcessID
        {
            get { return processID; }
            set { processID = value; }
        }

        [NonSerialized]
        System.Diagnostics.Process process;
        /// <summary>
        /// Process  - set during runtime
        /// </summary>
        [System.Xml.Serialization.XmlIgnore]
        public System.Diagnostics.Process Process
        {
            get { return process; }
            set { process = value; }
        }
    }
}
