using Application.CQRS.Users.Commands.UpdateConfirmEmailToken;
using Application.Interfaces.Repositories;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Users
{
    public class UpdateConfirmEmailTokenCommandValidatorTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly UpdateConfirmEmailTokenCommandValidator _validator;

        public UpdateConfirmEmailTokenCommandValidatorTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _validator = new UpdateConfirmEmailTokenCommandValidator(_userRepositoryMock.Object);
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new UpdateConfirmEmailTokenCommand
            {
                Email = "john@gmail.com"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_EmptyEmail_Fails()
        {
            // Arrange
            var command = new UpdateConfirmEmailTokenCommand
            {
                Email = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorMessage == "Email is required");
        }

        [Fact]
        public async Task Validate_InvalidEmailFormat_Fails()
        {
            // Arrange
            var command = new UpdateConfirmEmailTokenCommand
            {
                Email = "invalid-email"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorMessage == "Email address doesn't correct");
        }

        [Theory]
        [InlineData("john@gmail.com")]
        [InlineData("john@yahoo.com")]
        [InlineData("john@yandex.ru")]
        [InlineData("john@mail.ru")]
        public async Task Validate_AllowedEmailDomains_Passes(string email)
        {
            // Arrange
            var command = new UpdateConfirmEmailTokenCommand
            {
                Email = email
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("john@outlook.com")]
        [InlineData("john@icloud.com")]
        [InlineData("john@hotmail.com")]
        [InlineData("john@example.com")]
        public async Task Validate_DisallowedEmailDomains_Fails(string email)
        {
            // Arrange
            var command = new UpdateConfirmEmailTokenCommand
            {
                Email = email
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Email" &&
                e.ErrorMessage == "Allowed only Gmail, Yahoo, Yandex and Mail emails");
        }
    }
}
