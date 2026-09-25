using Xunit;
using MemoryPack;
using VikingAir.Core;

namespace VikingAir.Tests;

/// <summary>
/// Rapp caches <see cref="BookingRequest"/> as a binary payload rather than JSON. The property
/// that matters is that a cached value round-trips without loss, because a cache that silently
/// drops a member is worse than no cache.
/// </summary>
public class CacheSerializationTests
{
    [Fact]
    public void Binary_round_trip_preserves_every_member()
    {
        var original = new BookingRequest
        {
            FlightCode = "VA123",
            PassportNumber = "ABC123DEF",
            SeatPreference = "Aisle"
        };

        var restored = MemoryPackSerializer.Deserialize<BookingRequest>(
            MemoryPackSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Equal(original.FlightCode, restored!.FlightCode);
        Assert.Equal(original.PassportNumber, restored.PassportNumber);
        Assert.Equal(original.SeatPreference, restored.SeatPreference);
    }

    [Fact]
    public void Binary_payload_is_smaller_than_json()
    {
        var request = new BookingRequest
        {
            FlightCode = "VA123",
            PassportNumber = "ABC123DEF",
            SeatPreference = "Window"
        };

        var binary = MemoryPackSerializer.Serialize(request).Length;
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request).Length;

        Assert.True(
            binary < json,
            $"Expected the binary payload to be smaller than JSON, but got binary={binary} json={json}.");
    }
}
