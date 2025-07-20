using Application.CQRS.Chats.Queries.GetGroupMembers;
using Domain.Enums;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class GetGroupMembersQueryValidatorTests
    {
        private GetGroupMembersQueryValidator _validator;

        public GetGroupMembersQueryValidatorTests()
        {
            _validator = new GetGroupMembersQueryValidator();
        }

        [Fact]
        public async Task Validate_ValidQuery_Passes()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 50,
                SearchTerm = "test",
                RoleFilter = ChatRole.Member,
                IncludeDeleted = false
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_EmptyUserId_Fails()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.Empty,
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 50
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId" && e.ErrorMessage == "UserId is required");
        }

        [Fact]
        public async Task Validate_EmptyChatId_Fails()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.Empty,
                Offset = 0,
                Limit = 50
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "ChatId" && e.ErrorMessage == "ChatId is required");
        }

        [Fact]
        public async Task Validate_NegativeOffset_Fails()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Offset = -1,
                Limit = 50
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Offset" && e.ErrorMessage == "Offset must be non-negative");
        }

        [Fact]
        public async Task Validate_ZeroLimit_Fails()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 0
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Limit" && e.ErrorMessage == "Limit must be between 1 and 200");
        }

        [Fact]
        public async Task Validate_LimitExceedsMax_Fails()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 201
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Limit" && e.ErrorMessage == "Limit must be between 1 and 200");
        }

        [Fact]
        public async Task Validate_LongSearchTerm_Fails()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Offset = 0,
                Limit = 50,
                SearchTerm = new string('a', 101)
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "SearchTerm" && e.ErrorMessage == "Search term cannot exceed 100 characters");
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var query = new GetGroupMembersQuery
            {
                UserId = Guid.Empty,
                ChatId = Guid.Empty,
                Offset = -1,
                Limit = 0,
                SearchTerm = new string('a', 101)
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "ChatId");
            result.Errors.Should().Contain(e => e.PropertyName == "Offset");
            result.Errors.Should().Contain(e => e.PropertyName == "Limit");
            result.Errors.Should().Contain(e => e.PropertyName == "SearchTerm");
            result.Errors.Should().HaveCount(5);
        }
    }
}
