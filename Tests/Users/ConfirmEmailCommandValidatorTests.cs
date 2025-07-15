using Application.CQRS.Users.Commands.ConfirmEmail;
using Application.Interfaces.Repositories;
using FluentValidation.TestHelper;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Users
{
    public class ConfirmEmailCommandValidatorTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly ConfirmEmailCommandValidator _validator;

        public ConfirmEmailCommandValidatorTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _validator = new ConfirmEmailCommandValidator(_userRepositoryMock.Object);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Validate_EmptyEmail_ShouldHaveValidationError(string email)
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = email,
                Code = "123456"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("Email is required");
        }

        [Theory]
        [InlineData("invalid-email")]
        [InlineData("test@")]
        [InlineData("@gmail.com")]
        [InlineData("test.gmail.com")]
        public async Task Validate_InvalidEmailFormat_ShouldHaveValidationError(string email)
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = email,
                Code = "123456"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("Email address doesn't correct");
        }

        [Theory]
        [InlineData("test@outlook.com")]
        [InlineData("test@hotmail.com")]
        [InlineData("test@example.com")]
        [InlineData("test@domain.org")]
        public async Task Validate_DisallowedEmailDomain_ShouldHaveValidationError(string email)
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = email,
                Code = "123456"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails");
        }

        [Theory]
        [InlineData("test@gmail.com")]
        [InlineData("user@yahoo.com")]
        [InlineData("person@yandex.ru")]
        [InlineData("someone@mail.ru")]
        [InlineData("test.user@gmail.com")]
        [InlineData("test+tag@gmail.com")]
        public async Task Validate_AllowedEmailDomains_ShouldNotHaveValidationError(string email)
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = email,
                Code = "123456"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Validate_EmptyCode_ShouldHaveValidationError(string code)
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = code
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Code)
                .WithErrorMessage("Code is required");
        }

        [Fact]
        public async Task Validate_ValidEmailAndCode_ShouldNotHaveValidationErrors()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "123456"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public async Task Validate_EmailWithSpecialCharacters_ShouldNotHaveValidationError()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test.email+tag@gmail.com",
                Code = "123456"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public async Task Validate_LongValidCode_ShouldNotHaveValidationError()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = "1234567890ABCDEF"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Code);
        }

        [Theory]
        [InlineData("test@gmail.com")]
        [InlineData("test@yahoo.com")]
        [InlineData("test@yandex.ru")]
        [InlineData("test@mail.ru")]
        public async Task Validate_VariousAllowedDomains_ShouldNotHaveValidationError(string email)
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = email,
                Code = "123456"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public async Task Validate_MultipleValidationErrors_ShouldHaveAllErrors()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "",
                Code = ""
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("Email is required");
            result.ShouldHaveValidationErrorFor(x => x.Code)
                .WithErrorMessage("Code is required");
        }

        [Fact]
        public async Task Validate_EmailWithInvalidDomainButValidFormat_ShouldHaveOnlyDomainError()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "valid.email@invalidomain.com",
                Code = "123456"
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails");
            result.ShouldNotHaveValidationErrorFor(x => x.Code);
        }

        // Not allow case insensitive
        //[Fact]
        //public async Task Validate_CaseInsensitiveEmailValidation_ShouldPass()
        //{
        //    // Arrange
        //    var command = new ConfirmEmailCommand
        //    {
        //        Email = "TEST@GMAIL.COM",
        //        Code = "123456"
        //    };

        //    // Act
        //    var result = await _validator.TestValidateAsync(command);

        //    // Assert
        //    result.ShouldNotHaveAnyValidationErrors();
        //}

        [Theory]
        [InlineData("1")]
        [InlineData("ABC")]
        [InlineData("!@#$%")]
        [InlineData("123ABC!@#")]
        public async Task Validate_VariousCodeFormats_ShouldNotHaveValidationError(string code)
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Email = "test@gmail.com",
                Code = code
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Code);
        }

        // Note: The commented-out validation methods in the original validator
        // would require additional test cases if they were uncommented:
        //[Fact]
        //public async Task BeExistEmail_ExistingEmail_ShouldReturnTrue()
        //{
        //    // Arrange
        //    var email = "test@gmail.com";
        //    _userRepositoryMock.Setup(x => x.EmailExistsAsync(email, It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(true);

        //    // Act
        //    var result = await _validator.BeExistEmail(email, CancellationToken.None);

        //    // Assert
        //    result.Should().BeTrue();
        //}

        //[Fact]
        //public async Task BeNotAlreadyConfirmed_UnconfirmedEmail_ShouldReturnTrue()
        //{
        //    // Arrange
        //    var email = "test@gmail.com";
        //    var user = new User
        //    {
        //        Email = email,
        //        EmailConfirmed = false
        //    };

        //    _userRepositoryMock.Setup(x => x.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(user);

        //    // Act
        //    var result = await _validator.BeNotAlreadyConfirmed(email, CancellationToken.None);

        //    // Assert
        //    result.Should().BeTrue();
        //}

        //[Fact]
        //public async Task BeValidConfirmationCode_ValidCode_ShouldReturnTrue()
        //{
        //    // Arrange
        //    var command = new ConfirmEmailCommand
        //    {
        //        Email = "test@gmail.com",
        //        Code = "123456"
        //    };

        //    var user = new User
        //    {
        //        Email = command.Email,
        //        EmailConfirmationToken = command.Code,
        //        EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(10)
        //    };

        //    _userRepositoryMock.Setup(x => x.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(user);

        //    // Act
        //    var result = await _validator.BeValidConfirmationCode(command, CancellationToken.None);

        //    // Assert
        //    result.Should().BeTrue();
        //}
    }
}
