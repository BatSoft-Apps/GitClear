using System.Windows;
using System.Windows.Threading;
using GitClear.App.Services;
using GitClear.App.ViewModels;
using GitClear.App.Views;
using GitClear.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace GitClear.App;

/// <summary>
/// Application entry point. Builds the DI container and shows the main window
/// resolved from it, so views and view models get their dependencies injected.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Last-resort handler so an unexpected UI-thread error surfaces a message
        // instead of crashing the app.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        _services.GetRequiredService<MainWindow>().Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "GitClear",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services (discovery, scanning, deletion).
        services.AddGitClearCore();

        // App services.
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IConfirmationDialog, MessageBoxConfirmationDialog>();

        // View models.
        services.AddSingleton<MainViewModel>();

        // Views.
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
