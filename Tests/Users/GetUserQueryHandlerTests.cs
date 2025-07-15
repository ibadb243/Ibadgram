using Application.CQRS.Users.Queries.Get;
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

namespace Tests.Users
{
    public class GetUserQueryHandlerTests
    {
        private Mock<IUserRepository> _userRepositoryMock;
        private Mock<ILogger<GetUserQueryHandler>> _loggerMock;
        private GetUserQueryHandler _handler;

        public GetUserQueryHandlerTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _loggerMock = new Mock<ILogger<GetUserQueryHandler>>();

            _handler = new GetUserQueryHandler(
                _userRepositoryMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ValidQuery_ReturnsUserVm()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Firstname = "John",
                Lastname = "Doe",
                Bio = "Test bio",
                Status = UserStatus.Online,
                LastSeenAt = DateTime.UtcNow,
                IsDeleted = false,
                Mention = new UserMention
                {
                    Shortname = "johndoe",
                    UserId = userId
                }
            };
            var command = new GetUserQuery
            {
                UserId = userId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Firstname.Should().Be(user.Firstname);
            result.Value.Lastname.Should().Be(user.Lastname);
            result.Value.Shortname.Should().Be(user.Mention.Shortname);
            result.Value.Bio.Should().Be(user.Bio);
            result.Value.Status.Should().Be(user.Status);
            result.Value.LastSeenAt.Should().Be(user.LastSeenAt);
            result.Value.IsDeleted.Should().BeNull();

            _userRepositoryMock.Verify(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var command = new GetUserQuery
            {
                UserId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(command.UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Message == "User not found");

            _userRepositoryMock.Verify(r => r.GetByIdAsync(command.UserId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_DeletedUser_ReturnsDeletedUserVm()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                IsDeleted = true
            };
            var command = new GetUserQuery
            {
                UserId = userId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.IsDeleted.Should().BeTrue();
            result.Value.Firstname.Should().BeNull();
            result.Value.Lastname.Should().BeNull();
            result.Value.Shortname.Should().BeNull();
            result.Value.Bio.Should().BeNull();
            result.Value.Status.Should().Be(UserStatus.Offline);
            result.Value.LastSeenAt.Should().BeNull();

            _userRepositoryMock.Verify(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_DatabaseError_ThrowsException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var command = new GetUserQuery
            {
                UserId = userId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));

            _userRepositoryMock.Verify(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Handle_ValidQueryWithPartialData_ReturnsCorrectUserVm()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Firstname = "John",
                Lastname = null,
                Bio = null,
                Status = UserStatus.Offline,
                LastSeenAt = null,
                IsDeleted = false,
                Mention = new UserMention
                {
                    Shortname = "johndoe",
                    UserId = userId
                }
            };
            var command = new GetUserQuery
            {
                UserId = userId
            };

            _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Firstname.Should().Be(user.Firstname);
            result.Value.Lastname.Should().BeNull();
            result.Value.Shortname.Should().Be(user.Mention.Shortname);
            result.Value.Bio.Should().BeNull();
            result.Value.Status.Should().Be(user.Status);
            result.Value.LastSeenAt.Should().BeNull();
            result.Value.IsDeleted.Should().BeNull();

            _userRepositoryMock.Verify(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
