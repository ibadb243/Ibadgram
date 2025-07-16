using Application.CQRS.Chats.Commands.CreateGroup;
using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class CreateGroupCommandHandlerTests
    {
        private Mock<IUnitOfWork> _unitOfWorkMock;
        private Mock<IUserRepository> _userRepositoryMock;
        private Mock<IChatRepository> _chatRepositoryMock;
        private Mock<IChatMemberRepository> _chatMemberRepositoryMock;
        private Mock<IMentionRepository> _mentionRepositoryMock;
        private Mock<ILogger<CreateGroupCommandHandler>> _loggerMock;
        private CreateGroupCommandHandler _handler;

        public CreateGroupCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _chatRepositoryMock = new Mock<IChatRepository>();
            _chatMemberRepositoryMock = new Mock<IChatMemberRepository>();
            _mentionRepositoryMock = new Mock<IMentionRepository>();
            _loggerMock = new Mock<ILogger<CreateGroupCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.ChatRepository).Returns(_chatRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.ChatMemberRepository).Returns(_chatMemberRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.MentionRepository).Returns(_mentionRepositoryMock.Object);

            _handler = new CreateGroupCommandHandler(
                _unitOfWorkMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidPublicGroupCommand_CreatesGroupAndReturnsId()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var command = new CreateGroupCommand
            {
                UserId = userId,
                Name = "Test Group",
                Description = "Test Description",
                IsPrivate = false,
                Shortname = "testgroup"
            };
            var chatId = Guid.NewGuid();

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _mentionRepositoryMock.Setup(m => m.GetByShortnameAsync(command.Shortname, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChatMention)null);
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
            result.Value.Should().NotBeEmpty();

            _chatRepositoryMock.Verify(r => r.AddAsync(It.Is<Chat>(c =>
                c.Type == ChatType.Group &&
                c.Name == command.Name &&
                c.Description == command.Description &&
                c.IsPrivate == command.IsPrivate),
                It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.Is<ChatMention>(m => m.Shortname == command.Shortname), It.IsAny<CancellationToken>()), Times.Once);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.Is<ChatMember>(m =>
                m.UserId == userId &&
                m.Role == ChatRole.Creator &&
                m.Nickname == "Creator"),
                It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ValidPrivateGroupCommand_CreatesGroupWithoutMention()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var command = new CreateGroupCommand
            {
                UserId = userId,
                Name = "Test Group",
                Description = "Test Description",
                IsPrivate = true,
                Shortname = null
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
            result.Value.Should().NotBeEmpty();

            _chatRepositoryMock.Verify(r => r.AddAsync(It.Is<Chat>(c =>
                c.Type == ChatType.Group &&
                c.Name == command.Name &&
                c.Description == command.Description &&
                c.IsPrivate == command.IsPrivate),
                It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.Is<ChatMember>(m =>
                m.UserId == userId &&
                m.Role == ChatRole.Creator &&
                m.Nickname == "Creator"),
                It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = "Test Group",
                IsPrivate = false,
                Shortname = "testgroup"
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
            result.Errors.Should().ContainSingle(e => e.Message == "User not found");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var command = new CreateGroupCommand
            {
                UserId = userId,
                Name = "Test Group",
                IsPrivate = false,
                Shortname = "testgroup"
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
            result.Errors.Should().ContainSingle(e => e.Message == "User isn't verified");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var command = new CreateGroupCommand
            {
                UserId = userId,
                Name = "Test Group",
                IsPrivate = false,
                Shortname = "testgroup"
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
            _chatRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShortnameTaken_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var command = new CreateGroupCommand
            {
                UserId = userId,
                Name = "Test Group",
                IsPrivate = false,
                Shortname = "testgroup"
            };
            var existingMention = new ChatMention
            {
                Id = Guid.NewGuid(),
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _mentionRepositoryMock.Setup(m => m.GetByShortnameAsync(command.Shortname, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingMention);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Shortname has already been taken");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
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
                IsDeleted = false
            };
            var command = new CreateGroupCommand
            {
                UserId = userId,
                Name = "Test Group",
                IsPrivate = false,
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _mentionRepositoryMock.Setup(m => m.GetByShortnameAsync(command.Shortname, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChatMention)null);
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
