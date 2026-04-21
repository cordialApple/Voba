namespace Voba.Services
{
    public class ServiceResult<T>
    {
        public T? Data { get; init; }
        public bool Success { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }

        public static ServiceResult<T> Ok(T data) =>
            new() { Data = data, Success = true };

        public static ServiceResult<T> Fail(string code, string message) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = message };
    }

    public static class ErrorCodes
    {
        public const string ValidationError    = "VALIDATION_ERROR";
        public const string NotFound           = "NOT_FOUND";
        public const string Unauthorized       = "UNAUTHORIZED";
        public const string ExternalApiFailure = "EXTERNAL_API_FAILURE";
        public const string DatabaseError      = "DATABASE_ERROR";
    }
}
