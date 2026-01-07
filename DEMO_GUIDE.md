# Viking Air - Demonstration Guide

This guide provides steps to run and demonstrate the capabilities of the Viking AOT Suite.

## Quick Start

```bash
cd viking-air
dotnet run --project VikingAir.AppHost
```

This will start:
- Redis container
- VikingAir.Api
- VikingAir.Web
- Aspire Dashboard

## Demonstration Walkthrough

### 1. Introduction to the Problem

Traditional .NET validation often relies on reflection, which can be slow and allocate memory. Caching mechanisms can also cause issues when schemas change if not handled correctly.

- **File**: `VikingAir.Core/BookingRequest.cs`
- **Observation**: Note the standard `[Required]` and `[StringLength]` attributes.

### 2. The Solution (Viking Suite)

Viking Air utilizes:
- **Sannr**: For zero-allocation validation.
- **Rapp**: For schema-safe binary caching.

- **File**: `VikingAir.Core/BookingRequest.cs`
- **Observation**: Note the `[RappCache]` and `[MemoryPackable]` attributes.

### 3. Running the Application

1. **Start Aspire**: Run the project using the command above.
2. **Aspire Dashboard**: Open the URL displayed in the console (typically `https://localhost:17299`).
3. **Web Interface**: Select the `web` resource to open the frontend.

### 4. Validating Requests

**Valid Request:**
- Flight Code: `VA123`
- Passport: `ABC123DEF`
- Seat: `Window`
- **Result**: Success message indicating validation by Sannr and caching by Rapp.

**Invalid Request:**
- Flight Code: `INVALID`
- **Result**: Immediate validation errors displayed in the UI.

### 5. Observability

1. Navigate to the **Aspire Dashboard**.
2. Select the **Traces** tab.
3. Locate the recent booking request to view the distributed trace (Web -> API -> Redis).

### 6. Schema Safety Demonstration

Run the evolution demo to see how Rapp handles schema changes:

```bash
dotnet run --project VikingAir.EvolutionDemo
```

This demonstrates how Rapp uses schema hash comparison to prevent deserialization of incompatible data.

### 7. Performance Benchmarks

Run the benchmarks to compare Sannr against standard DataAnnotations:

```bash
dotnet run -c Release --project VikingAir.Benchmarks
```

## Technical Points

- **Source Generation**: View generated code in `obj/Debug/net10.0/generated`.
- **Native AOT**: The application is compatible with Native AOT, offering faster startup and smaller binaries.
- **Aspire**: Orchestrates the microservices, Redis, and provides OpenTelemetry support.

## Troubleshooting

- **Web App**: Ensure Node.js dependencies are installed (`cd VikingAir.Web && npm install`).
- **Redis**: Ensure Docker Desktop is running.
- **Ports**: Check the console output if default ports are in use.
