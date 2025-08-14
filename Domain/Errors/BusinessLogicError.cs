using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Errors
{
    public class BusinessLogicError : Error
    {
        public string ErrorCode { get; }
        public object? AdditionalData { get; }

        public BusinessLogicError(string code, string message, object? additionalData = null)
            : base(message)
        {
            ErrorCode = code;
            AdditionalData = additionalData;
            WithMetadata("ErrorCode", code);
            if (additionalData != null)
            {
                WithMetadata("AdditionalData", additionalData);
            }
        }
    }
}
