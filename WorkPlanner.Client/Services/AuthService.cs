using System.Net.Http.Json;
using WorkPlanner.Client.Models;

namespace WorkPlanner.Client.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;
    private UserInfo? _currentUser;
    private bool _isAuthenticated;

    public event Action? OnAuthStateChanged;

    public AuthService(HttpClient httpClient, ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsAuthenticated => _isAuthenticated;
    public UserInfo? CurrentUser => _currentUser;

    public async Task<bool> LoginAsync(string email, string password, bool rememberMe = true)
    {
        try
        {
            _logger.LogInformation("Login attempt for {Email}", email);
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new
            {
                Email = email,
                Password = password,
                RememberMe = rememberMe
            });

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Login success for {Email}", email);
                await RefreshUserAsync();
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Login failed for {Email}. Status: {StatusCode}. Body: {Body}", email, response.StatusCode, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login request failed for {Email}", email);
        }

        return false;
    }

    public async Task<bool> RegisterAsync(RegisterModel model)
    {
        try
        {
            _logger.LogInformation("Registration attempt for {Email}", model.Email);
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", model);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Registration success for {Email}", model.Email);
                await RefreshUserAsync();
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Registration failed for {Email}. Status: {StatusCode}. Body: {Body}", model.Email, response.StatusCode, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration request failed for {Email}", model.Email);
        }

        return false;
    }

    public async Task LogoutAsync()
    {
        try
        {
            _logger.LogInformation("Logout attempt");
            await _httpClient.PostAsync("api/auth/logout", null);
            _logger.LogInformation("Logout success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout request failed");
        }
        finally
        {
            _currentUser = null;
            _isAuthenticated = false;
            NotifyAuthStateChanged();
        }
    }

    public async Task<bool> RefreshUserAsync()
    {
        try
        {
            var user = await _httpClient.GetFromJsonAsync<UserInfo>("api/auth/me");
            if (user != null)
            {
                _currentUser = user;
                _isAuthenticated = true;
                NotifyAuthStateChanged();
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Refresh user failed or unauthorized");
        }

        _currentUser = null;
        _isAuthenticated = false;
        NotifyAuthStateChanged();
        return false;
    }

    private void NotifyAuthStateChanged()
    {
        OnAuthStateChanged?.Invoke();
    }
}
