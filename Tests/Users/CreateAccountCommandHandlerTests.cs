using Application.CQRS.Users.Commands.CreateAccount;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using FluentAssertions;
using FluentValidation;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Users
{
    public class CreateAccountCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<IEmailSender> _emailSenderMock;
        private readonly CreateAccountCommandHandler _handler;

        public CreateAccountCommandHandlerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _emailSenderMock = new Mock<IEmailSender>();
            _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepositoryMock.Object);
            _handler = new CreateAccountCommandHandler(_unitOfWorkMock.Object, _passwordHasherMock.Object, _emailSenderMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_ReturnsUserId()
        {
            // Arrange
            var command = new CreateAccountCommand
            {
                Firstname = "Bob",
                Email = "bob@gmail.com",
                Password = "password123",
            };
            _passwordHasherMock.Setup(h => h.HashPassword(It.IsAny<string>(), It.IsAny<string>())).Returns("hashedPassword");
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(1));
            _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBe(Guid.Empty);
            _emailSenderMock.Verify(e => e.SendEmailAsync(command.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }
    }
}
