using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common
{
    public class ValidationError : Error
    {
        public ValidationError(string message) : base(message)
        {
        }

        public ValidationError WithPropertyName(string propertyName)
        {
            WithMetadata("PropertyName", propertyName);
            return this;
        }

        public ValidationError WithAttemptedValue(object attemptedValue)
        {
            WithMetadata("AttemptedValue", attemptedValue);
            return this;
        }

        public ValidationError WithErrorCode(string errorCode)
        {
            WithMetadata("ErrorCode", errorCode);
            return this;
        }
    }
}
