using Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class PasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash_bytes = SHA3_384.HashData(bytes);
            return Convert.ToBase64String(hash_bytes);
        }

        public bool VerifyPassword(string password, string hash)
        {
            var password_hash = HashPassword(password);
            return Enumerable.SequenceEqual(hash, password_hash);
        }
    }
}
