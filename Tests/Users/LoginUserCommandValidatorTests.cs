using Application.CQRS.Users.Commands.Login;
using Application.Interfaces.Repositories;
using Castle.Core.Logging;
using Domain.Common.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Users
{
    public class LoginUserCommandValidatorTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<ILogger<LoginUserCommandValidator>> _loggerMock;
        private readonly LoginUserCommandValidator _validator;

        public LoginUserCommandValidatorTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _validator = new LoginUserCommandValidator(_userRepositoryMock.Object);
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = "Password123"
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
            var command = new LoginUserCommand
            {
                Email = "",
                Password = "Password123"
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
            var command = new LoginUserCommand
            {
                Email = "invalid-email",
                Password = "Password123"
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
            var command = new LoginUserCommand
            {
                Email = email,
                Password = "Password123"
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
            var command = new LoginUserCommand
            {
                Email = email,
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Email" &&
                e.ErrorMessage == "Allowed only Gmail, Yahoo, Yandex and Mail emails");
        }

        [Fact]
        public async Task Validate_EmptyPassword_Fails()
        {
            // Arrange
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage == "Password is required");
        }

        [Fact]
        public async Task Validate_ShortPassword_Fails()
        {
            // Arrange
            var shortPassword = new string('a', UserConstants.PasswordMinLength - 1);
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = shortPassword
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Password" &&
                e.ErrorMessage == $"Password's length should have minimum {UserConstants.PasswordMinLength} characters");
        }

        [Fact]
        public async Task Validate_LongPassword_Fails()
        {
            // Arrange
            var longPassword = new string('a', UserConstants.PasswordMaxLength + 1);
            var command = new LoginUserCommand
            {
                Email = "john@gmail.com",
                Password = longPassword
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Password" &&
                e.ErrorMessage == $"Password's length cann't have characters greater than {UserConstants.PasswordMaxLength}");
        }

        [Fact]
        public async Task Validate_AllFieldsEmpty_Fails()
        {
            // Arrange
            var command = new LoginUserCommand
            {
                Email = "",
                Password = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Email");
            result.Errors.Should().Contain(e => e.PropertyName == "Password");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
        }
    }
}
