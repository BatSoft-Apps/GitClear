using System.Globalization;

namespace GitClear.App.Services;

/// <summary>
/// Chooses which user-guide file to show for a given UI culture (UI-5).
/// Files are named <c>USER-GUIDE.pdf</c> for the neutral/default language and
/// <c>USER-GUIDE.&lt;culture&gt;.pdf</c> for a translation, so a translated guide
/// is added by dropping the file in — no code change.
/// Pure and filesystem-free so the fallback order is unit-testable.
/// </summary>
public static class UserGuideLocator
{
    private const string BaseName = "USER-GUIDE";
    private const string Extension = ".pdf";

    /// <summary>
    /// Picks the most specific available guide for <paramref name="culture"/>:
    /// the exact culture, then each parent culture, then the neutral file.
    /// Returns <c>null</c> when no guide is available at all.
    /// </summary>
    public static string? SelectForCulture(IReadOnlyCollection<string> availableFileNames, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(availableFileNames);
        ArgumentNullException.ThrowIfNull(culture);

        foreach (string candidate in CandidatesFor(culture))
        {
            foreach (string available in availableFileNames)
            {
                if (string.Equals(available, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return available;
                }
            }
        }

        return null;
    }

    /// <summary>Candidate file names, most specific first.</summary>
    private static IEnumerable<string> CandidatesFor(CultureInfo culture)
    {
        CultureInfo current = culture;
        while (!string.IsNullOrEmpty(current.Name))
        {
            yield return $"{BaseName}.{current.Name}{Extension}";
            current = current.Parent;
        }

        yield return BaseName + Extension;
    }
}
