using Application.CQRS.Messages.Commands.UpdateMessage;
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

namespace Tests.Messages
{
    public class UpdateMessageCommandHandlerTests
    {
        private Mock<IUnitOfWork> _unitOfWorkMock;
        private Mock<IUserRepository> _userRepositoryMock;
        private Mock<IChatRepository> _chatRepositoryMock;
        private Mock<IChatMemberRepository> _chatMemberRepositoryMock;
        private Mock<IMessageRepository> _messageRepositoryMock;
        private Mock<ILogger<UpdateMessageCommandHandler>> _loggerMock;
        private UpdateMessageCommandHandler _handler;

        public UpdateMessageCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _chatRepositoryMock = new Mock<IChatRepository>();
            _chatMemberRepositoryMock = new Mock<IChatMemberRepository>();
            _messageRepositoryMock = new Mock<IMessageRepository>();
            _loggerMock = new Mock<ILogger<UpdateMessageCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.ChatRepository).Returns(_chatRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.ChatMemberRepository).Returns(_chatMemberRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.MessageRepository).Returns(_messageRepositoryMock.Object);

            _handler = new UpdateMessageCommandHandler(
                _unitOfWorkMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_UpdatesMessageSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var messageId = 1L;
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = chatId,
                UserId = userId
            };
            var message = new Message
            {
                Id = messageId,
                ChatId = chatId,
                UserId = userId,
                Text = "Old message",
                IsDeleted = false
            };
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = chatId,
                MessageId = messageId,
                Text = "Updated message"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _messageRepositoryMock.Setup(r => r.GetByIdAsync(chatId, messageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);
            _messageRepositoryMock.Setup(m => m.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Message)null);
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

            _messageRepositoryMock.Verify(m => m.UpdateAsync(It.Is<Message>(msg =>
                msg.Id == messageId &&
                msg.ChatId == chatId &&
                msg.Text == command.Text &&
                msg.UserId == userId &&
                msg.UpdatedAtUtc != null &&
                !msg.IsDeleted), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new UpdateMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = "Updated message"
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
            _messageRepositoryMock.Verify(m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = "Updated message"
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
            _messageRepositoryMock.Verify(m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = "Updated message"
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
            _messageRepositoryMock.Verify(m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ChatNotFound_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = "Updated message"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(command.ChatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Chat)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Chat not found");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _messageRepositoryMock.Verify(m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeletedChat_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = true
            };
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = chatId,
                MessageId = 1L,
                Text = "Updated message"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
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
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _messageRepositoryMock.Verify(m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_UserNotMember_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = false
            };
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = chatId,
                MessageId = 1L,
                Text = "Updated message"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChatMember)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User isn't member of chat");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _messageRepositoryMock.Verify(m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_MessageNotFound_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = chatId,
                UserId = userId
            };
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = chatId,
                MessageId = 1L,
                Text = "Updated message"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _messageRepositoryMock.Setup(r => r.GetByIdAsync(chatId, command.MessageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Message)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Message not found");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _messageRepositoryMock.Verify(m => m.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeletedMessage_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var messageId = 1L;
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = chatId,
                UserId = userId
            };
            var message = new Message
            {
                Id = messageId,
                ChatId = chatId,
                UserId = userId,
                Text = "Old message",
                IsDeleted = true
            };
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = chatId,
                MessageId = messageId,
                Text = "Updated message"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _messageRepositoryMock.Setup(r => r.GetByIdAsync(chatId, messageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Message is deleted");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _messageRepositoryMock.Verify(m => m.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_UserNotAuthor_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var messageId = 1L;
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = chatId,
                UserId = userId
            };
            var message = new Message
            {
                Id = messageId,
                ChatId = chatId,
                UserId = Guid.NewGuid(), // Different user
                Text = "Old message",
                IsDeleted = false
            };
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = chatId,
                MessageId = messageId,
                Text = "Updated message"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _messageRepositoryMock.Setup(r => r.GetByIdAsync(chatId, messageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User isn't author of message");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _messageRepositoryMock.Verify(m => m.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var messageId = 1L;
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = false
            };
            var member = new ChatMember
            {
                ChatId = chatId,
                UserId = userId
            };
            var message = new Message
            {
                Id = messageId,
                ChatId = chatId,
                UserId = userId,
                Text = "Old message",
                IsDeleted = false
            };
            var command = new UpdateMessageCommand
            {
                UserId = userId,
                ChatId = chatId,
                MessageId = messageId,
                Text = "Updated message"
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _messageRepositoryMock.Setup(r => r.GetByIdAsync(chatId, messageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(message);
            _messageRepositoryMock.Setup(m => m.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
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
