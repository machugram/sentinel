namespace Sentinel.Infrastructure.Auth;

/// <summary>
/// Mock authentication service for development and testing.
/// Replace with real OAuth2/OIDC implementation in production.
/// </summary>
public class MockAuthService : IAuthService
{
    private AuthenticatedUser? _currentUser;
    private string? _accessToken;
    
    public AuthenticatedUser? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    
    public Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        // Mock authentication - accept any credentials for development
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return Task.FromResult(new AuthResult
            {
                Success = false,
                ErrorMessage = "Username and password are required"
            });
        }
        
        _currentUser = new AuthenticatedUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"{username}@sentinel.example.com",
            DisplayName = username,
            Roles = new[] { "User", "Operator" },
            Permissions = new[] { "workflows:read", "workflows:write", "runs:read", "runs:write", "alerts:read" },
            TokenExpiry = DateTime.UtcNow.AddHours(8)
        };
        
        _accessToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        
        return Task.FromResult(new AuthResult
        {
            Success = true,
            User = _currentUser,
            AccessToken = _accessToken,
            RefreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        });
    }
    
    public Task<AuthResult> LoginWithOAuthAsync(string provider, CancellationToken cancellationToken = default)
    {
        // Mock OAuth - simulate successful login
        _currentUser = new AuthenticatedUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"oauth.user@sentinel.example.com",
            DisplayName = $"OAuth User ({provider})",
            Roles = new[] { "User", "Operator" },
            Permissions = new[] { "workflows:read", "workflows:write", "runs:read", "runs:write", "alerts:read" },
            TokenExpiry = DateTime.UtcNow.AddHours(8)
        };
        
        _accessToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        
        return Task.FromResult(new AuthResult
        {
            Success = true,
            User = _currentUser,
            AccessToken = _accessToken,
            RefreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        });
    }
    
    public Task<AuthResult> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser == null)
        {
            return Task.FromResult(new AuthResult
            {
                Success = false,
                ErrorMessage = "No active session"
            });
        }
        
        _currentUser.TokenExpiry = DateTime.UtcNow.AddHours(8);
        _accessToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        
        return Task.FromResult(new AuthResult
        {
            Success = true,
            User = _currentUser,
            AccessToken = _accessToken,
            RefreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        });
    }
    
    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _currentUser = null;
        _accessToken = null;
        return Task.CompletedTask;
    }
    
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accessToken);
    }
    
    public bool HasPermission(string permission)
    {
        return _currentUser?.Permissions.Contains(permission) ?? false;
    }
    
    public bool HasRole(string role)
    {
        return _currentUser?.Roles.Contains(role) ?? false;
    }
}
