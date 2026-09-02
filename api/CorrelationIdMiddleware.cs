using Microsoft.AspNetCore.Http;

namespace Crm.Api;

/// <summary>
/// Ensures every response has an <c>X-RequestId</c> header and writes a correlation-id
/// log scope so all log lines for a request share the same ID.
///
/// When a client sends an <c>X-RequestId</c> (or <c>traceparent</c>) header it is reused;
/// otherwise a new GUID is generated. The value is stored in HttpContext.Items and exposed
/// via <see cref="CorrelationId"/>.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const int MaxLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var id = ExtractId(context) ?? Guid.NewGuid().ToString("N");
        context.Items[CorrelationIdKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[XRequestIdHeader] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>(1)
               {
                   [nameof(CorrelationId)] = id
               }))
        {
            await next(context);
        }
    }

    private static string? ExtractId(HttpContext context)
    {
        var fromHeader = context.Request.Headers[XRequestIdHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fromHeader) && fromHeader.Length <= MaxLength)
            return FromAscii(fromHeader);

        var fromTrace = context.Request.Headers.TraceParent.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fromTrace) && fromTrace.Length <= MaxLength)
            return FromAscii(fromTrace);

        return null;
    }

    private static string FromAscii(string value)
    {
        // Take only the ASCII-safe prefix; drop any control characters.
        var end = 0;
        while (end < value.Length && value[end] >= ' ' && value[end] < 127)
            end++;
        return value[..end];
    }

    public static string CorrelationId(HttpContext context)
        => (context.Items[CorrelationIdKey] as string) ?? string.Empty;

    private const string CorrelationIdKey = "X-RequestId";
    private const string XRequestIdHeader = "X-RequestId";
}