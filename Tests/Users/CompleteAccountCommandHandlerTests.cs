using Application.CQRS.Users.Commands.CompleteAccount;
using Application.Interfaces.Repositories;
using Domain.Common;
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
    public class CompleteAccountCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IMentionRepository> _mentionRepositoryMock;
        private readonly Mock<ILogger<CompleteAccountCommandHandler>> _loggerMock;
        private readonly CompleteAccountCommandHandler _handler;

        public CompleteAccountCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _mentionRepositoryMock = new Mock<IMentionRepository>();
            _loggerMock = new Mock<ILogger<CompleteAccountCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.MentionRepository).Returns(_mentionRepositoryMock.Object);

            _handler = new CompleteAccountCommandHandler(
                _unitOfWorkMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handler_ValidCommand_ReturnsUserId()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                EmailConfirmed = true
            };
            var command = new CompleteAccountCommand
            {
                UserId = userId,
                Shortname = "testuser",
                Bio = "Test bio"
            };

            User capturedUser = null;
            UserMention capturedMention = null;

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, ct) => capturedUser = u)
                .ReturnsAsync((User)null);
            _mentionRepositoryMock.Setup(m => m.ExistsByShortnameAsync(command.Shortname, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mentionRepositoryMock.Setup(m => m.AddAsync(It.IsAny<Mention>(), It.IsAny<CancellationToken>()))
                .Callback<Mention, CancellationToken>((m, ct) => capturedMention = (UserMention)m)
                .ReturnsAsync((Mention)null);
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
            result.Value.Should().Be(userId);

            capturedUser.Should().NotBeNull();
            capturedUser.Id.Should().Be(userId);
            capturedUser.Bio.Should().Be(command.Bio);
            capturedUser.IsVerified.Should().BeTrue();

            capturedMention.Should().NotBeNull();
            capturedMention.Id.Should().NotBe(Guid.Empty);
            capturedMention.UserId.Should().Be(userId);
            capturedMention.Shortname.Should().Be(command.Shortname);

            _userRepositoryMock.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Id == userId && u.Bio == command.Bio && u.IsVerified), It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.Is<UserMention>(m => m.UserId == userId && m.Shortname == command.Shortname), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new CompleteAccountCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = "testuser",
                Bio = "Test bio"
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
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<UserMention>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_EmailNotConfirmed_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                EmailConfirmed = false
            };
            var command = new CompleteAccountCommand
            {
                UserId = userId,
                Shortname = "testuser",
                Bio = "Test bio"
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
            result.Errors.Should().ContainSingle(e => e.Message == "User's email address not confirmed");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<UserMention>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShortnameAlreadyTaken_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                EmailConfirmed = true
            };
            var command = new CompleteAccountCommand
            {
                UserId = userId,
                Shortname = "testuser",
                Bio = "Test bio"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _mentionRepositoryMock.Setup(m => m.ExistsByShortnameAsync(command.Shortname, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == $"Shortname {command.Shortname} had benn taken");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<UserMention>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                EmailConfirmed = true
            };
            var command = new CompleteAccountCommand
            {
                UserId = userId,
                Shortname = "testuser",
                Bio = "Test bio"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _mentionRepositoryMock.Setup(m => m.ExistsByShortnameAsync(command.Shortname, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
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
