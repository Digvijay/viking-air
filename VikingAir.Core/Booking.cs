using AutoMappic;

namespace VikingAir.Core;

/// <summary>
/// Persistence shape for a booking. Deliberately differs from <see cref="BookingRequest"/>
/// so the mapping is non-trivial and exercises AutoMappic rather than a field-for-field copy.
/// </summary>
public sealed class BookingEntity
{
    public string Id { get; set; } = "";

    public string Flight { get; set; } = "";

    public string Passport { get; set; } = "";

    public string Seat { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Outbound shape returned to callers. Never exposes the raw passport number.
/// </summary>
public sealed class BookingConfirmationDto
{
    public string BookingId { get; set; } = "";

    public string FlightCode { get; set; } = "";

    /// <summary>Last three characters of the passport number only.</summary>
    public string PassportSuffix { get; set; } = "";

    public string SeatPreference { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Compile-time mapping configuration. AutoMappic generates the mapping bodies during build,
/// so no reflection or runtime expression compilation is required and the code survives
/// trimming and Native AOT publication.
/// </summary>
internal sealed class BookingProfile : Profile
{
    public BookingProfile()
    {
        CreateMap<BookingRequest, BookingEntity>()
            .ForMember(d => d.Flight, opt => opt.MapFrom(src => src.FlightCode))
            .ForMember(d => d.Passport, opt => opt.MapFrom(src => src.PassportNumber))
            .ForMember(d => d.Seat, opt => opt.MapFrom(src => src.SeatPreference))
            .ForMember(d => d.Id, opt => opt.MapFrom(src => BookingId.For(src.FlightCode, src.PassportNumber)))
            .ForMember(d => d.CreatedAt, opt => opt.MapFrom(src => DateTimeOffset.UtcNow));

        CreateMap<BookingEntity, BookingConfirmationDto>()
            .ForMember(d => d.BookingId, opt => opt.MapFrom(src => src.Id))
            .ForMember(d => d.FlightCode, opt => opt.MapFrom(src => src.Flight))
            .ForMember(d => d.SeatPreference, opt => opt.MapFrom(src => src.Seat))
            .ForMember(d => d.PassportSuffix, opt => opt.MapFrom(src => BookingId.Suffix(src.Passport)));
    }
}

/// <summary>
/// Deterministic identifier helpers, kept separate so both the mapping profile and the
/// hand-written baseline in the benchmark project use exactly the same logic.
/// </summary>
public static class BookingId
{
    public static string For(string flightCode, string passportNumber)
        => string.Concat(flightCode, "-", Suffix(passportNumber));

    public static string Suffix(string passportNumber)
        => passportNumber.Length <= 3 ? passportNumber : passportNumber[^3..];
}

/// <summary>
/// Builds the configured mapper. This lives in VikingAir.Core so that every AutoMappic
/// source-generated artefact is produced in the same assembly as <see cref="BookingProfile"/>.
/// </summary>
internal static class BookingMapper
{
    public static IMapper Create()
        => new MapperConfiguration(cfg => cfg.AddProfile<BookingProfile>()).CreateMapper();
}

/// <summary>
/// Mapping facade.
/// <para>
/// AutoMappic 0.7.0 generates its interceptors and helper methods into the assembly that
/// declares the mapped types and the <see cref="Profile"/>. A <c>Map&lt;T&gt;()</c> call made
/// from another assembly generates an interceptor that references helpers which do not exist
/// there, so the consuming project fails to compile. Every mapping call is therefore kept
/// inside this assembly and exposed through this facade.
/// </para>
/// <para>
/// Tracked as an AutoMappic limitation: cross-assembly mapping is not currently supported.
/// </para>
/// </summary>
public static class BookingMapping
{
    private static readonly IMapper Mapper = BookingMapper.Create();

    public static BookingEntity ToEntity(BookingRequest request) => Mapper.Map<BookingEntity>(request);

    public static BookingConfirmationDto ToConfirmation(BookingEntity entity)
        => Mapper.Map<BookingConfirmationDto>(entity);
}
