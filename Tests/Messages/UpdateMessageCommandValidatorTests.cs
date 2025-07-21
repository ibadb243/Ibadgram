using Application.CQRS.Messages.Commands.UpdateMessage;
using Domain.Common.Constants;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Messages
{
    public class UpdateMessageCommandValidatorTests
    {
        private UpdateMessageCommandValidator _validator;

        public UpdateMessageCommandValidatorTests()
        {
            _validator = new UpdateMessageCommandValidator();
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new UpdateMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = "Updated message"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_EmptyUserId_Fails()
        {
            // Arrange
            var command = new UpdateMessageCommand
            {
                UserId = Guid.Empty,
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = "Updated message"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId" && e.ErrorMessage == "User ID is required");
        }

        [Fact]
        public async Task Validate_EmptyChatId_Fails()
        {
            // Arrange
            var command = new UpdateMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.Empty,
                MessageId = 1L,
                Text = "Updated message"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "ChatId" && e.ErrorMessage == "Chat ID is required");
        }

        [Fact]
        public async Task Validate_ZeroMessageId_Fails()
        {
            // Arrange
            var command = new UpdateMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                MessageId = 0L,
                Text = "Updated message"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "MessageId" && e.ErrorMessage == "Message ID must be greater than 0");
        }

        [Fact]
        public async Task Validate_EmptyText_Fails()
        {
            // Arrange
            var command = new UpdateMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Text" && e.ErrorMessage == "Message content cannot be empty");
        }

        [Fact]
        public async Task Validate_ShortText_Fails()
        {
            // Arrange
            var shortText = new string('a', MessageConstants.MinLength - 1);
            var command = new UpdateMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = shortText
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Text" &&
                e.ErrorMessage == $"Message must be at least {MessageConstants.MinLength} characters long");
        }

        [Fact]
        public async Task Validate_LongText_Fails()
        {
            // Arrange
            var longText = new string('a', MessageConstants.MaxLength + 1);
            var command = new UpdateMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                MessageId = 1L,
                Text = longText
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Text" &&
                e.ErrorMessage == $"Message cannot exceed {MessageConstants.MaxLength} characters");
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var command = new UpdateMessageCommand
            {
                UserId = Guid.Empty,
                ChatId = Guid.Empty,
                MessageId = 0L,
                Text = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "ChatId");
            result.Errors.Should().Contain(e => e.PropertyName == "MessageId");
            result.Errors.Should().Contain(e => e.PropertyName == "Text");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(4);
        }
    }
}
