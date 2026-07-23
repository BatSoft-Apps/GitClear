namespace GitClear.Core.Deletion;

/// <summary>
/// Thrown when moving files to the Recycle Bin fails at the shell level.
/// </summary>
public sealed class DeletionException : Exception
{
    public DeletionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
