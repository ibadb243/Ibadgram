using Application.CQRS.Chats.Commands.UpdateGroup;
using Domain.Common.Constants;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class UpdateGroupCommandValidatorTests
    {
        private UpdateGroupCommandValidator _validator;

        public UpdateGroupCommandValidatorTests()
        {
            _validator = new UpdateGroupCommandValidator();
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new UpdateGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Name = "Test Group",
                Description = "Test Description"
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
            var command = new UpdateGroupCommand
            {
                UserId = Guid.Empty,
                GroupId = Guid.NewGuid(),
                Name = "Test Group"
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
            var command = new UpdateGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.Empty,
                Name = "Test Group"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "GroupId" && e.ErrorMessage == "GroupId is required");
        }

        [Fact]
        public async Task Validate_ShortName_Fails()
        {
            // Arrange
            var shortName = new string('a', ChatConstants.NameMinLength - 1);
            var command = new UpdateGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Name = shortName
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" &&
                e.ErrorMessage == $"Name's length should have minimum {ChatConstants.NameMinLength} characters");
        }

        [Fact]
        public async Task Validate_LongName_Fails()
        {
            // Arrange
            var longName = new string('a', ChatConstants.NameMaxLength + 1);
            var command = new UpdateGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Name = longName
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" &&
                e.ErrorMessage == $"Name's length cann't have characters greater than {ChatConstants.NameMaxLength}");
        }

        [Fact]
        public async Task Validate_LongDescription_Fails()
        {
            // Arrange
            var longDescription = new string('a', ChatConstants.DescriptionLength + 1);
            var command = new UpdateGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Name = "Test Group",
                Description = longDescription
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Description" &&
                e.ErrorMessage == $"Description's length cann't have characters greater than {UserConstants.BioLength}");
        }

        [Fact]
        public async Task Validate_AllPropertiesNull_Fails()
        {
            // Arrange
            var command = new UpdateGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Name = null,
                Description = null
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.ErrorMessage == "There aren't any parameters");
        }

        [Fact]
        public async Task Validate_NullNameWithValidDescription_Passes()
        {
            // Arrange
            var command = new UpdateGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Name = null,
                Description = "Test Description"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_ValidNameWithNullDescription_Passes()
        {
            // Arrange
            var command = new UpdateGroupCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Name = "Test Group",
                Description = null
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var command = new UpdateGroupCommand
            {
                UserId = Guid.Empty,
                GroupId = Guid.Empty,
                Name = "",
                Description = new string('a', ChatConstants.DescriptionLength + 1)
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "GroupId");
            result.Errors.Should().Contain(e => e.PropertyName == "Name");
            result.Errors.Should().Contain(e => e.PropertyName == "Description");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(4);
        }
    }
}
