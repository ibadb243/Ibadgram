using Application.CQRS.Messages.Commands.SendMessage;
using Domain.Common.Constants;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Messages
{
    public class SendMessageCommandValidatorTests
    {
        private SendMessageCommandValidator _validator;

        public SendMessageCommandValidatorTests()
        {
            _validator = new SendMessageCommandValidator();
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new SendMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Message = "Hello, world!"
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
            var command = new SendMessageCommand
            {
                UserId = Guid.Empty,
                ChatId = Guid.NewGuid(),
                Message = "Hello, world!"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId" && e.ErrorMessage == "UserId is required");
        }

        [Fact]
        public async Task Validate_EmptyChatId_Fails()
        {
            // Arrange
            var command = new SendMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.Empty,
                Message = "Hello, world!"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "ChatId" && e.ErrorMessage == "ChatId is required");
        }

        [Fact]
        public async Task Validate_EmptyMessage_Fails()
        {
            // Arrange
            var command = new SendMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Message = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Message" && e.ErrorMessage == "Message content is required");
        }

        [Fact]
        public async Task Validate_ShortMessage_Fails()
        {
            // Arrange
            var shortMessage = new string('a', MessageConstants.MinLength - 1);
            var command = new SendMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Message = shortMessage
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Message" &&
                e.ErrorMessage == $"Message must be at least {MessageConstants.MinLength} characters");
        }

        [Fact]
        public async Task Validate_LongMessage_Fails()
        {
            // Arrange
            var longMessage = new string('a', MessageConstants.MaxLength + 1);
            var command = new SendMessageCommand
            {
                UserId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                Message = longMessage
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Message" &&
                e.ErrorMessage == $"Message cannot exceed {MessageConstants.MaxLength} characters");
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var command = new SendMessageCommand
            {
                UserId = Guid.Empty,
                ChatId = Guid.Empty,
                Message = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "ChatId");
            result.Errors.Should().Contain(e => e.PropertyName == "Message");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
        }
    }
}
