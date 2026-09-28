using Xunit;
using VikingAir.Core;

namespace VikingAir.Tests;

/// <summary>
/// Sannr generates a validator for <see cref="BookingRequest"/> at compile time and registers it
/// from a <c>[ModuleInitializer]</c> in the assembly that declares the model.
/// </summary>
public class ValidationTests
{
    private static Sannr.SannrValidatorFuncAsync GetValidator()
    {
        // Sannr's registry returns success when no validator is registered for a type, so a
        // registration failure would otherwise look like a passing test suite.
        Assert.True(
            Sannr.SannrValidatorRegistry.TryGetValidator(typeof(BookingRequest), out var validator),
            "No Sannr validator is registered for BookingRequest. Sannr's registry treats an " +
            "unregistered type as valid, so every validation assertion below would pass vacuously.");

        Assert.NotNull(validator);
        return validator!;
    }

    private static Sannr.ValidationResult Validate(BookingRequest request)
        => GetValidator()(new Sannr.SannrValidationContext(request)).GetAwaiter().GetResult();

    [Fact]
    public void A_validator_is_registered_for_the_model()
    {
        Assert.NotNull(GetValidator());
    }

    [Fact]
    public void Valid_request_produces_no_errors()
    {
        var result = Validate(new BookingRequest
        {
            FlightCode = "VA123",
            PassportNumber = "ABC123DEF",
            SeatPreference = "Window"
        });

        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("", "ABC123DEF", "Window")]          // FlightCode required
    [InlineData("VA", "ABC123DEF", "Window")]        // FlightCode too short
    [InlineData("VA123", "", "Window")]              // PassportNumber required
    [InlineData("VA123", "ABC", "Window")]           // PassportNumber too short
    [InlineData("VA123", "ABC123DEF", "Balcony")]    // SeatPreference not an allowed value
    public void Invalid_request_produces_errors(string flight, string passport, string seat)
    {
        var result = Validate(new BookingRequest
        {
            FlightCode = flight,
            PassportNumber = passport,
            SeatPreference = seat
        });

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Sanitization_trims_and_upper_cases_before_validation()
    {
        var request = new BookingRequest
        {
            FlightCode = "  va123  ",
            PassportNumber = "  abc123def  ",
            SeatPreference = "Window"
        };

        var result = Validate(request);

        Assert.Empty(result.Errors);
        Assert.Equal("VA123", request.FlightCode);
        Assert.Equal("ABC123DEF", request.PassportNumber);
    }
}
