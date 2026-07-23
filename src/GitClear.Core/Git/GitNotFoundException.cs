namespace GitClear.Core.Git;

/// <summary>
/// Thrown when the <c>git</c> executable cannot be found on PATH (ARCH-3
/// requires an installed git; this is a handled error, not a fallback).
/// </summary>
public sealed class GitNotFoundException : Exception
{
    public GitNotFoundException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
