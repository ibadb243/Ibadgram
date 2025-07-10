using Application.CQRS.Users.Commands.CreateAccount;
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
    public class CreateAccountCommandValidatorTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly CreateAccountCommandValidator _validator;

        public CreateAccountCommandValidatorTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _validator = new CreateAccountCommandValidator(_userRepositoryMock.Object);
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Email = "john@gmail.com",
                Password = "Password123"
            };
            _userRepositoryMock.Setup(r => r.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_DuplicateEmail_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Email = "john@gmail.com",
                Password = "Password123"
            };
            _userRepositoryMock.Setup(r => r.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage == "Email has already been registered with an account");
        }

        [Fact]
        public async Task Validate_InvalidEmail_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Email = "gmailjohnasdash",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage == "A valid email address is required");
        }

        [Fact]
        public async Task Validate_WrongEmailPattern_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Email = "john@icloud.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage == "Allowed only Gmail, Yahoo, Yandex and Mail emails");
        }

        [Fact]
        public async Task Validate_EmptyCommand_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand();

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Firstname");
            result.Errors.Should().Contain(e => e.PropertyName == "Email");
            result.Errors.Should().Contain(e => e.PropertyName == "Password");
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
        }

        [Fact]
        public async Task Validate_EmptyFirstname_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Email = "john@gmail.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Firstname");
            result.Errors.Should().Contain(e => e.ErrorMessage == "'Firstname' должно быть заполнено.");
        }

        [Fact]
        public async Task Validate_ShortPassword_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Email = "john@gmail.com",
                Password = "123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Password");
            result.Errors.Should().Contain(e => e.ErrorMessage == $"'Password' должно быть длиной не менее 8 символов. Количество введенных символов: {command.Password.Length}.");
        }

        [Fact]
        public async Task Validate_LongPassword_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Email = "john@gmail.com",
                Password = "123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Password");
            result.Errors.Should().Contain(e => e.ErrorMessage == $"'Password' должно быть длиной не более 24 символов. Количество введенных символов: {command.Password.Length}.");
        }
    }
}
