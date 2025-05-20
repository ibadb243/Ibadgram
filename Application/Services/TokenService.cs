using Application.Interfaces.Services;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly SymmetricSecurityKey _key;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly TimeSpan _refreshTokenLifetime;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;

            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured")));

            _accessTokenLifetime = TimeSpan.FromMinutes(
                double.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15"));

            _refreshTokenLifetime = TimeSpan.FromDays(
                double.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "3"));
        }

        public string GenerateAccessToken(User user)
        {
            var claims = new List<Claim>()
            {
                new(JwtRegisteredClaimNames.Sid, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Name, user.Fullname),
                new(JwtRegisteredClaimNames.UniqueName, user.Mention.Shortname),
            };

            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow + _accessTokenLifetime,
                signingCredentials: creds,
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"]);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken(string access_token)
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);

            // Optionally link refresh token to access token
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(access_token);
            var jti = jwtToken.Claims.First(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value;

            return Convert.ToHexString(randomNumber);
        }
    }
}
