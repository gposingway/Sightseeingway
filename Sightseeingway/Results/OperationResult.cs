using System;

namespace Sightseeingway.Results
{
    public readonly record struct OperationResult(bool IsSuccess, string? ErrorMessage, Exception? Exception)
    {
        public static OperationResult Success() => new(true, null, null);
        public static OperationResult Failure(string errorMessage) => new(false, errorMessage, null);
        public static OperationResult Failure(string errorMessage, Exception exception) => new(false, errorMessage, exception);
    }

    public readonly record struct OperationResult<T>(bool IsSuccess, T? Data, string? ErrorMessage, Exception? Exception)
    {
        public static OperationResult<T> Success(T data) => new(true, data, null, null);
        public static OperationResult<T> Failure(string errorMessage) => new(false, default, errorMessage, null);
        public static OperationResult<T> Failure(Exception exception) => new(false, default, exception.Message, exception);
        public static OperationResult<T> Failure(string errorMessage, Exception exception) => new(false, default, errorMessage, exception);
    }
}
