using System.Windows;
using GitClear.App.ViewModels;

namespace GitClear.App.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml. The view model is injected by the DI
/// container and set as the data context; the view stays logic-free (MVVM).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
