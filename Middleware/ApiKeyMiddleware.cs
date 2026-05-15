using System.Text.Json;
using XiangqiApi.Models;
using XiangqiApi.Model;

namespace XiangqiAnalyzerApi.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private const string ApiKeyHeaderName = "Authorization";
    private const string ApiKeyPrefix = "Bearer ";

    public ApiKeyMiddleware(
        RequestDelegate next,  // RequestDelegate được cung cấp bởi UseMiddleware
        IConfiguration configuration,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip API key check for health check endpoint
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            await SendUnauthorizedResponse(context, "missing_api_key", "API key is missing. Please provide Authorization: Bearer <key>");
            return;
        }

        var apiKey = extractedApiKey.ToString();
        if (!apiKey.StartsWith(ApiKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await SendUnauthorizedResponse(context, "invalid_auth_format", "Invalid authorization format. Use Bearer <key>");
            return;
        }

        apiKey = apiKey.Substring(ApiKeyPrefix.Length).Trim();

        var validApiKey = _configuration["ApiKey"];
        if (string.IsNullOrEmpty(validApiKey) || apiKey != validApiKey)
        {
            _logger.LogWarning("Invalid API key attempt from {RemoteIp}", context.Connection.RemoteIpAddress);
            await SendUnauthorizedResponse(context, "invalid_api_key", "Invalid API key");
            return;
        }

        await _next(context);
    }

    private static async Task SendUnauthorizedResponse(HttpContext context, string error, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Error = error,
            Message = message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }
}