using Application.CQRS.Users.Commands.UpdateUser;
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
    public class UpdateUserCommandValidatorTests
    {
        private Mock<IUserRepository> _userRepositoryMock;
        private UpdateUserCommandValidator _validator;

        public UpdateUserCommandValidatorTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _validator = new UpdateUserCommandValidator(_userRepositoryMock.Object);
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Firstname = "John",
                Lastname = "Doe",
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
            var command = new UpdateUserCommand
            {
                UserId = Guid.Empty,
                Firstname = "John"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId" && e.ErrorMessage == "UserId is required");
        }

        [Fact]
        public async Task Validate_ShortFirstname_Fails()
        {
            // Arrange
            var shortFirstname = new string('a', UserConstants.FirstnameMinLength - 1);
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Firstname = shortFirstname
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Firstname" &&
                e.ErrorMessage == $"Firstname's length should have minimum {UserConstants.FirstnameMinLength} characters");
        }

        [Fact]
        public async Task Validate_LongFirstname_Fails()
        {
            // Arrange
            var longFirstname = new string('a', UserConstants.FirstnameMaxLength + 1);
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Firstname = longFirstname
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Firstname" &&
                e.ErrorMessage == $"Firstname's length cann't have characters greater than {UserConstants.FirstnameMaxLength}");
        }

        [Fact]
        public async Task Validate_LongLastname_Fails()
        {
            // Arrange
            var longLastname = new string('a', UserConstants.LastnameLength + 1);
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Lastname = longLastname
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Lastname" &&
                e.ErrorMessage == $"Lastname's length cann't have characters greater than {UserConstants.LastnameLength}");
        }

        [Fact]
        public async Task Validate_LongBio_Fails()
        {
            // Arrange
            var longBio = new string('a', UserConstants.BioLength + 1);
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
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
        public async Task Validate_AllPropertiesNull_Fails()
        {
            // Arrange
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Firstname = null,
                Lastname = null,
                Bio = null
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.ErrorMessage == "There aren't any parameters");
        }

        [Fact]
        public async Task Validate_NullFirstname_Passes()
        {
            // Arrange
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Firstname = null,
                Lastname = "Doe",
                Bio = null
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_NullLastname_Passes()
        {
            // Arrange
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Firstname = "John",
                Lastname = null,
                Bio = null
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_NullBio_Passes()
        {
            // Arrange
            var command = new UpdateUserCommand
            {
                UserId = Guid.NewGuid(),
                Firstname = "John",
                Lastname = null,
                Bio = null
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
            var command = new UpdateUserCommand
            {
                UserId = Guid.Empty,
                Firstname = new string('a', UserConstants.FirstnameMaxLength + 1),
                Lastname = new string('a', UserConstants.LastnameLength + 1),
                Bio = new string('a', UserConstants.BioLength + 1)
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "UserId");
            result.Errors.Should().Contain(e => e.PropertyName == "Firstname");
            result.Errors.Should().Contain(e => e.PropertyName == "Lastname");
            result.Errors.Should().Contain(e => e.PropertyName == "Bio");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(4);
        }
    }
}
