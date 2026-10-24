using System.Security.Cryptography;
using System.Text;

namespace Geofarmland.Server.Features.Auth.RefreshToken;

public static class RefreshTokenHasher
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
