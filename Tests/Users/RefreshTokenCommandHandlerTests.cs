using Application.CQRS.Users.Commands.Refresh;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Users
{
    public class RefreshTokenCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly Mock<ILogger<RefreshTokenCommandHandler>> _loggerMock;
        private readonly RefreshTokenCommandHandler _handler;

        public RefreshTokenCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
            _tokenServiceMock = new Mock<ITokenService>();
            _loggerMock = new Mock<ILogger<RefreshTokenCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.RefreshTokenRepository).Returns(_refreshTokenRepositoryMock.Object);

            _handler = new RefreshTokenCommandHandler(
                _unitOfWorkMock.Object,
                _tokenServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_ReturnsTokenResponse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsDeleted = false
            };
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                Token = "valid_token",
                IsRevoked = false,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            };
            var command = new RefreshTokenCommand
            {
                RefreshToken = "valid_token"
            };
            var newAccessToken = "new_access_token";
            var updatedRefreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                Token = "new_refresh_token",
                IsRevoked = false,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            };

            _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync(command.RefreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshToken);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _tokenServiceMock.Setup(t => t.GenerateAccessToken(user))
                .Returns(newAccessToken);
            _tokenServiceMock.Setup(t => t.UpdateRefreshToken(refreshToken, newAccessToken))
                .Returns(updatedRefreshToken);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(1));
            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.AccessToken.Should().Be(newAccessToken);
            result.Value.RefreshToken.Should().Be(updatedRefreshToken.Token);

            _refreshTokenRepositoryMock.Verify(r => r.UpdateAsync(It.Is<RefreshToken>(rt => rt.Token == updatedRefreshToken.Token), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_TokenNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new RefreshTokenCommand
            {
                RefreshToken = "invalid_token"
            };

            _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync(command.RefreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync((RefreshToken)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Refresh Token not found");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = "valid_token",
                IsRevoked = false,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            };
            var command = new RefreshTokenCommand
            {
                RefreshToken = "valid_token"
            };

            _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync(command.RefreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshToken);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Access forbidden");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeletedUser_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsDeleted = true
            };
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                Token = "valid_token",
                IsRevoked = false,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            };
            var command = new RefreshTokenCommand
            {
                RefreshToken = "valid_token"
            };

            _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync(command.RefreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshToken);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User was deleted");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task Handle_RevokedToken_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsDeleted = false
            };
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                Token = "valid_token",
                IsRevoked = true,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            };
            var command = new RefreshTokenCommand
            {
                RefreshToken = "valid_token"
            };

            _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync(command.RefreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshToken);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Refresh Token was revoked");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ExpiredToken_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsDeleted = false
            };
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                Token = "valid_token",
                IsRevoked = false,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
            };
            var command = new RefreshTokenCommand
            {
                RefreshToken = "valid_token"
            };

            _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync(command.RefreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshToken);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Refresh Token is expired");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsDeleted = false
            };
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                Token = "valid_token",
                IsRevoked = false,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            };
            var command = new RefreshTokenCommand
            {
                RefreshToken = "valid_token"
            };

            _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync(command.RefreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshToken);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _tokenServiceMock.Setup(t => t.GenerateAccessToken(user))
                .Returns("new_access_token");
            _tokenServiceMock.Setup(t => t.UpdateRefreshToken(refreshToken, It.IsAny<string>()))
                .Returns(new RefreshToken { Id = Guid.NewGuid(), Token = "new_refresh_token" });
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.AtLeastOnce);
        }
    }
}
