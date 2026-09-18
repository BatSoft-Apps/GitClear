namespace GitClear.App.Services;

/// <summary>
/// Opens the user guide in the system's default viewer (UI-5).
/// </summary>
public interface IUserGuideService
{
    /// <summary>
    /// Opens the guide for the current UI culture. Returns <c>false</c> if no
    /// guide file is installed or the shell refused to open it.
    /// </summary>
    bool TryOpen();
}
