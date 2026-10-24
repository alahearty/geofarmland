namespace Geofarmland.Server.Features.Auth.Login;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public IList<string> Roles { get; init; } = [];
}

