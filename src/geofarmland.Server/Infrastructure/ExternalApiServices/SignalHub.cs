using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Geofarmland.Server.Infrastructure.ExternalApiServices
{
    /// <summary>
    /// Authenticated, server-push-only hub. Clients may connect to receive
    /// owner-scoped sensor and weather events but cannot publish broadcasts.
    /// </summary>
    [Authorize]
    public class SignalHub : Hub
    {
    }
}
