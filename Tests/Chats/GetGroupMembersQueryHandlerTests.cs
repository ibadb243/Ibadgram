using Application.CQRS.Chats.Queries.GetGroupMembers;
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
    public class GetGroupMembersQueryHandlerTests
    {
        private Mock<IUserRepository> _userRepositoryMock;
        private Mock<IChatRepository> _chatRepositoryMock;
        private Mock<IChatMemberRepository> _chatMemberRepositoryMock;
        private Mock<ILogger<GetGroupMembersQueryHandler>> _loggerMock;
        private GetGroupMembersQueryHandler _handler;

        public GetGroupMembersQueryHandlerTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _chatRepositoryMock = new Mock<IChatRepository>();
            _chatMemberRepositoryMock = new Mock<IChatMemberRepository>();
            _loggerMock = new Mock<ILogger<GetGroupMembersQueryHandler>>();

            _handler = new GetGroupMembersQueryHandler(
                _userRepositoryMock.Object,
                _chatRepositoryMock.Object,
                _chatMemberRepositoryMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidQuery_ReturnsMembers()
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
            var member1 = new ChatMember
            {
                UserId = userId,
                ChatId = chatId,
                Role = ChatRole.Creator,
                CreatedAtUtc = DateTime.UtcNow,
                User = user
            };
            var member2 = new ChatMember
            {
                UserId = Guid.NewGuid(),
                ChatId = chatId,
                Role = ChatRole.Member,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                User = new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = "Jane",
                    Lastname = "Doe",
                    Status = UserStatus.Online,
                    IsDeleted = false,
                    Avatar = "avatar.jpg",
                    LastSeenAt = DateTime.UtcNow
                }
            };
            var members = new List<ChatMember> { member1, member2 };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member1);
            _chatMemberRepositoryMock.Setup(r => r.GetByChatIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(members);

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 0,
                Limit = 50
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Members.Should().HaveCount(2);
            result.Value.TotalCount.Should().Be(2);
            result.Value.Offset.Should().Be(0);
            result.Value.Limit.Should().Be(50);
            result.Value.HasNextPage.Should().BeFalse();
            result.Value.HasPreviousPage.Should().BeFalse();

            var firstMember = result.Value.Members[0];
            firstMember.Role.Should().Be(ChatRole.Creator);
            var secondMember = result.Value.Members[1];
            secondMember.Firstname.Should().Be("Jane");
            secondMember.Lastname.Should().Be("Doe");
            secondMember.IsOnline.Should().BeTrue();
            secondMember.AvatarUrl.Should().Be("avatar.jpg");
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 50
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
            _chatMemberRepositoryMock.Verify(r => r.GetByChatIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 50
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
            _chatMemberRepositoryMock.Verify(r => r.GetByChatIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 50
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
            _chatMemberRepositoryMock.Verify(r => r.GetByChatIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 50
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(query.ChatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Chat)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Group not found");

            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByChatIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_NonGroupChat_ReturnsFailure()
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
                IsDeleted = false
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 0,
                Limit = 50
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Chat is not a group");

            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByChatIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DeletedGroup_ReturnsFailure()
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

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 0,
                Limit = 50
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "Group is deleted");

            _chatMemberRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatMemberRepositoryMock.Verify(r => r.GetByChatIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChatMember)null);

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 0,
                Limit = 50
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "You are not a member of this group");

            _chatMemberRepositoryMock.Verify(r => r.GetByChatIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WithSearchTerm_FiltersMembers()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Firstname = "Admin",
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = false
            };
            var member1 = new ChatMember
            {
                UserId = userId,
                ChatId = chatId,
                Role = ChatRole.Creator,
                CreatedAtUtc = DateTime.UtcNow,
                User = user
            };
            var member2 = new ChatMember
            {
                UserId = Guid.NewGuid(),
                ChatId = chatId,
                Role = ChatRole.Member,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                User = new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = "Jane",
                    Lastname = "Doe",
                    IsDeleted = false
                }
            };
            var members = new List<ChatMember> { member1, member2 };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member1);
            _chatMemberRepositoryMock.Setup(r => r.GetByChatIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(members);

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 0,
                Limit = 50,
                SearchTerm = "Jane"
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Members.Should().HaveCount(1);
            result.Value.Members.First().Firstname.Should().Be("Jane");
            result.Value.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task Handle_WithRoleFilter_FiltersMembers()
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
            var member1 = new ChatMember
            {
                UserId = userId,
                ChatId = chatId,
                Role = ChatRole.Creator,
                CreatedAtUtc = DateTime.UtcNow,
                User = user
            };
            var member2 = new ChatMember
            {
                UserId = Guid.NewGuid(),
                ChatId = chatId,
                Role = ChatRole.Member,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                User = new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = "Jane",
                    Lastname = "Doe",
                    IsDeleted = false
                }
            };
            var members = new List<ChatMember> { member1, member2 };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member1);
            _chatMemberRepositoryMock.Setup(r => r.GetByChatIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(members);

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 0,
                Limit = 50,
                RoleFilter = ChatRole.Creator
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Members.Should().HaveCount(1);
            result.Value.Members[0].Role.Should().Be(ChatRole.Creator);
            result.Value.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task Handle_IncludeDeleted_ReturnsDeletedMembers()
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
            var member1 = new ChatMember
            {
                UserId = userId,
                ChatId = chatId,
                Role = ChatRole.Creator,
                CreatedAtUtc = DateTime.UtcNow,
                User = user
            };
            var member2 = new ChatMember
            {
                UserId = Guid.NewGuid(),
                ChatId = chatId,
                Role = ChatRole.Member,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                User = new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = "Jane",
                    Lastname = "Doe",
                    IsDeleted = true
                }
            };
            var members = new List<ChatMember> { member1, member2 };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member1);
            _chatMemberRepositoryMock.Setup(r => r.GetByChatIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(members);

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 0,
                Limit = 50,
                IncludeDeleted = true
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Members.Should().HaveCount(2);
            result.Value.Members.Should().Contain(m => m.IsDeleted);
            result.Value.TotalCount.Should().Be(2);
        }

        [Fact]
        public async Task Handle_Pagination_ReturnsCorrectSubset()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Firstname = "Admin",
                IsVerified = true,
                IsDeleted = false
            };
            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Group,
                IsDeleted = false
            };
            var members = new List<ChatMember>
            {
                new ChatMember
                {
                    UserId = userId,
                    ChatId = chatId,
                    Role = ChatRole.Creator,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
                    User = user
                },
                new ChatMember
                {
                    UserId = Guid.NewGuid(),
                    ChatId = chatId,
                    Role = ChatRole.Member,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                    User = new User { Id = Guid.NewGuid(), Firstname = "Jane", IsDeleted = false }
                },
                new ChatMember
                {
                    UserId = Guid.NewGuid(),
                    ChatId = chatId,
                    Role = ChatRole.Member,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                    User = new User { Id = Guid.NewGuid(), Firstname = "John", IsDeleted = false }
                },
                new ChatMember
                {
                    UserId = Guid.NewGuid(),
                    ChatId = chatId,
                    Role = ChatRole.Member,
                    CreatedAtUtc = DateTime.UtcNow,
                    User = new User { Id = Guid.NewGuid(), Firstname = "Bob", IsDeleted = false }
                }
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(members[0]);
            _chatMemberRepositoryMock.Setup(r => r.GetByChatIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(members);

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 1,
                Limit = 1
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Members.Should().HaveCount(1);
            result.Value.TotalCount.Should().Be(4);
            result.Value.Offset.Should().Be(1);
            result.Value.Limit.Should().Be(1);
            result.Value.HasNextPage.Should().BeTrue();
            result.Value.HasPreviousPage.Should().BeTrue();
            result.Value.Members.First().Firstname.Should().Be("Jane");
        }

        [Fact]
        public async Task Handle_DatabaseError_ReturnsFailure()
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
                UserId = userId,
                ChatId = chatId,
                Role = ChatRole.Creator
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _chatRepositoryMock.Setup(r => r.GetByIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chat);
            _chatMemberRepositoryMock.Setup(r => r.GetByIdsAsync(chatId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);
            _chatMemberRepositoryMock.Setup(r => r.GetByChatIdAsync(chatId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            var query = new GetGroupMembersQuery
            {
                UserId = userId,
                ChatId = chatId,
                Offset = 0,
                Limit = 50
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "An error occurred while retrieving group members");

            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.AtLeastOnce);
        }
    }
}
