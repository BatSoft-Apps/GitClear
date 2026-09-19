using System.Globalization;
using GitClear.App.Services;

namespace GitClear.App.Tests.Services;

public sealed class UserGuideLocatorTests
{
    private static readonly string[] EnglishOnly = ["USER-GUIDE.pdf"];

    private static readonly string[] Translated =
    [
        "USER-GUIDE.pdf",
        "USER-GUIDE.fr.pdf",
        "USER-GUIDE.fr-CA.pdf",
        "USER-GUIDE.de.pdf",
    ];

    private static string? Select(string[] files, string culture)
        => UserGuideLocator.SelectForCulture(files, new CultureInfo(culture));

    [Fact]
    public void Prefers_an_exact_culture_match()
    {
        Assert.Equal("USER-GUIDE.fr-CA.pdf", Select(Translated, "fr-CA"));
    }

    [Fact]
    public void Falls_back_to_the_parent_language()
    {
        // fr-FR has no file of its own, but fr does.
        Assert.Equal("USER-GUIDE.fr.pdf", Select(Translated, "fr-FR"));
    }

    [Fact]
    public void Falls_back_to_the_neutral_guide_for_an_untranslated_culture()
    {
        Assert.Equal("USER-GUIDE.pdf", Select(Translated, "ja-JP"));
    }

    [Fact]
    public void Uses_the_neutral_guide_when_nothing_is_translated()
    {
        Assert.Equal("USER-GUIDE.pdf", Select(EnglishOnly, "de-DE"));
    }

    [Fact]
    public void Returns_null_when_no_guide_is_installed()
    {
        Assert.Null(Select([], "en-AU"));
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        Assert.Equal("user-guide.FR.pdf", Select(["user-guide.FR.pdf"], "fr-FR"));
    }

    [Fact]
    public void Ignores_unrelated_files()
    {
        Assert.Null(Select(["USER-GUIDE.es.pdf"], "fr-FR"));
    }
}
