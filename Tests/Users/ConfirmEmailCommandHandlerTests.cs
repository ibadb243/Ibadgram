using Application.CQRS.Users.Commands.ConfirmEmail;
using Application.Interfaces.Repositories;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Users
{
    public class ConfirmEmailCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<ILogger<ConfirmEmailCommandHandler>> _loggerMock;
        private readonly ConfirmEmailCommandHandler _handler;
        private readonly CancellationToken _cancellationToken;

        public ConfirmEmailCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _loggerMock = new Mock<ILogger<ConfirmEmailCommandHandler>>();
            _cancellationToken = CancellationToken.None;

            _unitOfWorkMock.Setup(x => x.UserRepository).Returns(_userRepositoryMock.Object);

            _handler = new ConfirmEmailCommandHandler(_unitOfWorkMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                EmailConfirmed = false,
                EmailConfirmationToken = command.Code,
                EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(10)
            };

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ReturnsAsync(user);

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.SaveChangesAsync(_cancellationToken))
                .Returns(Task.FromResult(1));

            _unitOfWorkMock.Setup(x => x.CommitTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, _cancellationToken);

            // Assert
            result.IsSuccess.Should().BeTrue();
            user.EmailConfirmed.Should().BeTrue();
            user.EmailConfirmationToken.Should().BeNull();
            user.EmailConfirmationTokenExpiry.Should().BeNull();

            _userRepositoryMock.Verify(x => x.UpdateAsync(user, _cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(_cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "nonexistent@gmail.com",
                Code = "123456"
            };

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ReturnsAsync((User)null);

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.RollbackTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, _cancellationToken);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Message.Contains("User with email nonexistent@gmail.com not found"));

            _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(_cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Never);
        }

        [Fact]
        public async Task Handle_EmailAlreadyConfirmed_ReturnsFailure()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                EmailConfirmed = true,
                EmailConfirmationToken = command.Code,
                EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(10)
            };

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ReturnsAsync(user);

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.RollbackTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, _cancellationToken);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Message.Contains("Email has already been confirmed"));

            _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(_cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Never);
        }

        [Fact]
        public async Task Handle_NoConfirmationToken_ReturnsFailure()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                EmailConfirmed = false,
                EmailConfirmationToken = null,
                EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(10)
            };

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ReturnsAsync(user);

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.RollbackTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, _cancellationToken);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Message.Contains("No confirmation code found for this email"));

            _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(_cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Never);
        }

        [Fact]
        public async Task Handle_InvalidConfirmationCode_ReturnsFailure()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                EmailConfirmed = false,
                EmailConfirmationToken = "654321", // Different code
                EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(10)
            };

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ReturnsAsync(user);

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.RollbackTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, _cancellationToken);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Message.Contains("Invalid confirmation code"));

            _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(_cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Never);
        }

        [Fact]
        public async Task Handle_ExpiredConfirmationCode_ReturnsFailure()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                EmailConfirmed = false,
                EmailConfirmationToken = command.Code,
                EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(-10) // Expired
            };

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ReturnsAsync(user);

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.RollbackTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, _cancellationToken);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Message.Contains("Confirmation code has expired"));

            _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(_cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Never);
        }

        [Fact]
        public async Task Handle_ExceptionThrown_RollsBackTransaction()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ThrowsAsync(new Exception("Database error"));

            _unitOfWorkMock.Setup(x => x.RollbackTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, _cancellationToken));

            _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(_cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Never);
        }

        [Fact]
        public async Task Handle_RollbackFails_LogsError()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ThrowsAsync(new Exception("Database error"));

            _unitOfWorkMock.Setup(x => x.RollbackTransactionAsync(_cancellationToken))
                .ThrowsAsync(new Exception("Rollback failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, _cancellationToken));

            _unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(_cancellationToken), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Never);
        }

        [Fact]
        public async Task Handle_NullTokenExpiry_ValidatesSuccessfully()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                EmailConfirmed = false,
                EmailConfirmationToken = command.Code,
                EmailConfirmationTokenExpiry = null // No expiry
            };

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, _cancellationToken))
                .ReturnsAsync(user);

            _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(x => x.SaveChangesAsync(_cancellationToken))
                .Returns(Task.FromResult(1));

            _unitOfWorkMock.Setup(x => x.CommitTransactionAsync(_cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, _cancellationToken);

            // Assert
            result.IsSuccess.Should().BeTrue();
            user.EmailConfirmed.Should().BeTrue();
            user.EmailConfirmationToken.Should().BeNull();
            user.EmailConfirmationTokenExpiry.Should().BeNull();

            _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(_cancellationToken), Times.Once);
        }
    }
}
