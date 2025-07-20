using Application.CQRS.Chats.Commands.MakePublicGroup;
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
    public class MakePublicGroupCommandHandlerTests
    {
        private Mock<IUnitOfWork> _unitOfWorkMock;
        private Mock<IUserRepository> _userRepositoryMock;
        private Mock<IChatRepository> _chatRepositoryMock;
        private Mock<IChatMemberRepository> _chatMemberRepositoryMock;
        private Mock<IMentionRepository> _mentionRepositoryMock;
        private Mock<ILogger<MakePublicGroupCommandHandler>> _loggerMock;
        private MakePublicGroupCommandHandler _handler;

        public MakePublicGroupCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _chatRepositoryMock = new Mock<IChatRepository>();
            _chatMemberRepositoryMock = new Mock<IChatMemberRepository>();
            _mentionRepositoryMock = new Mock<IMentionRepository>();
            _loggerMock = new Mock<ILogger<MakePublicGroupCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.ChatRepository).Returns(_chatRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.ChatMemberRepository).Returns(_chatMemberRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.MentionRepository).Returns(_mentionRepositoryMock.Object);

            _handler = new MakePublicGroupCommandHandler(
                _unitOfWorkMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_MakesGroupPublicSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var group = new Chat
            {
                Id = groupId,
                Type = ChatType.Group,
                IsPrivate = true,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = groupId,
                UserId = userId,
                Role = ChatRole.Creator
            };
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = groupId,
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(group);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(groupId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _mentionRepositoryMock.Setup(m => m.ExistsByShortnameAsync(command.Shortname, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
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

            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Chat>(c => c.Id == groupId && c.IsPrivate == true), It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.Is<ChatMention>(m => m.Shortname == command.Shortname && m.ChatId == groupId), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new MakePublicGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
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
            _chatRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = Guid.NewGuid(),
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
            _chatRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = Guid.NewGuid(),
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
            _chatRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_GroupNotFound_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = Guid.NewGuid(),
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(command.GroupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Chat)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Group not found");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeletedGroup_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var group = new Chat
            {
                Id = groupId,
                Type = ChatType.Group,
                IsDeleted = true
            };
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = groupId,
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(group);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Group is deleted");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_GroupAlreadyPublic_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var group = new Chat
            {
                Id = groupId,
                Type = ChatType.Group,
                IsPrivate = false,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = groupId,
                UserId = userId,
                Role = ChatRole.Creator
            };
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = groupId,
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(group);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(groupId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Group is public");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShortnameTaken_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var group = new Chat
            {
                Id = groupId,
                Type = ChatType.Group,
                IsPrivate = true,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = groupId,
                UserId = userId,
                Role = ChatRole.Creator
            };
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = groupId,
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(group);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(groupId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
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
            result.Errors.Should().ContainSingle(e => e.Message == "Shortname has already been taken");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_UserNotMember_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var group = new Chat
            {
                Id = groupId,
                Type = ChatType.Group,
                IsPrivate = true,
                IsDeleted = false
            };
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = groupId,
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(group);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(groupId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChatMember)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User isn't member of group");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_UserNotCreator_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var group = new Chat
            {
                Id = groupId,
                Type = ChatType.Group,
                IsPrivate = true,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = groupId,
                UserId = userId,
                Role = ChatRole.Member
            };
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = groupId,
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(group);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(groupId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User isn't creator of group");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mentionRepositoryMock.Verify(m => m.AddAsync(It.IsAny<ChatMention>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var group = new Chat
            {
                Id = groupId,
                Type = ChatType.Group,
                IsPrivate = true,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = groupId,
                UserId = userId,
                Role = ChatRole.Creator
            };
            var command = new MakePublicGroupCommand
            {
                UserId = userId,
                GroupId = groupId,
                Shortname = "testgroup"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(group);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(groupId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
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
