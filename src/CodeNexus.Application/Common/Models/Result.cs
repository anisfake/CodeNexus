namespace CodeNexus.Application.Common.Models;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    protected Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new(true, null, null);

    public static Result Failure(string errorCode, string errorMessage)
        => new(false, errorCode, errorMessage);
}
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage)
        : base(isSuccess, errorCode, errorMessage)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);

    public new static Result<T> Failure(string errorCode, string errorMessage)
        => new(false, default, errorCode, errorMessage);
}
