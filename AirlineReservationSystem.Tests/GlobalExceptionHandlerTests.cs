using System.Text.Json;
using AirlineReservationSystem.Middlewares;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Service.Helpers.Exceptions;

namespace AirlineReservationSystem.Tests;

public class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, JsonElement Body, string? ContentType)> HandleAsync(Exception exception, string environment = "Production")
    {
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new HostingEnvironment { EnvironmentName = environment });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, body.RootElement, context.Response.ContentType);
    }

    [Fact]
    public async Task ValidationErrors_Return400WithFieldErrors()
    {
        var exception = new ValidationException(new[]
        {
            new ValidationFailure("Email", "Email is required."),
            new ValidationFailure("Passengers[0].DocumentNumber", "Enter a valid document number.")
        });

        var (status, body, contentType) = await HandleAsync(exception);

        Assert.Equal(400, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal("Email is required.", body.GetProperty("errors").GetProperty("Email")[0].GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("Passengers[0].DocumentNumber", out _));
        Assert.Equal("/api/test", body.GetProperty("instance").GetString());
    }

    [Theory]
    [InlineData(typeof(UnauthorizedException), 401)]
    [InlineData(typeof(NotFoundException), 404)]
    [InlineData(typeof(ConflictException), 409)]
    [InlineData(typeof(BusinessRuleException), 422)]
    public async Task ServiceExceptions_MapToStatusCodes(Type exceptionType, int expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Readable message.")!;

        var (status, body, _) = await HandleAsync(exception);

        Assert.Equal(expected, status);
        Assert.Equal(expected, body.GetProperty("status").GetInt32());
        Assert.Equal("Readable message.", body.GetProperty("detail").GetString());
        Assert.True(body.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task UnexpectedErrors_Return500_AndHideDetailsOutsideDevelopment()
    {
        var (status, body, _) = await HandleAsync(new InvalidOperationException("connection string with password"));

        Assert.Equal(500, status);
        Assert.False(body.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String);

        var (_, developmentBody, _) = await HandleAsync(new InvalidOperationException("boom"), Environments.Development);
        Assert.Equal("boom", developmentBody.GetProperty("detail").GetString());
    }
}
