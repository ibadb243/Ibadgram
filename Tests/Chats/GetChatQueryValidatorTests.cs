using Application.CQRS.Chats.Queries;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class GetChatQueryValidatorTests
    {
        private GetChatQueryValidator _validator;

        public GetChatQueryValidatorTests()
        {
            _validator = new GetChatQueryValidator();
        }

        [Fact]
        public async Task Validate_ValidQuery_Passes()
        {
            // Arrange
            var query = new GetChatQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid()
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
            var query = new GetChatQuery
            {
                UserId = Guid.Empty,
                ChatId = Guid.NewGuid()
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
            var query = new GetChatQuery
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "ChatId" && e.ErrorMessage == "ChatId is required");
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var query = new GetChatQuery
            {
                UserId = Guid.Empty,
                ChatId = Guid.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(query);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "ChatId");
            result.Errors.Should().HaveCount(2);
        }
    }
}
