using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Serilog;
using Hexa.NET.ImGui.Backends.D3D9;
using IDirect3DDevice9 = Windows.Win32.Graphics.Direct3D9.IDirect3DDevice9;

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

    public static unsafe IDirect3DDevice9Ptr ToHexaDevice(IDirect3DDevice9* device) {
        return new IDirect3DDevice9Ptr((Hexa.NET.ImGui.Backends.D3D9.IDirect3DDevice9*) device);
    }
}
