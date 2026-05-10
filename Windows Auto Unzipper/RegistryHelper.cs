using Microsoft.Win32;
using System.Windows.Forms;

namespace Windows_Auto_Unzipper
{
    class RegistryHelper
    {
        private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public static void EnableAutoRun()
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key != null)
            {
                key.SetValue(Application.ProductName, $"\"{Application.ExecutablePath}\"");
            }
        }

        public static void DisableAutoRun()
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key != null && IsAutoRunEnabled(key))
            {
                key.DeleteValue(Application.ProductName, false);
            }
        }

        public static bool IsAutoRunEnabled()
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return IsAutoRunEnabled(key);
        }

        public static bool IsAutoRunEnabled(RegistryKey key)
        {
            return key != null && key.GetValue(Application.ProductName) != null;
        }
    }
}
