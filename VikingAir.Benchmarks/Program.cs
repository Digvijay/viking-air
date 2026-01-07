using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FluentValidation;
using VikingAir.Core;
using System.ComponentModel.DataAnnotations;

namespace VikingAir.Benchmarks;

[MemoryDiagnoser]
public class ValidationBenchmarks
{
    private BookingRequest _request = null!;
    private FluentBookingValidator _fluentValidator = null!;

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
    }

    [Benchmark(Baseline = true)]
    public object FluentValidation()
    {
        return _fluentValidator.Validate(_request);
    }

    [Benchmark]
    public object DataAnnotationsValidation()
    {
        // Standard DataAnnotations validation (uses reflection)
        var context = new ValidationContext(_request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(_request, context, results, validateAllProperties: true);
        return results;
    }

    // Note: Sannr generates optimized validation code at compile-time
    // The actual performance gain is seen when using Sannr's generated validators
    // in a real ASP.NET Core application with AddSannr() registered
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
    {
        BenchmarkRunner.Run<ValidationBenchmarks>();
    }
}
