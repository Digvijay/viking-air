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
// This uses Sannr's ASP.NET Core integration directly. Earlier it could not: both
// WithSannrValidation overloads bound to a shadow registry that nothing ever wrote to, so the
// filter silently skipped validation and answered 200 Confirmed for a payload violating every
// rule on the model. Sannr 1.7.0 fixes the shadowing and additionally fails closed at endpoint
// construction when a bound model has no registered validator, so a wiring failure can no longer
// be silent. The explicit validator lookup that used to stand in for the filter is gone.
var api = app.MapGroup("/api").WithSannrValidation();

api.MapPost("/book", async (BookingRequest request, HybridCache cache) =>
{
    // STEP 1 happened in the Sannr endpoint filter above: the request was validated and its
    // [Sanitize] rules applied, so `request` is already trimmed and upper-cased here. An invalid
    // payload never reaches this delegate.

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
