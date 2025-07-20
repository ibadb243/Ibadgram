using Application.CQRS.Chats.Commands.UpdateShortname;
using Domain.Common.Constants;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Chats
{
    public class UpdateShortnameCommandValidatorTests
    {
        private UpdateShortnameCommandValidator _validator;

        public UpdateShortnameCommandValidatorTests()
        {
            _validator = new UpdateShortnameCommandValidator();
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Shortname = "newshortname"
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
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.Empty,
                GroupId = Guid.NewGuid(),
                Shortname = "newshortname"
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
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.Empty,
                Shortname = "newshortname"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "GroupId" && e.ErrorMessage == "GroupId is required");
        }

        [Fact]
        public async Task Validate_EmptyShortname_Fails()
        {
            // Arrange
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Shortname = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Shortname" && e.ErrorMessage == "Shortname is required");
        }

        [Fact]
        public async Task Validate_ShortShortname_Fails()
        {
            // Arrange
            var shortShortname = new string('a', ShortnameConstants.MinLength - 1);
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
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
        public async Task Validate_LongShortname_Fails()
        {
            // Arrange
            var longShortname = new string('a', ShortnameConstants.MaxLength + 1);
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
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
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.Empty,
                GroupId = Guid.Empty,
                Shortname = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "GroupId");
            result.Errors.Should().Contain(e => e.PropertyName == "Shortname");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
        }
    }
}
