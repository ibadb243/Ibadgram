using Application.CQRS.Users.Commands.UpdateShortname;
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
    public class UpdateShortnameCommandValidatorTests
    {
        private Mock<IUserRepository> _userRepositoryMock;
        private Mock<IMentionRepository> _mentionRepositoryMock;
        private UpdateShortnameCommandValidator _validator;

        public UpdateShortnameCommandValidatorTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _mentionRepositoryMock = new Mock<IMentionRepository>();
            _validator = new UpdateShortnameCommandValidator(_userRepositoryMock.Object, _mentionRepositoryMock.Object);
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = "newshortname"
            };

            _mentionRepositoryMock.Setup(m => m.ExistsByShortnameAsync(command.Shortname, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

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
                Shortname = "newshortname"
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
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.NewGuid(),
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
        public async Task Validate_NullShortname_Fails()
        {
            // Arrange
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.NewGuid(),
                Shortname = null
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Shortname" && e.ErrorMessage == "Shortname is required");
        }

        [Fact]
        public async Task Validate_AllFieldsInvalid_Fails()
        {
            // Arrange
            var command = new UpdateShortnameCommand
            {
                UserId = Guid.Empty,
                Shortname = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "Shortname");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
        }
    }
}
