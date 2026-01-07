using Rapp;
using MemoryPack;
using Sannr; 

namespace VikingAir.Core;

// RappCache makes it safe for HybridCache binary serialization
// Sannr will auto-generate validation extensions for this model at compile-time
[RappCache]
[MemoryPackable]
public partial class BookingRequest
{
    [Required(ErrorMessage = "Flight code is required")]
    [StringLength(10, MinimumLength = 3, ErrorMessage = "Flight code must be 3-10 characters")]
    [Sanitize(Trim = true, ToUpper = true)]
    public string FlightCode { get; set; } = "";

    [Required(ErrorMessage = "Passport number is required")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "Passport number must be 5-20 characters")]
    [Sanitize(Trim = true, ToUpper = true)]
    public string PassportNumber { get; set; } = "";
    
    [Required(ErrorMessage = "Seat preference is required")]
    [AllowedValues("Window", "Aisle", "Middle", ErrorMessage = "Seat preference must be Window, Aisle, or Middle")]
    public string SeatPreference { get; set; } = "Window";
}

public interface IPaymentGateway
{
    bool Charge(string creditCardNumber, decimal amount);
    bool CheckRisk(string passport);
}
