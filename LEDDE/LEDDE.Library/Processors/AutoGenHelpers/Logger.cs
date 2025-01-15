using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LEDDE.Library.Processors.AutoGenHelpers
{
    internal class Logger
    {
        private static string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "application.log");

        public static void Log(string message)
        {
            try
            {
                // Ensure the log directory exists
                string logDirectory = Path.GetDirectoryName(logFilePath);
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Append the message to the log file with timestamp
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";

                // Write the log to the file
                File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Handle any logging errors (e.g., disk issues)
                File.AppendAllText(logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ERROR logging message: {ex.Message}" + Environment.NewLine);
            }
        }
    }
}
