using Application.CQRS.Users.Commands.CreateAccount;
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

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_EmptyFirstname_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "",
                Email = "john@gmail.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Firstname" && e.ErrorMessage == "Firstname is required");
        }

        [Fact]
        public async Task Validate_ShortFirstname_Fails()
        {
            // Arrange
            var shortFirstname = new string('a', UserConstants.FirstnameMinLength - 1);
            var command = new CreateAccountCommand
            {
                Firstname = shortFirstname,
                Email = "john@gmail.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Firstname" &&
                e.ErrorMessage == $"Firstname's length should have minimum {UserConstants.FirstnameMinLength} characters");
        }

        [Fact]
        public async Task Validate_LongFirstname_Fails()
        {
            // Arrange
            var longFirstname = new string('a', UserConstants.FirstnameMaxLength + 1);
            var command = new CreateAccountCommand
            {
                Firstname = longFirstname,
                Email = "john@gmail.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Firstname" &&
                e.ErrorMessage == $"Firstname's length cann't have characters greater than {UserConstants.FirstnameMaxLength}");
        }

        [Fact]
        public async Task Validate_LongLastname_Fails()
        {
            // Arrange
            var longLastname = new string('a', UserConstants.LastnameLength + 1);
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Lastname = longLastname,
                Email = "john@gmail.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Lastname" &&
                e.ErrorMessage == $"Lastname's length cann't have characters greater than {UserConstants.LastnameLength}");
        }

        [Fact]
        public async Task Validate_EmptyEmail_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
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
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Email = "invalid-email-format",
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
            var command = new CreateAccountCommand
            {
                Firstname = "John",
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
            var command = new CreateAccountCommand
            {
                Firstname = "John",
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
            var command = new CreateAccountCommand
            {
                Firstname = "John",
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
            var command = new CreateAccountCommand
            {
                Firstname = "John",
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
            var command = new CreateAccountCommand
            {
                Firstname = "John",
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
        public async Task Validate_NullLastname_Passes()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Lastname = null,
                Email = "john@gmail.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_EmptyLastname_Passes()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Lastname = "",
                Email = "john@gmail.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_AllFieldsEmpty_Fails()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "",
                Email = "",
                Password = ""
            };

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
        public async Task Validate_ValidCommandWithLastname_Passes()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "John",
                Lastname = "Doe",
                Email = "john.doe@gmail.com",
                Password = "Password123"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        // Note: The commented-out email uniqueness validation test
        // This test would be relevant if you uncomment the BeUniqueEmail rule in the validator
        //[Fact]
        //public async Task Validate_DuplicateEmail_Fails()
        //{
        //    // Arrange
        //    var command = new CreateAccountCommand
        //    {
        //        Firstname = "John",
        //        Email = "john@gmail.com",
        //        Password = "Password123"
        //    };
        //    _userRepositoryMock.Setup(r => r.EmailExistsAsync(command.Email, It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(true);

        //    // Act
        //    var result = await _validator.ValidateAsync(command);

        //    // Assert
        //    result.IsValid.Should().BeFalse();
        //    result.Errors.Should().Contain(e => e.ErrorMessage == "Email has already been registered with an account");
        //}
    }
}
