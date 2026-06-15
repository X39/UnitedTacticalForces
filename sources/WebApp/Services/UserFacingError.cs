using System.Net.Http;

namespace X39.UnitedTacticalForces.WebApp.Services;

public static class UserFacingError
{
    private const int MaxMessageLength = 240;

    public const string DefaultMessage = "The request could not be completed. Please try again.";
    public const string ServerRejectedMessage = "The server rejected the request. Check the input and try again.";
    public const string OperationCanceledMessage = "The operation was canceled.";

    public static string Message(Exception exception, string fallback = DefaultMessage)
    {
        if (exception is OperationCanceledException)
            return OperationCanceledMessage;
        if (IsHttpOrApiException(exception))
            return ServerRejectedMessage;

        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message) || ContainsUnsafeDiagnosticText(message))
            return fallback;

        return NormalizeAndTrim(message);
    }

    public static string ConsoleMessage(Exception exception, string fallback = DefaultMessage)
        => Message(exception, fallback);

    private static bool IsHttpOrApiException(Exception exception)
        => exception is HttpRequestException
           || exception.GetType().Name.Contains("ApiException", StringComparison.Ordinal);

    private static bool ContainsUnsafeDiagnosticText(string message)
    {
        if (message.Contains('\r') || message.Contains('\n'))
            return true;

        var unsafeMarkers = new[]
        {
            " at ",
            ".cs:line",
            ":line ",
            "ApiException",
            "Authorization",
            "Bearer ",
            "Exception",
            "Microsoft.",
            "Npgsql",
            "password",
            "RequestInformation",
            "secret",
            "StackTrace",
            "System.",
            "token",
            "X39.",
        };

        return unsafeMarkers.Any((marker) => message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAndTrim(string message)
    {
        var normalized = string.Join(
            " ",
            message.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        );

        return normalized.Length <= MaxMessageLength
            ? normalized
            : string.Concat(normalized.AsSpan(0, MaxMessageLength - 3), "...");
    }
}
