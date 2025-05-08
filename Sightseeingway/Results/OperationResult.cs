// filepath: f:\Replica\NAS\Files\repo\github\Sightseeingway\Sightseeingway\Results\OperationResult.cs
using System;

namespace Sightseeingway.Results
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail.
    /// </summary>
    /// <typeparam name="T">Type of the result data when operation succeeds</typeparam>
    public class OperationResult<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; }
        
        /// <summary>
        /// Gets the result data if the operation was successful.
        /// </summary>
        public T? Data { get; }
        
        /// <summary>
        /// Gets the error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; }
        
        /// <summary>
        /// Gets the exception if the operation failed due to an exception.
        /// </summary>
        public Exception? Exception { get; }

        private OperationResult(bool isSuccess, T? data, string? errorMessage, Exception? exception)
        {
            IsSuccess = isSuccess;
            Data = data;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        /// <summary>
        /// Creates a successful result with data.
        /// </summary>
        /// <param name="data">The result data.</param>
        /// <returns>A successful operation result.</returns>
        public static OperationResult<T> Success(T data)
        {
            return new OperationResult<T>(true, data, null, null);
        }

        /// <summary>
        /// Creates a successful result without data.
        /// </summary>
        /// <returns>A successful operation result.</returns>
        public static OperationResult<T> Success()
        {
            return new OperationResult<T>(true, default, null, null);
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult<T> Failure(string errorMessage)
        {
            return new OperationResult<T>(false, default, errorMessage, null);
        }

        /// <summary>
        /// Creates a failed result with an exception.
        /// </summary>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult<T> Failure(Exception exception)
        {
            return new OperationResult<T>(false, default, exception.Message, exception);
        }

        /// <summary>
        /// Creates a failed result with a custom error message and exception.
        /// </summary>
        /// <param name="errorMessage">The custom error message.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult<T> Failure(string errorMessage, Exception exception)
        {
            return new OperationResult<T>(false, default, errorMessage, exception);
        }
    }

    /// <summary>
    /// Non-generic version of OperationResult for operations that don't return data.
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; }
        
        /// <summary>
        /// Gets the error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; }
        
        /// <summary>
        /// Gets the exception if the operation failed due to an exception.
        /// </summary>
        public Exception? Exception { get; }

        private OperationResult(bool isSuccess, string? errorMessage, Exception? exception)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <returns>A successful operation result.</returns>
        public static OperationResult Success()
        {
            return new OperationResult(true, null, null);
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult Failure(string errorMessage)
        {
            return new OperationResult(false, errorMessage, null);
        }

        /// <summary>
        /// Creates a failed result with an exception.
        /// </summary>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult Failure(Exception exception)
        {
            return new OperationResult(false, exception.Message, exception);
        }

        /// <summary>
        /// Creates a failed result with a custom error message and exception.
        /// </summary>
        /// <param name="errorMessage">The custom error message.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult Failure(string errorMessage, Exception exception)
        {
            return new OperationResult(false, errorMessage, exception);
        }

        /// <summary>
        /// Converts this result to a generic result with the specified data.
        /// </summary>
        /// <typeparam name="T">The type of the result data.</typeparam>
        /// <param name="data">The data for successful result.</param>
        /// <returns>A generic operation result.</returns>
        public OperationResult<T> WithData<T>(T data)
        {
            return IsSuccess
                ? OperationResult<T>.Success(data)
                : OperationResult<T>.Failure(ErrorMessage ?? string.Empty, Exception);
        }
    }
}