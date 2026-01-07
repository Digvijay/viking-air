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

var app = builder.Build();

app.MapDefaultEndpoints();

// Create an API group with automatic Sannr validation
var api = app.MapGroup("/api").WithSannrValidation();

api.MapPost("/book", async (BookingRequest request, HybridCache cache) =>
{
    // STEP 1: SANNR VALIDATION
    // Validation happens automatically via WithSannrValidation()
    // If validation fails, Sannr returns a 400 Bad Request with detailed errors
    
    // STEP 2: RAPP CACHING (Binary & Schema-Safe)
    var key = $"booking:{request.PassportNumber}";
    
    // Rapp provides schema-safe binary serialization for HybridCache
    var result = await cache.GetOrCreateAsync(
        key, 
        async cancel => 
        {
            // In a real app, this would be a DB fetch
            await Task.Delay(100, cancel); // Simulate DB call
            return request;
        }
    );

    return Results.Ok(new BookingResponse
    { 
        Status = "Confirmed", 
        Data = result,
        Message = "🛡️ Validated by Sannr (zero-alloc) • 💾 Cached by Rapp (schema-safe binary)"
    });
});

app.Run();

// Response model for proper AOT serialization
public record BookingResponse
{
    public string Status { get; init; } = "";
    public BookingRequest? Data { get; init; }
    public string Message { get; init; } = "";
}

[JsonSerializable(typeof(BookingRequest))]
[JsonSerializable(typeof(BookingResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
