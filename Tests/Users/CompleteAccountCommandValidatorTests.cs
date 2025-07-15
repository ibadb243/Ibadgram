using Application.CQRS.Users.Commands.CompleteAccount;
using Application.Interfaces.Repositories;
using Domain.Common.Constants;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Users
{
    public class CompleteAccountCommandValidatorTests
    {
        private readonly Mock<IMentionRepository> _mentionRepositoryMock;
        private readonly CompleteAccountCommandValidator _validator;

        public CompleteAccountCommandValidatorTests()
        {
            _mentionRepositoryMock = new Mock<IMentionRepository>();
            _validator = new CompleteAccountCommandValidator(_mentionRepositoryMock.Object);
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new CompleteAccountCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = "testuser",
                Bio = "Test bio"
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
            var command = new CompleteAccountCommand
            {
                UserId = Guid.Empty,
                Shortname = "testuser",
                Bio = "Test bio"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId" && e.ErrorMessage == "UserId is required");
        }

        [Fact]
        public async Task Validate_EmptyShortname_Fails()
        {
            // Arrange
            var command = new CompleteAccountCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = "",
                Bio = "Test bio"
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
            var command = new CompleteAccountCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = shortShortname,
                Bio = "Test bio"
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
            var command = new CompleteAccountCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = longShortname,
                Bio = "Test bio"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Shortname" &&
                e.ErrorMessage == $"Shortname's length cann't have characters greater than {ShortnameConstants.MaxLength}");
        }

        [Fact]
        public async Task Validate_LongBio_Fails()
        {
            // Arrange
            var longBio = new string('a', UserConstants.BioLength + 1);
            var command = new CompleteAccountCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = "testuser",
                Bio = longBio
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Bio" &&
                e.ErrorMessage == $"Bio's length cann't have characters greater than {UserConstants.BioLength}");
        }

        [Fact]
        public async Task Validate_NullBio_Passes()
        {
            // Arrange
            var command = new CompleteAccountCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = "testuser",
                Bio = null
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_EmptyBio_Passes()
        {
            // Arrange
            var command = new CompleteAccountCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = "testuser",
                Bio = ""
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
            var command = new CompleteAccountCommand
            {
                UserId = Guid.Empty,
                Shortname = "",
                Bio = new string('a', UserConstants.BioLength + 1)
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "Shortname");
            result.Errors.Should().Contain(e => e.PropertyName == "Bio");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
        }
    }
}
