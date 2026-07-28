using System.Windows;
using Balancer.Wpf.ViewModels;

namespace Balancer.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
