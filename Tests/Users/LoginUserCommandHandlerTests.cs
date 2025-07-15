using Application.CQRS.Users.Commands.Login;
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
    public class LoginUserCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<ILogger<LoginUserCommandHandler>> _loggerMock;
        private readonly LoginUserCommandHandler _handler;

        public LoginUserCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
            _tokenServiceMock = new Mock<ITokenService>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _loggerMock = new Mock<ILogger<LoginUserCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.RefreshTokenRepository).Returns(_refreshTokenRepositoryMock.Object);

            _handler = new LoginUserCommandHandler(
                _unitOfWorkMock.Object,
                _tokenServiceMock.Object,
                _passwordHasherMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_ReturnsLoginResponse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Firstname = "John",
                Lastname = "Doe",
                Email = "john@gmail.com",
                Bio = "Test bio",
                PasswordHash = "hashedPassword",
                PasswordSalt = "salt",
                IsDeleted = false
            };
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = "Password123"
            };
            var accessToken = "access_token";
            var refreshToken = new RefreshToken { Id = Guid.NewGuid(), Token = "refresh_token", UserId = userId };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _passwordHasherMock.Setup(h => h.VerifyPassword(command.Password, user.PasswordSalt, user.PasswordHash))
                .Returns(true);
            _tokenServiceMock.Setup(t => t.GenerateAccessToken(user))
                .Returns(accessToken);
            _tokenServiceMock.Setup(t => t.GenerateRefreshToken(user, accessToken))
                .Returns(refreshToken);
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
            result.Value.UserId.Should().Be(userId);
            result.Value.Firstname.Should().Be(user.Firstname);
            result.Value.Lastname.Should().Be(user.Lastname);
            result.Value.Bio.Should().Be(user.Bio);
            result.Value.AccessToken.Should().Be(accessToken);
            result.Value.RefreshToken.Should().Be(refreshToken.Token);

            _refreshTokenRepositoryMock.Verify(r => r.AddAsync(It.Is<RefreshToken>(rt => rt.UserId == userId), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = "Password123"
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid email or password!");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _passwordHasherMock.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
            _refreshTokenRepositoryMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeletedUser_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "john@gmail.com",
                IsDeleted = true
            };
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = "Password123"
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
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
            _passwordHasherMock.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task Handle_InvalidPassword_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "john@gmail.com",
                PasswordHash = "hashedPassword",
                PasswordSalt = "salt",
                IsDeleted = false
            };
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = "WrongPassword"
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _passwordHasherMock.Setup(h => h.VerifyPassword(command.Password, user.PasswordSalt, user.PasswordHash))
                .Returns(false);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid email or password!");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
            _refreshTokenRepositoryMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "john@gmail.com",
                PasswordHash = "hashedPassword",
                PasswordSalt = "salt",
                IsDeleted = false
            };
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = "Password123"
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _passwordHasherMock.Setup(h => h.VerifyPassword(command.Password, user.PasswordSalt, user.PasswordHash))
                .Returns(true);
            _tokenServiceMock.Setup(t => t.GenerateAccessToken(user))
                .Returns("access_token");
            _tokenServiceMock.Setup(t => t.GenerateRefreshToken(user, It.IsAny<string>()))
                .Returns(new RefreshToken { Id = Guid.NewGuid(), Token = "refresh_token", UserId = userId });
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
