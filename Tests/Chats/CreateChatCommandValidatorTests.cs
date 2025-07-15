using Application.CQRS.Chats.Commands.CreateChat;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class CreateChatCommandValidatorTests
    {
        private CreateChatCommandValidator _validator;

        public CreateChatCommandValidatorTests()
        {
            _validator = new CreateChatCommandValidator();
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new CreateChatCommand
            {
                FirstUserId = Guid.NewGuid(),
                SecondUserId = Guid.NewGuid()
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_EmptyFirstUserId_Fails()
        {
            // Arrange
            var command = new CreateChatCommand
            {
                FirstUserId = Guid.Empty,
                SecondUserId = Guid.NewGuid()
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "FirstUserId" && e.ErrorMessage == "FirstUserId is required");
        }

        [Fact]
        public async Task Validate_EmptySecondUserId_Fails()
        {
            // Arrange
            var command = new CreateChatCommand
            {
                FirstUserId = Guid.NewGuid(),
                SecondUserId = Guid.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "SecondUserId" && e.ErrorMessage == "SecondUserId is required");
        }

        [Fact]
        public async Task Validate_SameUserIds_Fails()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var command = new CreateChatCommand
            {
                FirstUserId = userId,
                SecondUserId = userId
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.ErrorMessage == "FirstUserId and SecondUserId shouldn't be equal");
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var command = new CreateChatCommand
            {
                FirstUserId = Guid.Empty,
                SecondUserId = Guid.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "FirstUserId");
            result.Errors.Should().Contain(e => e.PropertyName == "SecondUserId");
            result.Errors.Should().Contain(e => e.ErrorMessage == "FirstUserId and SecondUserId shouldn't be equal");
            result.Errors.Should().HaveCount(3);
        }
    }
}
