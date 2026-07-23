using System.Globalization;

namespace GitClear.App.Formatting;

/// <summary>
/// Formats byte counts as human-readable sizes (1024-based, Windows-style labels).
/// </summary>
public static class ByteSize
{
    private static readonly string[] Units = ["bytes", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} {(bytes == 1 ? "byte" : "bytes")}";
        }

        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{size:0.##} {Units[unit]}");
    }
}
