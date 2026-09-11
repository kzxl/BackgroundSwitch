using System.ComponentModel;
using System.Windows;
using BackgroundSwitch.ViewModels;

namespace BackgroundSwitch;

public partial class MainWindow : Window
{
    private bool _isRealClose;

    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isRealClose)
        {
            e.Cancel = true;
            Hide();
            ((App)System.Windows.Application.Current).TrimWorkingSetMemory();
        }
    }

    public void ForceClose()
    {
        _isRealClose = true;
        Close();
    }
}