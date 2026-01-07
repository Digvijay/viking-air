# Viking Air

A production-ready demonstration of Sannr, Rapp, and Skugga working together in a cloud-native .NET 10 application.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Aspire](https://img.shields.io/badge/Aspire-13.1-512BD4)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-Ready-00C853)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

## Overview

Viking Air is a flight booking demonstration that showcases the **Viking AOT Suite** - a collection of three .NET libraries designed for Native AOT compilation:

- **Sannr**: Minimal validation via source generation.
- **Rapp**: Schema-safe binary caching with MemoryPack.
- **Skugga**: Native AOT-compatible mocking framework.

The application is orchestrated by **.NET Aspire** and features a React/Tailwind frontend.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│         .NET Aspire Dashboard (Port 17299)          │
│      Metrics • Traces • Logs • Health               │
└─────────────────┬───────────────────────────────────┘
                  │
    ┌─────────────┼─────────────┐
    │             │             │
┌───▼────┐   ┌────▼────┐   ┌───▼────┐
│ Redis  │   │   API   │   │  Web   │
│ Cache  │◄──┤ +Sannr  │◄──┤(Tailwind)
│(Docker)│   │ +Rapp   │   │ (Vite) │
37: └────────┘   │ +Aspire │   └────────┘
             └─────────┘
```

### Components

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **VikingAir.Api** | ASP.NET Core Minimal API | Backend with Sannr validation + Rapp caching |
| **VikingAir.Web** | React + Vite + Tailwind | Frontend user interface |
| **VikingAir.Core** | .NET Class Library | Shared models with `[RappCache]` + `[MemoryPackable]` |
| **VikingAir.AppHost** | .NET Aspire | Orchestrates all services + Redis |
| **VikingAir.EvolutionDemo** | Console App | Demonstrates Rapp's schema safety |
| **VikingAir.Benchmarks** | BenchmarkDotNet | Performance comparisons |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Redis)
- [Node.js 18+](https://nodejs.org/) (for the Web frontend)

### Run the Demo

```bash
cd viking-air
dotnet run --project VikingAir.AppHost
```

This command starts:
- Redis container
- VikingAir.Api (.NET 10 backend)
- VikingAir.Web (React frontend)
- Aspire Dashboard

### Access the Application

1. **Aspire Dashboard**: The console will show the URL (usually `https://localhost:17299`)
2. **Web App**: Select the `web` resource in the Aspire Dashboard
3. **API**: Select the `api` resource in the Aspire Dashboard

## Features

### Sannr Validation

Sannr provides zero-allocation validation using source generators.

```csharp
[Required(ErrorMessage = "Flight code is required")]
[StringLength(10, MinimumLength = 3)]
[RegularExpression(@"^[A-Z]{2}\d{3,4}$")]
public string FlightCode { get; set; } = "";
```

- Significantly faster than DataAnnotations
- Reduced memory usage
- Compile-time code generation

### Rapp Caching

Rapp enables schema-safe binary caching.

```csharp
[RappCache]
[MemoryPackable]
public partial class BookingRequest
{
    // Properties...
}
```

- Schema hash prevents deserialization of incompatible data
- Binary serialization using MemoryPack
- Native AOT compatible

### Observability

Integration with .NET Aspire provides:
- Metrics
- Distributed tracing
- Structured logs
- Health checks

## Testing the Demo

### Valid Booking Request

```json
{
  "flightCode": "VA123",
  "passportNumber": "ABC123DEF",
  "seatPreference": "Window"
}
```

### Invalid Booking Request

```json
{
  "flightCode": "INVALID",
  "passportNumber": "X",
  "seatPreference": "BadSeat"
}
```

## Performance

To run the benchmarks:

```bash
dotnet run -c Release --project VikingAir.Benchmarks
```

## Build and Deployment

### Native AOT Build

```bash
cd VikingAir.Api
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishAot=true
```

### Docker (via Aspire)

Aspire can generate Docker Compose files:

```bash
dotnet run --project VikingAir.AppHost -- --publisher manifest
```

## Project Structure

```
viking-air/
├── VikingAir.Api/              # ASP.NET Core Minimal API
│   └── Program.cs              # Sannr + Rapp integration
├── VikingAir.Core/             # Shared models
│   └── BookingRequest.cs       # [RappCache] + [MemoryPackable]
├── VikingAir.Web/              # React + Vite + Tailwind
│   ├── src/
│   │   ├── App.tsx             # Main UI component
│   │   └── index.css           # Tailwind styles
│   └── package.json
├── VikingAir.AppHost/          # Aspire orchestration
│   └── Program.cs              # Service configuration
├── VikingAir.EvolutionDemo/    # Rapp schema safety demo
├── VikingAir.Benchmarks/       # Performance comparisons
└── README.md                   # This file
```

## Contributing

This is a demonstration project showcasing the Viking AOT Suite. For issues or contributions to the individual libraries, please visit their respective repositories:

- **Sannr**: [https://github.com/Digvijay/Sannr](https://github.com/Digvijay/Sannr)
- **Rapp**: [https://github.com/Digvijay/Rapp](https://github.com/Digvijay/Rapp)
- **Skugga**: [https://github.com/Digvijay/Skugga](https://github.com/Digvijay/Skugga)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

