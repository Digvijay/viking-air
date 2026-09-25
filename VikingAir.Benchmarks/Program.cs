using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using FluentValidation;
using MemoryPack;
using VikingAir.Core;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace VikingAir.Benchmarks;

/// <summary>
/// Validation: Sannr's compile-time generated validator against the two common
/// reflection-based alternatives.
/// </summary>
[MemoryDiagnoser]
public class ValidationBenchmarks
{
    private BookingRequest _request = null!;
    private FluentBookingValidator _fluentValidator = null!;
    private Sannr.SannrValidatorFuncAsync _sannrValidator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _request = new BookingRequest
        {
            FlightCode = "VA123",
            PassportNumber = "ABC123DEF",
            SeatPreference = "Window"
        };
        _fluentValidator = new FluentBookingValidator();

        // Sannr registers generated validators from a [ModuleInitializer] in the assembly
        // that declares the model. Fail loudly rather than benchmarking a no-op: the registry
        // returns Success() for unregistered types, which would otherwise look like a very
        // fast validator that never validates anything.
        if (!Sannr.SannrValidatorRegistry.TryGetValidator(typeof(BookingRequest), out var validator) || validator is null)
        {
            throw new InvalidOperationException(
                "No Sannr validator was registered for BookingRequest. The benchmark would " +
                "otherwise report timings for a validator that performs no work.");
        }

        _sannrValidator = validator;

        // Guard the comparison itself: a known-invalid model must actually be rejected,
        // otherwise the numbers are not measuring comparable work.
        var invalid = new BookingRequest { FlightCode = "", PassportNumber = "", SeatPreference = "Sideways" };
        if (_sannrValidator(new Sannr.SannrValidationContext(invalid)).GetAwaiter().GetResult().Errors.Count == 0)
        {
            throw new InvalidOperationException("Sannr accepted a model the benchmark expects to be invalid.");
        }
    }

    [Benchmark(Baseline = true)]
    public object FluentValidation() => _fluentValidator.Validate(_request);

    [Benchmark]
    public object DataAnnotationsValidation()
    {
        var context = new ValidationContext(_request);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Validator.TryValidateObject(_request, context, results, validateAllProperties: true);
        return results;
    }

    [Benchmark]
    public object SannrValidation()
        => _sannrValidator(new Sannr.SannrValidationContext(_request)).GetAwaiter().GetResult();
}

/// <summary>
/// <summary>
/// Mapping: AutoMapper's reflection-based mapper, AutoMappic's compile-time generated mapper,
/// and an equivalent hand-written projection that represents the practical performance ceiling.
/// </summary>
/// <remarks>
/// This class previously required BenchmarkDotNet's in-process toolchain. AutoMappic's generator
/// derives C# identifiers from the assembly name without guaranteeing they are valid identifiers,
/// and BenchmarkDotNet names its generated host assembly <c>&lt;Project&gt;-&lt;Job&gt;-&lt;N&gt;</c>.
/// The hyphens produced <c>class AutoMappic_Extension_VikingAir_Benchmarks-DefaultJob-1</c>, which
/// the compiler parsed as a subtraction expression (CS0116 / CS1106 / CS0548), so the host project
/// never built.
///
/// That is fixed upstream, so these benchmarks now run on the default out-of-process toolchain
/// with full process isolation, like every other class here.
/// </remarks>
[MemoryDiagnoser]
public class MappingBenchmarks
{
    private BookingRequest _request = null!;
    private AutoMapper.IMapper _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        _request = new BookingRequest
        {
            FlightCode = "VA123",
            PassportNumber = "ABC123DEF",
            SeatPreference = "Window"
        };

        // AutoMapper is the reflection-based incumbent this library exists to replace.
        // It is configured to perform exactly the same projection as the other two.
        var mapperConfigExpression = new AutoMapper.MapperConfigurationExpression();
        mapperConfigExpression.CreateMap<BookingRequest, BookingEntity>()
            .ForMember(d => d.Id, o => o.MapFrom(s => BookingId.For(s.FlightCode, s.PassportNumber)))
            .ForMember(d => d.Flight, o => o.MapFrom(s => s.FlightCode))
            .ForMember(d => d.Passport, o => o.MapFrom(s => s.PassportNumber))
            .ForMember(d => d.Seat, o => o.MapFrom(s => s.SeatPreference))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(_ => DateTimeOffset.UtcNow));

        var config = new AutoMapper.MapperConfiguration(
            mapperConfigExpression,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        _autoMapper = config.CreateMapper();

        // Guard: all three mappers must agree, otherwise the comparison is not like for like.
        var manual = MapByHand(_request);
        AssertMatches(BookingMapping.ToEntity(_request), manual, "AutoMappic");
        AssertMatches(_autoMapper.Map<BookingEntity>(_request), manual, "AutoMapper");
    }

    private static void AssertMatches(BookingEntity actual, BookingEntity expected, string name)
    {
        if (actual.Id != expected.Id || actual.Flight != expected.Flight ||
            actual.Passport != expected.Passport || actual.Seat != expected.Seat)
        {
            throw new InvalidOperationException(
                $"{name} output does not match the hand-written baseline; the comparison would be invalid.");
        }
    }

    [Benchmark(Baseline = true)]
    public BookingEntity HandWrittenMapping() => MapByHand(_request);

    [Benchmark]
    public BookingEntity AutoMappicMapping() => BookingMapping.ToEntity(_request);

    [Benchmark]
    public BookingEntity AutoMapperMapping() => _autoMapper.Map<BookingEntity>(_request);

    private static BookingEntity MapByHand(BookingRequest src) => new()
    {
        Id = BookingId.For(src.FlightCode, src.PassportNumber),
        Flight = src.FlightCode,
        Passport = src.PassportNumber,
        Seat = src.SeatPreference,
        CreatedAt = DateTimeOffset.UtcNow
    };
}

/// <summary>
/// Serialization: the MemoryPack binary format that Rapp uses for cache payloads
/// against System.Text.Json, which is the usual default for a distributed cache.
/// </summary>
[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private BookingRequest _request = null!;
    private byte[] _binaryPayload = null!;
    private byte[] _jsonPayload = null!;

    [GlobalSetup]
    public void Setup()
    {
        _request = new BookingRequest
        {
            FlightCode = "VA123",
            PassportNumber = "ABC123DEF",
            SeatPreference = "Window"
        };

        _binaryPayload = MemoryPackSerializer.Serialize(_request);
        _jsonPayload = JsonSerializer.SerializeToUtf8Bytes(_request);
    }

    /// <summary>Reported so payload-size claims can be checked rather than asserted.</summary>
    [Benchmark]
    public int BinaryPayloadBytes() => _binaryPayload.Length;

    [Benchmark]
    public int JsonPayloadBytes() => _jsonPayload.Length;

    [Benchmark(Baseline = true)]
    public byte[] JsonSerialize() => JsonSerializer.SerializeToUtf8Bytes(_request);

    [Benchmark]
    public byte[] BinarySerialize() => MemoryPackSerializer.Serialize(_request);

    [Benchmark]
    public BookingRequest? JsonDeserialize() => JsonSerializer.Deserialize<BookingRequest>(_jsonPayload);

    [Benchmark]
    public BookingRequest? BinaryDeserialize() => MemoryPackSerializer.Deserialize<BookingRequest>(_binaryPayload);
}

public class FluentBookingValidator : AbstractValidator<BookingRequest>
{
    public FluentBookingValidator()
    {
        RuleFor(x => x.FlightCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.PassportNumber).NotEmpty().MinimumLength(5);
        RuleFor(x => x.SeatPreference).NotEmpty();
    }
}

public class Program
{
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
