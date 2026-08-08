using System;
using System.IO;

namespace MonitoringLauncherWPF.Core
{
    public static class Logger
    {
        private static readonly string LogFilePath;
        private static readonly string LogDirectory;
        private static readonly object _lockObj = new object();

        static Logger()
        {
            LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            // Automatyczne czyszczenie logów starszych niż 7 dni przy starcie
            CleanUpOldLogs(7);

            string fileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            LogFilePath = Path.Combine(LogDirectory, fileName);
        }

        /// <summary>
        /// Usuwa pliki logów starsze niż podana liczba dni.
        /// </summary>
        public static void CleanUpOldLogs(int daysToKeep)
        {
            try
            {
                if (!Directory.Exists(LogDirectory)) return;

                DirectoryInfo directoryInfo = new DirectoryInfo(LogDirectory);
                FileInfo[] files = directoryInfo.GetFiles("log_*.txt");
                DateTime thresholdDate = DateTime.Now.AddDays(-daysToKeep);

                foreach (FileInfo file in files)
                {
                    // Sprawdzamy datę ostatniej modyfikacji pliku
                    if (file.LastWriteTime < thresholdDate)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch
                        {
                            // Ignorujemy błędy usuwania pojedynczego pliku (np. jeśli jest zablokowany przez inny proces)
                        }
                    }
                }
            }
            catch
            {
                // Ignorujemy błędy ogólne podczas czyszczenia, aby nie zablokować startu aplikacji
            }
        }

        private static string GetSenderName(object? sender)
        {
            if (sender == null) 
                return "Global";
            
            if (sender is string stringSender) 
                return stringSender;
            
            if (sender is Type typeSender) 
                return typeSender.FullName ?? typeSender.Name;
            
            return sender.GetType().FullName ?? sender.GetType().Name;
        }

        private static void WriteLog(string level, object? sender, string message, Exception? ex)
        {
            string senderName = GetSenderName(sender);

            lock (_lockObj)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(LogFilePath, true))
                    {
                        sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{senderName}] {message}");
                        
                        if (ex != null)
                        {
                            sw.WriteLine($"  [EXCEPTION] {ex.GetType().FullName}: {ex.Message}");
                            sw.WriteLine($"  [STACK TRACE]\n{ex.StackTrace}");

                            Exception? inner = ex.InnerException;
                            int depth = 1;
                            
                            while (inner != null)
                            {
                                sw.WriteLine($"  --- [INNER EXCEPTION LEVEL {depth}] ---");
                                sw.WriteLine($"  {inner.GetType().FullName}: {inner.Message}");
                                sw.WriteLine($"  {inner.StackTrace}");
                                
                                inner = inner.InnerException;
                                depth++;
                            }
                            sw.WriteLine(new string('-', 70));
                        }
                    }
                }
                catch
                {
                    // Fail-safe: ignorujemy błędy wejścia/wyjścia samego loggera
                }
            }
        }

        public static void Info(object? sender, string message, Exception? ex = null)
        {
            WriteLog("INFO", sender, message, ex);
        }

        public static void Warn(object? sender, string message, Exception? ex = null)
        {
            WriteLog("WARN", sender, message, ex);
        }

        public static void Err(object? sender, string message, Exception? ex = null)
        {
            WriteLog("ERROR", sender, message, ex);
        }
    }
}