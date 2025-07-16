using Application.CQRS.Chats.Commands.CreateGroup;
using Domain.Common.Constants;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class CreateGroupCommandValidatorTests
    {
        private CreateGroupCommandValidator _validator;

        public CreateGroupCommandValidatorTests()
        {
            _validator = new CreateGroupCommandValidator();
        }

        [Fact]
        public async Task Validate_ValidPublicGroupCommand_Passes()
        {
            // Arrange
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = "Test Group",
                Description = "Test Description",
                IsPrivate = false,
                Shortname = "testgroup"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_ValidPrivateGroupCommand_Passes()
        {
            // Arrange
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = "Test Group",
                Description = "Test Description",
                IsPrivate = true,
                Shortname = null
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
            var command = new CreateGroupCommand
            {
                UserId = Guid.Empty,
                Name = "Test Group",
                IsPrivate = false,
                Shortname = "testgroup"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId" && e.ErrorMessage == "UserId is required");
        }

        [Fact]
        public async Task Validate_EmptyName_Fails()
        {
            // Arrange
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = "",
                IsPrivate = false,
                Shortname = "testgroup"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage == "Name is required");
        }

        [Fact]
        public async Task Validate_ShortName_Fails()
        {
            // Arrange
            var shortName = new string('a', ChatConstants.NameMinLength - 1);
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = shortName,
                IsPrivate = false,
                Shortname = "testgroup"
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
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = longName,
                IsPrivate = false,
                Shortname = "testgroup"
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
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = "Test Group",
                Description = longDescription,
                IsPrivate = false,
                Shortname = "testgroup"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Description" &&
                e.ErrorMessage == $"Description's length cann't have characters greater than {UserConstants.BioLength}");
        }

        [Fact]
        public async Task Validate_PublicGroupEmptyShortname_Fails()
        {
            // Arrange
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = "Test Group",
                IsPrivate = false,
                Shortname = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Shortname" && e.ErrorMessage == "Shortname is required");
        }

        [Fact]
        public async Task Validate_PublicGroupShortShortname_Fails()
        {
            // Arrange
            var shortShortname = new string('a', ShortnameConstants.MinLength - 1);
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = "Test Group",
                IsPrivate = false,
                Shortname = shortShortname
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Shortname" &&
                e.ErrorMessage == $"Shortname's length should have minimum {ShortnameConstants.MinLength} characters");
        }

        [Fact]
        public async Task Validate_PublicGroupLongShortname_Fails()
        {
            // Arrange
            var longShortname = new string('a', ShortnameConstants.MaxLength + 1);
            var command = new CreateGroupCommand
            {
                UserId = Guid.NewGuid(),
                Name = "Test Group",
                IsPrivate = false,
                Shortname = longShortname
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Shortname" &&
                e.ErrorMessage == $"Shortname's length cann't have characters greater than {ShortnameConstants.MaxLength}");
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var command = new CreateGroupCommand
            {
                UserId = Guid.Empty,
                Name = "",
                Description = new string('a', ChatConstants.DescriptionLength + 1),
                IsPrivate = false,
                Shortname = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "Name");
            result.Errors.Should().Contain(e => e.PropertyName == "Description");
            result.Errors.Should().Contain(e => e.PropertyName == "Shortname");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(4);
        }
    }
}
