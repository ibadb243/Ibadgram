using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IRefreshTokenRepository
    {
        public Task<RefreshToken> AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
        public Task<RefreshToken> UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
        public Task DeleteAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    }
}
