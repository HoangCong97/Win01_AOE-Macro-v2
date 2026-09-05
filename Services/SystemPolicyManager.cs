using Microsoft.Win32;

namespace AOEKeyboardMacroPro.Services;

public static class SystemPolicyManager
{
    /// <summary>
    /// Disables or enables Windows LockWorkStation (Win+L screen lock) via Registry policy.
    /// This guarantees that Win+L cannot lock the computer screen during gaming macro execution.
    /// </summary>
    public static void SetLockWorkstationDisabled(bool disable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
            if (key != null)
            {
                if (disable)
                {
                    key.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);
                }
                else
                {
                    try
                    {
                        key.DeleteValue("DisableLockWorkstation", false);
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
    }
}
