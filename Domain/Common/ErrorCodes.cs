using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Common
{
    public static class ErrorCodes
    {
        // Validation errors
        public const string REQUIRED_FIELD = "REQUIRED_FIELD";
        public const string INVALID_FORMAT = "INVALID_FORMAT";
        public const string FIELD_TOO_SHORT = "FIELD_TOO_SHORT";
        public const string FIELD_TOO_LONG = "FIELD_TOO_LONG";
        public const string UNSUPPORTED_EMAIL_DOMAIN = "UNSUPPORTED_EMAIL_DOMAIN";

        // Business logic errors
        public const string USER_NOT_FOUND = "USER_NOT_FOUND";
        public const string USER_ALREADY_VERIFIED = "USER_ALREADY_VERIFIED";
        public const string EMAIL_ALREADY_CONFIRMED = "EMAIL_ALREADY_CONFIRMED";
        public const string EMAIL_NOT_CONFIRMED = "EMAIL_NOT_CONFIRMED";
        public const string EMAIL_AWAITING_CONFIRMATION = "EMAIL_AWAITING_CONFIRMATION";
        public const string EMAIL_DELIVERY_FAILED = "EMAIL_DELIVERY_FAILED";
        public const string INVALID_CONFIRMATION_CODE = "INVALID_CONFIRMATION_CODE";
        public const string CONFIRMATION_CODE_EXPIRED = "CONFIRMATION_CODE_EXPIRED";
        public const string INVALID_CONTENT = "USER_ALREADY_VERIFIED";
        public const string ACCOUNT_ALREADY_COMPLETED = "ACCOUNT_ALREADY_COMPLETED";
        public const string USERNAME_ALREADY_TAKEN = "USERNAME_ALREADY_TAKEN";

        // System errors
        public const string DATABASE_ERROR = "DATABASE_ERROR";
        public const string EXTERNAL_SERVICE_ERROR = "EXTERNAL_SERVICE_ERROR";
        public const string CONFIRMATION_TOKEN_NOT_FOUND = "CONFIRMATION_TOKEN_NOT_FOUND";
    }
}
