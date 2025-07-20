using Application.CQRS.Chats.Queries;
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
    public class GetChatQueryHandlerTests
    {
        private Mock<IUserRepository> _userRepositoryMock;
        private Mock<IChatRepository> _chatRepositoryMock;
        private Mock<IChatMemberRepository> _chatMemberRepositoryMock;
        private Mock<ILogger<GetChatQueryHandler>> _loggerMock;
        private GetChatQueryHandler _handler;

        public GetChatQueryHandlerTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _chatRepositoryMock = new Mock<IChatRepository>();
            _chatMemberRepositoryMock = new Mock<IChatMemberRepository>();
            _loggerMock = new Mock<ILogger<GetChatQueryHandler>>();

            _handler = new GetChatQueryHandler(
                _userRepositoryMock.Object,
                _chatRepositoryMock.Object,
                _chatMemberRepositoryMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidPersonalChat_ReturnsPersonalChatVm()
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
                Type = ChatType.Personal,
                IsDeleted = false,
                Messages = new List<Message> { new Message(), new Message() }
            };
            var member = new ChatMember
            {
                ChatId = chatId,
                UserId = userId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);

            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = chatId
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeOfType<PersonalChatVm>();
            var vm = (PersonalChatVm)result.Value;
            vm.Type.Should().Be(ChatType.Personal);
            vm.IsDeleted.Should().BeFalse();
            vm.MessageCount.Should().Be(2);
        }

        [Fact]
        public async Task Handle_ValidOneToOneChat_ReturnsOneToOneChatVm()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var otherUser = new User
            {
                Id = otherUserId,
                Firstname = "John",
                Lastname = "Doe",
                Bio = "Test bio",
                IsDeleted = false,
                Mention = new UserMention { Shortname = "johndoe" }
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.OneToOne,
                IsDeleted = false,
                Members = new List<ChatMember>
            {
                new ChatMember { UserId = userId, User = user },
                new ChatMember { UserId = otherUserId, User = otherUser }
            }
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatMember { ChatId = chatId, UserId = userId });

            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = chatId
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeOfType<OneToOneChatVm>();
            var vm = (OneToOneChatVm)result.Value;
            vm.Type.Should().Be(ChatType.OneToOne);
            vm.IsDeleted.Should().BeFalse();
            vm.Firstname.Should().Be("John");
            vm.Lastname.Should().Be("Doe");
            vm.Description.Should().Be("Test bio");
            vm.Shortname.Should().Be("johndoe");
            vm.IsOtherUserDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_ValidGroupChat_ReturnsGroupChatVm()
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
                IsPrivate = false,
                IsDeleted = false,
                Name = "Test Group",
                Description = "Test Description",
                Mention = new ChatMention { Shortname = "testgroup" },
                Members = new List<ChatMember> { new ChatMember(), new ChatMember() }
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);

            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = chatId
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeOfType<GroupChatVm>();
            var vm = (GroupChatVm)result.Value;
            vm.Type.Should().Be(ChatType.Group);
            vm.IsDeleted.Should().BeFalse();
            vm.Name.Should().Be("Test Group");
            vm.Description.Should().Be("Test Description");
            vm.Shortname.Should().Be("testgroup");
            vm.MemberCount.Should().Be(2);
            vm.IsPrivate.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_DeletedChat_ReturnsDeletedChatVm()
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

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);

            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = chatId
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeOfType<DeletedChatVm>();
            var vm = (DeletedChatVm)result.Value;
            vm.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var query = new GetChatQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(query.UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User not found");

            _chatRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User is not verified");

            _chatRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User is deleted");

            _chatRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(query.ChatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Chat)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Chat not found");

            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AccessDeniedPrivateGroup_ReturnsFailure()
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
                IsPrivate = true,
                IsDeleted = false
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChatMember)null);

            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = chatId
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Access denied");
        }

        [Fact]
        public async Task Handle_OneToOneChatOtherUserDeleted_ReturnsOneToOneChatVmWithDeletedUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsVerified = true,
                IsDeleted = false
            };
            var otherUser = new User
            {
                Id = otherUserId,
                IsDeleted = true
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.OneToOne,
                IsDeleted = false,
                Members = new List<ChatMember>
            {
                new ChatMember { UserId = userId, User = user },
                new ChatMember { UserId = otherUserId, User = otherUser }
            }
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatMember { ChatId = chatId, UserId = userId });

            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = chatId
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeOfType<OneToOneChatVm>();
            var vm = (OneToOneChatVm)result.Value;
            vm.Type.Should().Be(ChatType.OneToOne);
            vm.IsDeleted.Should().BeFalse();
            vm.IsOtherUserDeleted.Should().BeTrue();
            vm.Firstname.Should().Be("Deleted User");
            vm.Lastname.Should().BeNull();
            vm.Description.Should().Be("This user has been deleted");
            vm.Shortname.Should().Be("deleted_user");
        }

        [Fact]
        public async Task Handle_OneToOneChatInvalidMemberStructure_ReturnsDeletedChatVm()
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
                Type = ChatType.OneToOne,
                IsDeleted = false,
                Members = new List<ChatMember> { new ChatMember { UserId = userId, User = user } }
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatMember { ChatId = chatId, UserId = userId });

            var query = new GetChatQuery
            {
                UserId = userId,
                ChatId = chatId
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeOfType<DeletedChatVm>();
            var vm = (DeletedChatVm)result.Value;
            vm.IsDeleted.Should().BeTrue();
        }
    }
}
