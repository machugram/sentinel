namespace Sentinel.Infrastructure.Auth;

/// <summary>
/// Represents the current authenticated user.
/// </summary>
public class AuthenticatedUser
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string? AvatarUrl { get; set; }
    public DateTime TokenExpiry { get; set; }
}

/// <summary>
/// Authentication result from login operations.
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public AuthenticatedUser? User { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service for authentication and user management.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    AuthenticatedUser? CurrentUser { get; }
    
    /// <summary>
    /// Gets whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Authenticates with username and password.
    /// </summary>
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Initiates OAuth2/OIDC login flow.
    /// </summary>
    Task<AuthResult> LoginWithOAuthAsync(string provider, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refreshes the current access token.
    /// </summary>
    Task<AuthResult> RefreshTokenAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs out the current user.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current access token for API calls.
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user has a specific permission.
    /// </summary>
    bool HasPermission(string permission);
    
    /// <summary>
    /// Checks if the current user has a specific role.
    /// </summary>
    bool HasRole(string role);
}
