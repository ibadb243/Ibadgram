using Application.CQRS.Users.Commands.UpdateUser;
using Application.Interfaces.Repositories;
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
    public class UpdateUserCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<ILogger<UpdateUserCommandHandler>> _loggerMock;
        private readonly UpdateUserCommandHandler _handler;

        public UpdateUserCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _loggerMock = new Mock<ILogger<UpdateUserCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);

            _handler = new UpdateUserCommandHandler(
                _unitOfWorkMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_ReturnsSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false,
                Firstname = "OldFirstname",
                Lastname = "OldLastname",
                Bio = "Old bio"
            };
            var command = new UpdateUserCommand
            {
                UserId = userId,
                Firstname = "NewFirstname",
                Lastname = "NewLastname",
                Bio = "New bio"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
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

            _userRepositoryMock.Verify(r => r.UpdateAsync(It.Is<User>(u =>
                u.Id == userId &&
                u.Firstname == command.Firstname &&
                u.Lastname == command.Lastname &&
                u.Bio == command.Bio), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Firstname = "NewFirstname"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(command.UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == $"User with id {command.UserId} not found");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_UserNotVerified_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = false,
                IsDeleted = false
            };
            var command = new UpdateUserCommand
            {
                UserId = userId,
                Firstname = "NewFirstname"
            };

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
            result.Errors.Should().ContainSingle(e => e.Message == "User should pass full registration");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeletedUser_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = true
            };
            var command = new UpdateUserCommand
            {
                UserId = userId,
                Firstname = "NewFirstname"
            };

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
            result.Errors.Should().ContainSingle(e => e.Message == "User is deleted");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false,
                Firstname = "OldFirstname"
            };
            var command = new UpdateUserCommand
            {
                UserId = userId,
                Firstname = "NewFirstname"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
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

        [Fact]
        public async Task Handle_PartialUpdate_UpdatesOnlySpecifiedFields()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false,
                Firstname = "OldFirstname",
                Lastname = "OldLastname",
                Bio = "Old bio"
            };
            var command = new UpdateUserCommand
            {
                UserId = userId,
                Firstname = "NewFirstname",
                Lastname = null,
                Bio = null
            };

            User capturedUser = null;
            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, ct) => capturedUser = u)
                .ReturnsAsync((User)null);
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
            capturedUser.Should().NotBeNull();
            capturedUser.Id.Should().Be(userId);
            capturedUser.Firstname.Should().Be(command.Firstname);
            capturedUser.Lastname.Should().Be(user.Lastname);
            capturedUser.Bio.Should().Be(user.Bio);

            _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
