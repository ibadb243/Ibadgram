using Application.CQRS.Users.Commands.Refresh;
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
    public class RefreshTokenCommandValidatorTests
    {
        private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
        private readonly RefreshTokenCommandValidator _validator;

        public RefreshTokenCommandValidatorTests()
        {
            _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
            _validator = new RefreshTokenCommandValidator(_refreshTokenRepositoryMock.Object);
        }

        [Fact]
        public async Task Validate_ValidCommand_Passes()
        {
            // Arrange
            var command = new RefreshTokenCommand
            {
                RefreshToken = "valid_token"
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validate_EmptyRefreshToken_Fails()
        {
            // Arrange
            var command = new RefreshTokenCommand
            {
                RefreshToken = ""
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "RefreshToken" && e.ErrorMessage == "RefreshToken is required");
        }

        [Fact]
        public async Task Validate_NullRefreshToken_Fails()
        {
            // Arrange
            var command = new RefreshTokenCommand
            {
                RefreshToken = null
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "RefreshToken" && e.ErrorMessage == "RefreshToken is required");
        }
    }
}
