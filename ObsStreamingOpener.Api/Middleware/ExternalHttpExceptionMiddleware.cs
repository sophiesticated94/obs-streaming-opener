using ObsStreamingOpener.Application.Exceptions;

namespace ObsStreamingOpener.Api.Middleware;

public sealed class ExternalHttpExceptionMiddleware(
    RequestDelegate next,
    IWebHostEnvironment environment,
    ILogger<ExternalHttpExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ExternalHttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "External provider request failed: {ServiceName} {StatusCode} {ProviderErrorCode} {ProviderErrorMessage}. Response: {ResponseBody}",
                ex.ServiceName,
                (int)ex.StatusCode,
                ex.ProviderErrorCode,
                ex.ProviderErrorMessage,
                ex.ResponseBody);

            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ex.ToProblemDetails(environment.IsDevelopment()));
        }
    }
}
