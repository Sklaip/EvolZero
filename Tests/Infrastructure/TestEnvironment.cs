using System.Runtime.InteropServices;

namespace Tests.Infrastructure;

internal static class TestEnvironment
{
	public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public static string ExecutableExtension => IsWindows ? ".exe" : string.Empty;

	public static string DefaultTripleName
	{
		get
		{
			if (IsWindows) return "Windows";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
			return "Unknown";
		}
	}
}
