using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WorkPlanner.Api.Models;

namespace WorkPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Registration attempt with invalid model state for {Email}", model.Email);
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Registration attempt for {Email}", model.Email);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, Roles.User);
            _logger.LogInformation("Registration succeeded for {Email}", model.Email);

            // Sign in the user after registration
            await _signInManager.SignInAsync(user, isPersistent: true);

            return Ok(new { Message = "User registered successfully" });
        }

        foreach (var error in result.Errors)
        {
            _logger.LogWarning("Registration failed for {Email}. Error: {Error}", model.Email, error.Description);
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return BadRequest(ModelState);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Login attempt with invalid model state for {Email}", model.Email);
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Login attempt for {Email}", model.Email);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("Login succeeded for {Email}", model.Email);
            return Ok(new { Message = "Login successful" });
        }

        if (result.RequiresTwoFactor)
        {
            _logger.LogWarning("Login requires 2FA for {Email}", model.Email);
            return BadRequest(new { Message = "Two factor authentication required" });
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out for {Email}", model.Email);
            return BadRequest(new { Message = "Account locked out" });
        }

        _logger.LogWarning("Invalid login attempt for {Email}", model.Email);
        return BadRequest(new { Message = "Invalid login attempt" });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return Ok(new { Message = "Logout successful" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserInfo
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles.ToList()
        });
    }
}

public class RegisterModel
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = true;
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
