using System;
using System.Diagnostics;
namespace OneDriveFileDownloader.UI.Services
{
	public class SystemProcessLauncher : IProcessLauncher
	{
		public bool Open(string path)
		{
			try
			{
				if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return false;
				if (OperatingSystem.IsWindows())
				{
					Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
				}
				else if (OperatingSystem.IsLinux())
				{
					Process.Start("xdg-open", path);
				}
				else if (OperatingSystem.IsMacOS())
				{
					Process.Start("open", path);
				}
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}