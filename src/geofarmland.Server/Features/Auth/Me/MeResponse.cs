namespace Geofarmland.Server.Features.Auth.Me;

public class MeResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public IList<string> Roles { get; set; } = [];
}
