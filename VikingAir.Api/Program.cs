using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using VikingAir.Core;
using Microsoft.Extensions.Hosting;
using Rapp;
using Sannr.AspNetCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateSlimBuilder(args);

// 1. Add Aspire Service Defaults (OpenTelemetry, Health Checks)
builder.AddServiceDefaults();

// 2. Add Sannr for AOT-optimized validation
builder.Services.AddSannr(options =>
{
    options.EnableMetrics = true; // Track validation performance
});

// 3. Configure JSON for AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// 4. Add Redis for HybridCache (Orchestrated by Aspire)
builder.AddRedisClient("cache");

// 5. Add HybridCache with Rapp for schema-safe binary serialization
#pragma warning disable EXTEXP0018 // HybridCache is preview
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
}).UseRappForBookingRequest(); // Rapp generates this extension method!
#pragma warning restore EXTEXP0018

// 6. AutoMappic performs the wire -> domain -> response mapping. The mapping bodies are
//    source-generated inside VikingAir.Core, so no reflection or expression compilation
//    occurs at runtime and the mapper survives trimming and Native AOT publication.

var app = builder.Build();

app.MapDefaultEndpoints();

// STEP 1 of the pipeline is validation.
//
// Sannr's ASP.NET Core integration is deliberately NOT used here. Both
// SannrEndpointExtensions.WithSannrValidation(RouteGroupBuilder) and
// RouteHandlerBuilderExtensions.WithSannrValidation(RouteHandlerBuilder) were verified against
// this endpoint and neither rejected a payload that violates every rule on the model - the
// request was answered 200 Confirmed under both JIT and Native AOT. Sannr's registry also
// reports success for a type that has no registered validator, so a wiring failure is silent.
//
// The generated validator itself is correct; VikingAir.Tests proves it rejects these payloads.
// So this endpoint resolves the generated validator directly and fails closed if it is missing.
// Once the integration enforces validation, this block can be replaced by the filter.
if (!Sannr.SannrValidatorRegistry.TryGetValidator(typeof(BookingRequest), out var bookingValidator)
    || bookingValidator is null)
{
    throw new InvalidOperationException(
        "No Sannr validator is registered for BookingRequest. Refusing to start: Sannr's registry " +
        "treats an unregistered type as valid, so the API would accept every payload.");
}

var api = app.MapGroup("/api");

api.MapPost("/book", async (BookingRequest request, HybridCache cache) =>
{
    // STEP 1: SANNR VALIDATION (explicit, fail-closed - see the note above)
    // Sannr's [Sanitize] rules are applied by the validator, so `request` is trimmed and
    // upper-cased in place before anything downstream sees it.
    var validation = await bookingValidator(new Sannr.SannrValidationContext(request));
    if (validation.Errors.Count > 0)
    {
        return Results.ValidationProblem(
            validation.Errors
                .GroupBy(e => e.MemberName ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message ?? "Invalid.").ToArray()));
    }

    // STEP 2: RAPP CACHING (Binary & Schema-Safe)
    var key = $"booking:{request.PassportNumber}";

    // Rapp provides schema-safe binary serialization for HybridCache
    var cached = await cache.GetOrCreateAsync(
        key,
        async ValueTask<BookingRequest> (CancellationToken cancel) =>
        {
            // In a real app, this would be a DB fetch
            await Task.Delay(100, cancel); // Simulate DB call
            return request;
        }
    );

    // STEP 3: AUTOMAPPIC MAPPING (compile-time generated)
    // Wire shape -> persistence shape -> response shape. The response deliberately
    // never carries the full passport number.
    var entity = BookingMapping.ToEntity(cached);
    var confirmation = BookingMapping.ToConfirmation(entity);

    return Results.Ok(new BookingResponse
    {
        Status = "Confirmed",
        Data = confirmation,
        Message = "Validated by Sannr, cached by Rapp, mapped by AutoMappic - all without runtime reflection."
    });
});

app.Run();

// Response model for proper AOT serialization
public record BookingResponse
{
    public string Status { get; init; } = "";
    public BookingConfirmationDto? Data { get; init; }
    public string Message { get; init; } = "";
}

[JsonSerializable(typeof(BookingRequest))]
[JsonSerializable(typeof(BookingEntity))]
[JsonSerializable(typeof(BookingConfirmationDto))]
[JsonSerializable(typeof(BookingResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
