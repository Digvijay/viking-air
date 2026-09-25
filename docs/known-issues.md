# Known issues found by integrating the four libraries

Viking Air exists to prove AutoMappic, Sannr, Rapp, and Skugga compose in a realistic
application. Building it surfaced defects that none of the individual repositories' own tests or
samples catch, because each library is exercised there in isolation and in a single assembly.

Every item below was reproduced in this repository. Each one states what was observed, what it
blocks, and the workaround currently applied here.

---

## 1. Sannr: ASP.NET Core integration does not enforce validation

**Severity: high. This is a security-relevant fail-open.**

`WithSannrValidation` does not reject invalid payloads. A request violating every rule on
`BookingRequest`:

```json
{ "flightCode": "X", "passportNumber": "ab", "seatPreference": "Balcony" }
```

was answered `200 Confirmed`, with the invalid values echoed back and cached.

Verified against Sannr 1.6.0 with both public entry points:

- `SannrEndpointExtensions.WithSannrValidation(RouteGroupBuilder)`
- `RouteHandlerBuilderExtensions.WithSannrValidation(RouteHandlerBuilder)`

and under both JIT and Native AOT. All four combinations accepted the payload.

The generated validator itself is correct. `VikingAir.Tests/ValidationTests.cs` resolves it from
`SannrValidatorRegistry` and proves it rejects exactly these payloads and applies `[Sanitize]`.
The defect is in the ASP.NET Core integration layer, not the generator.

**Why this is worse than an ordinary bug:** `SannrValidatorRegistry.ValidateAsync` returns
success when no validator is registered for a type. A registration or wiring failure therefore
presents as "everything is valid" rather than as an error. A consumer who trusts the filter gets
an API with no input validation and no signal that anything is wrong.

**Suggested upstream fixes:**

1. Make an unregistered type fail closed, or require opt-in via something like
   `AddSannr(o => o.AllowUnvalidatedTypes = true)`.
2. Have `WithSannrValidation` throw at startup if it cannot find a validator for the endpoint's
   bound parameter types, rather than silently registering a filter that passes everything.
3. Add an integration test that asserts a known-invalid payload produces HTTP 400 through the
   real ASP.NET Core pipeline. A unit test over the generated validator cannot catch this.

**Workaround here:** `VikingAir.Api/Program.cs` does not use the filter. It resolves the
generated validator from the registry at startup, throws if it is absent, and validates
explicitly in the handler, returning `ValidationProblem` on failure.

---

## 2. Sannr: `WithSannrValidation` is ambiguous between two extension classes

Sannr 1.6.0 exposes `WithSannrValidation` for the same receiver type from both
`RouteHandlerBuilderExtensions` and `SannrEndpointExtensions`. Calling it in extension-method
form fails to compile:

```
error CS0121: The call is ambiguous between the following methods or properties
```

Consumers must call it through the declaring type, which defeats the point of an extension
method. One of the two overload sets should be removed or renamed.

---

## 3. Sannr: ships a vulnerable transitive dependency

Sannr 1.6.0 resolves `Microsoft.OpenApi` 2.4.1, which carries the High-severity advisory
**GHSA-v5pm-xwqc-g5wc**. Any consumer running `dotnet list package --vulnerable --include-transitive`
inherits the finding.

Pinning `Microsoft.OpenApi` 2.12.2 in the consuming projects clears it, which is what this
repository does. Sannr should raise its version floor so consumers do not have to.

---

## 4. AutoMappic: the `Profile` / `IMapper` API is not AOT-safe

Publishing `VikingAir.Api` with `PublishAot=true` produces IL2026 and IL3050 warnings from
AutoMappic's own API surface:

```
warning IL3050: Using member 'AutoMappic.IMapper.Map<TDestination>(Object)' which has
'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. Object mapping via
IMapper interface requires dynamic code generation if not intercepted by the source generator.

warning IL3050: Using member 'AutoMappic.Profile.CreateMap<TSource, TDestination>()' ...
Runtime mapping configuration requires dynamic code generation.
```

`MapperConfiguration`, `Profile`, `CreateMap`, `ForMember`, and `IMapper.Map<T>` are all
annotated as requiring dynamic code. The published binary does work, so the interceptor is
evidently applying, but the annotations mean every AOT consumer of the documented API gets
trim and AOT warnings, and anyone treating IL3050 as an error cannot build at all.

This matters for positioning: AutoMappic's headline claim is mapping without reflection, and its
most discoverable API is the AutoMapper-compatible one that is marked as needing reflection.
AutoMappic's own `samples/AotBenchmark` uses this same API.

**Suggested upstream fixes:** either annotate the intercepted paths so the warnings do not fire
when the generator handles the call, or document a distinct AOT-safe API and mark the
reflection-based one clearly as the compatibility shim.

---

## 5. AutoMappic: generated registration is `internal`, breaking cross-assembly use

The generator runs in every project that references an assembly containing mapping
configuration, and emits a registration that calls the declaring assembly's generated
`<Assembly>_Registration` class. That class is declared `internal`, so every consuming project
fails to compile:

```
error CS0122: 'VikingAir_Core_Registration' is inaccessible due to its protection level
```

This blocks the ordinary layering where models and mapping live in a Core library and the API
references it.

**Workaround here:** `VikingAir.Core/AssemblyInfo.cs` grants `InternalsVisibleTo` to every
consuming project. That is not a reasonable requirement to place on consumers.

**Suggested upstream fix:** emit the registration class as `public`, or emit a public
registration entry point.

---

## 6. AutoMappic: `PrivateAssets="all"` produces a runtime failure, not a compile error

The obvious way to stop the generator flowing downstream is `PrivateAssets="all"` on the
`PackageReference`. That also withholds `AutoMappic.Core.dll`, which the generated mapping code
needs at run time. Downstream projects then compile successfully and fail when the mapping
executes:

```
System.IO.FileNotFoundException: Could not load file or assembly 'AutoMappic.Core,
Version=0.7.0.0'
```

A build-time misconfiguration that only appears at run time is a poor failure mode. Splitting
the runtime library into its own package, or shipping the generator under `buildTransitive/`
with a clear contract, would avoid it.

---

## 7. AutoMappic: generator emits uncompilable code in BenchmarkDotNet host projects

Running `MappingBenchmarks` under BenchmarkDotNet's default toolchain fails to build the
generated host project:

```
error CS0116: A namespace cannot directly contain members such as fields, methods or statements
error CS1106: Extension method must be defined in a non-generic static class
error CS0548: property or indexer must have at least one accessor
```

The errors are inside AutoMappic's own `AutoMappic.Registration.g.cs`. The generator emits
invalid C# for this compilation shape, which is a project with top-level statements that
references an assembly carrying mapping configuration.

**Workaround here:** `MappingBenchmarks` uses `InProcessEmitToolchain`, which reuses the
already-compiled assembly and does not re-run the generator. This costs process isolation for
those benchmarks.

---

## 8. AutoMappic: allocates more than both alternatives

From [docs/benchmarks.md](benchmarks.md), mapping a five-member object:

| Method | Mean | Allocated |
|---|---:|---:|
| Hand-written | 56.21 ns | 136 B |
| AutoMappic | 68.64 ns | 208 B |
| AutoMapper | 152.81 ns | 136 B |

AutoMappic is comfortably faster than AutoMapper but allocates 53% more than both it and the
hand-written baseline. For a compile-time mapper emitting a direct projection, 72 bytes of
overhead per map is unexpected and is worth investigating, particularly because reduced memory
pressure is part of the density argument these libraries are sold on.

---

## 9. Viking Air: the test project was not in the solution

`VikingAir.Tests` was absent from `VikingAir.sln`. `dotnet test VikingAir.sln` therefore
discovered nothing, exited 0, and reported success while running no tests. The CI workflow's
test step was passing for the same reason.

Fixed by adding the project to the solution. The suite now has 15 tests covering validation,
mapping, cache round-tripping, and the Skugga payment-gateway double.

---

## 10. Viking Air: the CI workflow could never have worked

The previous `viking-air-ci.yml` installed **.NET 8** for projects targeting `net10.0`, ran
`dotnet workload install buildtools` which is not a real workload, and asserted the existence of
an AOT binary at a `bin/Release/net8.0/...` path that cannot exist for this solution.

Replaced by `ci.yml`, `aot-validation.yml`, and `ospo-compliance.yml`.

---

## Summary

| # | Library | Issue | Severity |
|---|---|---|---|
| 1 | Sannr | ASP.NET Core integration fails open | High |
| 2 | Sannr | Ambiguous `WithSannrValidation` overloads | Low |
| 3 | Sannr | Vulnerable transitive `Microsoft.OpenApi` | Moderate |
| 4 | AutoMappic | Public API annotated `RequiresDynamicCode` | High |
| 5 | AutoMappic | `internal` registration blocks cross-assembly use | High |
| 6 | AutoMappic | `PrivateAssets="all"` fails at run time | Moderate |
| 7 | AutoMappic | Invalid code emitted in BenchmarkDotNet projects | Moderate |
| 8 | AutoMappic | Higher allocation than alternatives | Moderate |
| 9 | viking-air | Tests not in solution, silently not run | High |
| 10 | viking-air | CI workflow could never pass meaningfully | High |

Rapp and Skugga produced no defects during this integration. Rapp's binary caching and schema
guard behaved as documented, and Skugga's compile-time test doubles worked under Native AOT
without incident.
