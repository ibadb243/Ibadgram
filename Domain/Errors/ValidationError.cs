using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Errors
{
    public class ValidationError : Error
    {
        public string ErrorCode { get; }
        public string PropertyName { get; }
        public object? AttemptedValue { get; }

        public ValidationError(string code, string message, string propertyName, object? attemptedValue = null)
            : base(message)
        {
            ErrorCode = code;
            PropertyName = propertyName;
            AttemptedValue = attemptedValue;
            WithMetadata("ErrorCode", code);
            WithMetadata("PropertyName", propertyName);
            if (attemptedValue != null)
            {
                WithMetadata("AttemptedValue", attemptedValue);
            }
        }
    }
}
