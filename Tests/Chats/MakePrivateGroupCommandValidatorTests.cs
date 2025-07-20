using Application.CQRS.Chats.Commands.MakePrivateGroup;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class MakePrivateGroupCommandValidatorTests
    {
        private MakePrivateGroupCommandValidator _validator;

        public MakePrivateGroupCommandValidatorTests()
        {
            _validator = new MakePrivateGroupCommandValidator();
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new MakePrivateGroupCommand
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
            var command = new MakePrivateGroupCommand
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
            var command = new MakePrivateGroupCommand
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
            var command = new MakePrivateGroupCommand
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
