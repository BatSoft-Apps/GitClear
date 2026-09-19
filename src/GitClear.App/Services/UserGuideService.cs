using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace GitClear.App.Services;

/// <summary>
/// Finds the guide shipped next to the executable and hands it to the shell.
/// </summary>
public sealed class UserGuideService : IUserGuideService
{
    private const string GuideFolderName = "Documentation";

    private readonly string _guideFolder = Path.Combine(AppContext.BaseDirectory, GuideFolderName);

    public bool TryOpen()
    {
        string? path = Resolve();
        if (path is null)
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = path,
                UseShellExecute = true,
            };

            using Process? process = Process.Start(startInfo);
            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // No registered viewer, or the shell refused — the caller reports it.
            return false;
        }
    }

    private string? Resolve()
    {
        if (!Directory.Exists(_guideFolder))
        {
            return null;
        }

        string[] fileNames;
        try
        {
            fileNames = Directory.GetFiles(_guideFolder, "USER-GUIDE*.pdf")
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        string? chosen = UserGuideLocator.SelectForCulture(fileNames, CultureInfo.CurrentUICulture);
        return chosen is null ? null : Path.Combine(_guideFolder, chosen);
    }
}
