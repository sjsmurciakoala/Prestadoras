namespace SIAD.Core.DTOs.Common;

public class ResponseModelDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public static ResponseModelDto Ok(object? data = null, string? message = null) =>
        new()
        {
            Success = true,
            Data = data,
            Message = message ?? "OK"
        };

    public static ResponseModelDto Fail(string message) =>
        new()
        {
            Success = false,
            Message = message
        };
}
