using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace WebAPI.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal principal)
        {
            if (Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sid)?.Value ?? "", out Guid userId)) return userId;
            return Guid.Empty;
        }
    }
}
