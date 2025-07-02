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
        public string HashPassword(string password, string salt)
        {
            var saltBytes = Encoding.UTF8.GetBytes(salt);
            var passwordBytes = Encoding.UTF8.GetBytes(password);

            var saltedPassword = new byte[saltBytes.Length + passwordBytes.Length];
            Buffer.BlockCopy(saltBytes, 0, saltedPassword, 0, saltBytes.Length);
            Buffer.BlockCopy(passwordBytes, 0, saltedPassword, saltBytes.Length, passwordBytes.Length);

            var hashBytes = SHA3_384.HashData(saltedPassword);
            return Convert.ToBase64String(hashBytes);
        }

        public bool VerifyPassword(string password, string salt, string hash)
        {
            var password_hash = HashPassword(password, salt);
            return Enumerable.SequenceEqual(hash, password_hash);
        }
    }
}
