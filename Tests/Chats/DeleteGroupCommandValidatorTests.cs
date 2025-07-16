using Application.CQRS.Chats.Commands.DeleteGroup;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class DeleteGroupCommandValidatorTests
    {
        private DeleteGroupCommandValidator _validator;

        public DeleteGroupCommandValidatorTests()
        {
            _validator = new DeleteGroupCommandValidator();
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new DeleteGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid()
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
            var command = new DeleteGroupCommand
            {
                UserId = Guid.Empty,
                GroupId = Guid.NewGuid()
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId" && e.ErrorMessage == "UserId is required");
        }

        [Fact]
        public async Task Validate_EmptyGroupId_Fails()
        {
            // Arrange
            var command = new DeleteGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "GroupId" && e.ErrorMessage == "GroupId is required");
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var command = new DeleteGroupCommand
            {
                UserId = Guid.Empty,
                GroupId = Guid.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "GroupId");
            result.Errors.Should().HaveCount(2);
        }
    }
}
