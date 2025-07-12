using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentExtractor.Desktop.Services;

namespace DocumentExtractor.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _error = string.Empty;

    public IRelayCommand LoginCommand { get; }

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
    }

    private async Task LoginAsync()
    {
        var ok = await _authService.LoginAsync(Email, Password);
        if (!ok)
        {
            Error = "Invalid credentials";
        }
        else
        {
            Error = string.Empty;
            CloseRequested?.Invoke();
        }
    }

    public event Action? CloseRequested;
}