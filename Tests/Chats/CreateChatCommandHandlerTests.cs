using Application.CQRS.Chats.Commands.CreateChat;
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
    public class CreateChatCommandHandlerTests
    {
        private Mock<IUnitOfWork> _unitOfWorkMock;
        private Mock<IUserRepository> _userRepositoryMock;
        private Mock<IChatRepository> _chatRepositoryMock;
        private Mock<IChatMemberRepository> _chatMemberRepositoryMock;
        private Mock<ILogger<CreateChatCommandHandler>> _loggerMock;
        private CreateChatCommandHandler _handler;

        public CreateChatCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _chatRepositoryMock = new Mock<IChatRepository>();
            _chatMemberRepositoryMock = new Mock<IChatMemberRepository>();
            _loggerMock = new Mock<ILogger<CreateChatCommandHandler>>();

            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.ChatRepository).Returns(_chatRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.ChatMemberRepository).Returns(_chatMemberRepositoryMock.Object);

            _handler = new CreateChatCommandHandler(
                _unitOfWorkMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_CreatesChatAndReturnsId()
        {
            // Arrange
            var firstUserId = Guid.NewGuid();
            var secondUserId = Guid.NewGuid();
            var firstUser = new User
            {
                Id = firstUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var secondUser = new User
            {
                Id = secondUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var command = new CreateChatCommand
            {
                FirstUserId = firstUserId,
                SecondUserId = secondUserId
            };
            var chatId = Guid.NewGuid();

            _userRepositoryMock.Setup(r => r.GetByIdAsync(firstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstUser);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(secondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(secondUser);
            _chatRepositoryMock.Setup(r => r.FindOneToOneChatAsync(firstUserId, secondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Chat)null);
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

            _chatRepositoryMock.Verify(r => r.AddAsync(It.Is<Chat>(c => c.Type == ChatType.OneToOne), It.IsAny<CancellationToken>()), Times.Once);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.Is<ChatMember>(m => m.UserId == firstUserId), It.IsAny<CancellationToken>()), Times.Once);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.Is<ChatMember>(m => m.UserId == secondUserId), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_FirstUserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new CreateChatCommand
            {
                FirstUserId = Guid.NewGuid(),
                SecondUserId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(command.FirstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "First user not found");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.GetByIdAsync(command.SecondUserId, It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.FindOneToOneChatAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_FirstUserNotVerified_ReturnsFailure()
        {
            // Arrange
            var firstUserId = Guid.NewGuid();
            var firstUser = new User
            {
                Id = firstUserId,
                IsVerified = false,
                IsDeleted = false
            };
            var command = new CreateChatCommand
            {
                FirstUserId = firstUserId,
                SecondUserId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(firstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstUser);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "First user not verified");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.GetByIdAsync(command.SecondUserId, It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.FindOneToOneChatAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_FirstUserDeleted_ReturnsFailure()
        {
            // Arrange
            var firstUserId = Guid.NewGuid();
            var firstUser = new User
            {
                Id = firstUserId,
                IsVerified = true,
                IsDeleted = true
            };
            var command = new CreateChatCommand
            {
                FirstUserId = firstUserId,
                SecondUserId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(firstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstUser);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "First user was deleted");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _userRepositoryMock.Verify(r => r.GetByIdAsync(command.SecondUserId, It.IsAny<CancellationToken>()), Times.Never);
            _chatRepositoryMock.Verify(r => r.FindOneToOneChatAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_SecondUserNotFound_ReturnsFailure()
        {
            // Arrange
            var firstUserId = Guid.NewGuid();
            var firstUser = new User
            {
                Id = firstUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var command = new CreateChatCommand
            {
                FirstUserId = firstUserId,
                SecondUserId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(firstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstUser);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(command.SecondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Second user not found");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatRepositoryMock.Verify(r => r.FindOneToOneChatAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_SecondUserNotVerified_ReturnsFailure()
        {
            // Arrange
            var firstUserId = Guid.NewGuid();
            var secondUserId = Guid.NewGuid();
            var firstUser = new User
            {
                Id = firstUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var secondUser = new User
            {
                Id = secondUserId,
                IsVerified = false,
                IsDeleted = false
            };
            var command = new CreateChatCommand
            {
                FirstUserId = firstUserId,
                SecondUserId = secondUserId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(firstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstUser);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(secondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(secondUser);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Second user not verified");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatRepositoryMock.Verify(r => r.FindOneToOneChatAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_SecondUserDeleted_ReturnsFailure()
        {
            // Arrange
            var firstUserId = Guid.NewGuid();
            var secondUserId = Guid.NewGuid();
            var firstUser = new User
            {
                Id = firstUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var secondUser = new User
            {
                Id = secondUserId,
                IsVerified = true,
                IsDeleted = true
            };
            var command = new CreateChatCommand
            {
                FirstUserId = firstUserId,
                SecondUserId = secondUserId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(firstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstUser);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(secondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(secondUser);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Second user was deleted");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatRepositoryMock.Verify(r => r.FindOneToOneChatAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ExistingChat_ReturnsFailure()
        {
            // Arrange
            var firstUserId = Guid.NewGuid();
            var secondUserId = Guid.NewGuid();
            var firstUser = new User
            {
                Id = firstUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var secondUser = new User
            {
                Id = secondUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var existingChat = new Chat
            {
                Id = Guid.NewGuid(),
                Type = ChatType.OneToOne
            };
            var command = new CreateChatCommand
            {
                FirstUserId = firstUserId,
                SecondUserId = secondUserId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(firstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstUser);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(secondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(secondUser);
            _chatRepositoryMock.Setup(r => r.FindOneToOneChatAsync(firstUserId, secondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingChat);
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Chat has already been created");

            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _chatRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Chat>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMember>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var firstUserId = Guid.NewGuid();
            var secondUserId = Guid.NewGuid();
            var firstUser = new User
            {
                Id = firstUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var secondUser = new User
            {
                Id = secondUserId,
                IsVerified = true,
                IsDeleted = false
            };
            var command = new CreateChatCommand
            {
                FirstUserId = firstUserId,
                SecondUserId = secondUserId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(firstUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstUser);
            _userRepositoryMock.Setup(r => r.GetByIdAsync(secondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(secondUser);
            _chatRepositoryMock.Setup(r => r.FindOneToOneChatAsync(firstUserId, secondUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Chat)null);
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
