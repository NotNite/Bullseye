using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Serilog;

namespace Bullseye.Util;

// Generic stuff that doesn't fit into a specific class
public class Utils {
    public static void ErrorWithMessageBox(Exception? e, string text) {
        Log.Error(e, text);

        const MESSAGEBOX_STYLE flags = MESSAGEBOX_STYLE.MB_YESNO | MESSAGEBOX_STYLE.MB_ICONWARNING;
        var result = PInvoke.MessageBox(HWND.Null,
            $"""
             An error occured in Bullseye, please report this:

             {text}
             {e}

             Do you want to open Bullseye's GitHub page?
             """,
            "Bullseye",
            flags);

        if (result == MESSAGEBOX_RESULT.IDYES) {
            Process.Start(new ProcessStartInfo($"{Bullseye.GitHubRepository}/issues") {
                UseShellExecute = true
            });
        }
    }
}
