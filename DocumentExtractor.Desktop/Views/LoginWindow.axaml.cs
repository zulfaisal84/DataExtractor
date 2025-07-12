using Avalonia.Controls;
using DocumentExtractor.Desktop.ViewModels;

namespace DocumentExtractor.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    public LoginWindow(LoginViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseRequested += () => Close();
    }
}