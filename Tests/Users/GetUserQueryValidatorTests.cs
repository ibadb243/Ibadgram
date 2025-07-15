using Application.CQRS.Users.Queries.Get;
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
    public class GetUserQueryValidatorTests
    {
        private Mock<IUserRepository> _userRepositoryMock;
        private GetUserQueryValidator _validator;

        public GetUserQueryValidatorTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _validator = new GetUserQueryValidator(_userRepositoryMock.Object);
        }

        [Fact]
        public async Task Validate_ValidQuery_Passes()
        {
            // Arrange
            var command = new GetUserQuery
            {
                UserId = Guid.NewGuid()
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
            var command = new GetUserQuery
            {
                UserId = Guid.Empty
            };

            // Act
            var result = await _validator.ValidateAsync(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId" && e.ErrorMessage == "UserId is required");
        }
    }
}
