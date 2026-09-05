using System.Diagnostics;
using System.Text;

namespace AOEKeyboardMacroPro.Services;

public static class CrashLogger
{
    private static readonly object _logLock = new();

    public static void Initialize()
    {
        // 1. Catch UI thread exceptions
        Application.ThreadException += (sender, e) =>
        {
            LogException("UI Thread Exception", e.Exception);
        };

        // 2. Catch non-UI thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException("Unhandled AppDomain Exception", ex);
            }
            else
            {
                LogException("Unhandled AppDomain Exception", new Exception(e.ExceptionObject?.ToString() ?? "Unknown error"));
            }
        };

        // 3. Catch unobserved Task exceptions
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogException("Unobserved Task Exception", e.Exception);
            e.SetObserved(); // Prevent app termination if possible
        };

        // Set unhandled exception mode to catch all
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
    }

    public static void LogException(string title, Exception ex)
    {
        lock (_logLock)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                StringBuilder sb = new();
                sb.AppendLine($"==================================================");
                sb.AppendLine($"[CRASH LOG] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"Type: {title}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Source: {ex.Source}");
                sb.AppendLine($"TargetSite: {ex.TargetSite}");
                sb.AppendLine($"StackTrace:\n{ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"InnerException:\n{ex.InnerException.Message}");
                    sb.AppendLine($"{ex.InnerException.StackTrace}");
                }
                sb.AppendLine($"==================================================\n");

                File.AppendAllText(logPath, sb.ToString());
            }
            catch
            {
                // Fallback catch if disk write fails
            }
        }
    }
}
