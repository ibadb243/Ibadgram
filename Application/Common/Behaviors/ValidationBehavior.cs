using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Application.Common.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : ResultBase
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

        public ValidationBehavior(
            IEnumerable<IValidator<TRequest>> validators,
            ILogger<ValidationBehavior<TRequest, TResponse>> logger)
        {
            _validators = validators;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!_validators.Any())
            {
                return await next();
            }

            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => Task.Run(() => v.Validate(context), cancellationToken))
            );

            var failures = validationResults
                .SelectMany(result => result.Errors)
                .Where(error => error != null)
                .ToList();

            if (failures.Count != 0)
            {
                var errors = failures.Select(failure =>
                    new ValidationError(failure.ErrorMessage)
                        .WithPropertyName(failure.PropertyName)
                        .WithAttemptedValue(failure.AttemptedValue)
                        .WithErrorCode(failure.ErrorCode)
                ).ToArray();
                
                return MakeFailResult(typeof(TResponse), errors);
            }

            return await next();
        }

        private TResponse MakeFailResult(Type responseType, ValidationError[] errors)
        {
            if (responseType.IsGenericType)
            {
                var tvalue = responseType.GetGenericArguments()[0];
                var extensionType = typeof(Result);

                var methodInfo = extensionType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .First(m =>
                        m.Name == "Fail" &&
                        m.IsGenericMethodDefinition &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(IEnumerable<IError>))
                .MakeGenericMethod(tvalue);

                var failedResult = (TResponse)methodInfo.Invoke(null, new object[] { errors });

                return failedResult;
            }
            else
            {
                var failedResult = Result.Fail(errors);
                return (TResponse)(object)failedResult;
            }
        }
    }
}
