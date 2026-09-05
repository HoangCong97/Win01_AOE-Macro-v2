using System.Diagnostics;
using System.Security.Principal;
using AOEKeyboardMacroPro.Services;

namespace AOEKeyboardMacroPro;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Initialize global exception logger
        CrashLogger.Initialize();

        // Enable 1ms high precision timer resolution for Windows
        NativeMethods.TimeBeginPeriod(1);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());

        NativeMethods.TimeEndPeriod(1);
    }
}