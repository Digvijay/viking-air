using Xunit;
using VikingAir.Core;

namespace VikingAir.Tests;

/// <summary>
/// AutoMappic generates the mapping at compile time. These tests pin the behaviour that matters
/// for the API contract, in particular that the confirmation shape does not leak a full passport
/// number.
/// </summary>
public class MappingTests
{
    private static BookingRequest ValidRequest() => new()
    {
        FlightCode = "VA123",
        PassportNumber = "ABC123DEF",
        SeatPreference = "Window"
    };

    [Fact]
    public void ToEntity_copies_every_member()
    {
        var request = ValidRequest();

        var entity = BookingMapping.ToEntity(request);

        Assert.Equal(BookingId.For(request.FlightCode, request.PassportNumber), entity.Id);
        Assert.Equal(request.FlightCode, entity.Flight);
        Assert.Equal(request.PassportNumber, entity.Passport);
        Assert.Equal(request.SeatPreference, entity.Seat);
        Assert.NotEqual(default, entity.CreatedAt);
    }

    [Fact]
    public void ToConfirmation_exposes_only_the_passport_suffix()
    {
        var request = ValidRequest();
        var entity = BookingMapping.ToEntity(request);

        var confirmation = BookingMapping.ToConfirmation(entity);

        Assert.DoesNotContain(request.PassportNumber, confirmation.PassportSuffix);
        Assert.Equal(BookingId.Suffix(request.PassportNumber), confirmation.PassportSuffix);
        Assert.Equal(3, confirmation.PassportSuffix.Length);
    }

    [Fact]
    public void ToConfirmation_never_carries_the_full_passport_on_any_member()
    {
        var request = ValidRequest();

        var confirmation = BookingMapping.ToConfirmation(BookingMapping.ToEntity(request));

        // Guards the response shape against a future member being added that re-exposes it.
        var rendered = System.Text.Json.JsonSerializer.Serialize(confirmation);
        Assert.DoesNotContain(request.PassportNumber, rendered, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Mapping_is_deterministic()
    {
        var request = ValidRequest();

        Assert.Equal(
            BookingMapping.ToEntity(request).Id,
            BookingMapping.ToEntity(request).Id);
    }
}
