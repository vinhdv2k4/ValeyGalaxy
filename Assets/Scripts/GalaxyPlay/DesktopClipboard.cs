#if false
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TextCopy;

namespace GameSource
{
  public class DesktopClipboard
  {
    public static bool _availabilityChecked;
    public static bool _isAvailable;

    public static bool IsAvailable
    {
      get
      {
        if (!DesktopClipboard._availabilityChecked)
        {
          DesktopClipboard._availabilityChecked = true;
          string output = "";
          DesktopClipboard._isAvailable = DesktopClipboard.GetText(ref output);
        }
        return DesktopClipboard._isAvailable;
      }
    }

    public static bool GetText(ref string output)
    {
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        output = "";
        output = ClipboardService.GetText();
        return true;
      }
      return DesktopClipboard.externalGetText("xclip", "-o", ref output) || DesktopClipboard.externalGetText("pbpaste", "", ref output);
    }

    public static bool SetText(string text)
    {
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        ClipboardService.SetText(text);
        return true;
      }
      return DesktopClipboard.externalSetText("xclip", "-selection clipboard", text) || DesktopClipboard.externalSetText("pbcopy", "", text);
    }

    private static bool externalSetText(string executable, string arguments, string text)
    {
      ProcessStartInfo startInfo = new ProcessStartInfo(executable, arguments)
      {
        RedirectStandardInput = true,
        UseShellExecute = false
      };
      try
      {
        using (Process process = Process.Start(startInfo))
        {
          process.StandardInput.Write(text);
          process.StandardInput.Close();
          process.WaitForExit();
          return process.ExitCode == 0;
        }
      }
      catch (Exception ex)
      {
        return false;
      }
    }

    private static bool externalGetText(string executable, string arguments, ref string output)
    {
      ProcessStartInfo startInfo = new ProcessStartInfo(executable, arguments)
      {
        RedirectStandardOutput = true,
        UseShellExecute = false
      };
      try
      {
        using (Process process = Process.Start(startInfo))
        {
          string end = process.StandardOutput.ReadToEnd();
          process.StandardOutput.Close();
          process.WaitForExit();
          output = process.ExitCode != 0 ? "" : end;
          return true;
        }
      }
      catch (Exception ex)
      {
        return false;
      }
    }
  }
}
#endif
