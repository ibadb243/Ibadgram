using Application.CQRS.Users.Commands.CreateAccount;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Users
{
    public class CreateAccountCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<IEmailSender> _emailSenderMock;
        private readonly Mock<ILogger<CreateAccountCommandHandler>> _loggerMock;
        private readonly CreateAccountCommandHandler _handler;

        public CreateAccountCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _emailSenderMock = new Mock<IEmailSender>();
            _loggerMock = new Mock<ILogger<CreateAccountCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);

            _handler = new CreateAccountCommandHandler(
                _unitOfWorkMock.Object,
                _passwordHasherMock.Object,
                _emailSenderMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_ReturnsUserId()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "Bob",
                Email = "bob@gmail.com",
                Password = "password123",
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _passwordHasherMock.Setup(h => h.HashPassword(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("hashedPassword");
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(1));
            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _emailSenderMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBe(Guid.Empty);

            _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _emailSenderMock.Verify(e => e.SendEmailAsync(
                command.Email,
                "Code for confirm email",
                It.Is<string>(html => html.Contains("Welcome to Ibadgram!"))),
                Times.Once);
        }

        [Fact]
        public async Task Handle_EmailAlreadyExists_VerifiedUser_ReturnsFailure()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "Bob",
                Email = "bob@gmail.com",
                Password = "password123",
            };

            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                IsVerified = true,
                EmailConfirmed = false
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingUser);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Email has already been used");

            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_EmailAlreadyExists_EmailConfirmed_ReturnsFailure()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "Bob",
                Email = "bob@gmail.com",
                Password = "password123",
            };

            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                IsVerified = false,
                EmailConfirmed = true
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingUser);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Email has already been confirmed");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_EmailAlreadyExists_AwaitingConfirmation_ReturnsFailure()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "Bob",
                Email = "bob@gmail.com",
                Password = "password123",
            };

            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                IsVerified = false,
                EmailConfirmed = false
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingUser);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Email has awated confirmation");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_EmailSendingFails_StillReturnsSuccess()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "Bob",
                Email = "bob@gmail.com",
                Password = "password123",
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _passwordHasherMock.Setup(h => h.HashPassword(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("hashedPassword");
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(1));
            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _emailSenderMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Email service unavailable"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBe(Guid.Empty);

            _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "Bob",
                Email = "bob@gmail.com",
                Password = "password123",
            };

            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ValidCommand_CreatesUserWithCorrectProperties()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "Bob",
                Lastname = "Smith",
                Email = "bob@gmail.com",
                Password = "password123",
            };

            User capturedUser = null;
            _userRepositoryMock.Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((user, ct) => capturedUser = user)
                .ReturnsAsync(capturedUser);
            _passwordHasherMock.Setup(h => h.HashPassword(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("hashedPassword");
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(1));
            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _emailSenderMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            capturedUser.Should().NotBeNull();
            capturedUser.Id.Should().NotBe(Guid.Empty);
            capturedUser.Firstname.Should().Be(command.Firstname);
            capturedUser.Lastname.Should().Be(command.Lastname);
            capturedUser.Email.Should().Be(command.Email);
            capturedUser.PasswordHash.Should().Be("hashedPassword");
            capturedUser.PasswordSalt.Should().NotBeNullOrEmpty();
            capturedUser.EmailConfirmed.Should().BeFalse();
            capturedUser.EmailConfirmationToken.Should().NotBeNullOrEmpty();
            capturedUser.EmailConfirmationTokenExpiry.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(10));

            _passwordHasherMock.Verify(h => h.HashPassword(command.Password, capturedUser.PasswordSalt), Times.Once);
        }
    }
}
