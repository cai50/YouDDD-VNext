using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Zack.JWT
{
    public class TokenService : ITokenService
    {
        public string BuildToken(IEnumerable<Claim> claims, JWTOptions options)
        {
            //TimeSpan ExpiryDuration = TimeSpan.FromSeconds(options.ExpireSeconds);
            //var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
            //var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
            //var tokenDescriptor = new JwtSecurityToken(options.Issuer, options.Audience, claims,
            //    expires: DateTime.Now.Add(ExpiryDuration), signingCredentials: credentials);
            //return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

            // 使用新版 JsonWebTokenHandler 替代 JwtSecurityTokenHandler
            var handler = new JsonWebTokenHandler();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddSeconds(options.ExpireSeconds), // 使用 UtcNow 避开时区坑
                Issuer = options.Issuer,
                Audience = options.Audience,
                SigningCredentials = credentials
            };

            // 直接生成 Token 字符串，性能更优
            return handler.CreateToken(tokenDescriptor);
        }
    }
}
