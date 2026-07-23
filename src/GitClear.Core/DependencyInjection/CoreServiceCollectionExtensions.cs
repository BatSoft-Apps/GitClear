using GitClear.Core.Deletion;
using GitClear.Core.Discovery;
using GitClear.Core.Git;
using GitClear.Core.Scanning;
using Microsoft.Extensions.DependencyInjection;

namespace GitClear.Core.DependencyInjection;

/// <summary>
/// Single composition entry point for the Core library. A host (the WPF app, a
/// test, or any future front end) registers every Core service by calling
/// <see cref="AddGitClearCore"/>, without needing to know the concrete types.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers all GitClear.Core services (discovery, scanning, deletion).
    /// </summary>
    public static IServiceCollection AddGitClearCore(this IServiceCollection services)
    {
        services.AddSingleton<IRepositoryDiscoveryService, RepositoryDiscoveryService>();
        services.AddSingleton<IGitClient, GitCommandLineClient>();
        services.AddSingleton<IIgnoredFileScanner, GitIgnoredFileScanner>();
        services.AddSingleton<IRecycleBinService, WindowsRecycleBinService>();
        services.AddSingleton<IDeletionService, RecycleBinDeletionService>();
        return services;
    }
}
