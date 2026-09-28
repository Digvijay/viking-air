# Viking Air benchmarks

These are the numbers this repository actually produced. They are reproducible with the command
below. Nothing in the README or the demo guide should quote a figure that does not appear here.

## How to reproduce

```bash
dotnet run --configuration Release --project VikingAir.Benchmarks -- --filter '*'
```

A `--job dry` run only checks that the benchmarks execute and that their correctness guards
hold. It produces no usable timings.

## Environment

| | |
|---|---|
| BenchmarkDotNet | 0.15.8 |
| OS | Windows 11 (10.0.26200.9457 / 25H2) |
| CPU | Snapdragon X Elite X1E80100, 12 physical / 12 logical cores, 3.40 GHz |
| Architecture | **ARM64**, RyuJIT armv8.0-a |
| Runtime | .NET 10.0.12 |
| SDK | 10.0.401 |
| Job | default (mapping runs on `InProcessEmitToolchain`, see below) |

This is a developer laptop, not a server. Treat the ratios as indicative of relative cost and
re-measure on your target hardware before using any of this for capacity planning. The ARM64
result is the more relevant one for Azure Cobalt and other ARM64 hosts.

## Validation

`BookingRequest` validated once per operation.

| Method | Mean | Ratio | Allocated | Alloc ratio |
|---|---:|---:|---:|---:|
| FluentValidation (baseline) | 164.51 ns | 1.00 | 696 B | 1.00 |
| DataAnnotations | 366.83 ns | 2.23 | 1224 B | 1.76 |
| **Sannr** | **56.59 ns** | **0.34** | **256 B** | **0.37** |

Sannr is roughly **2.9x faster than FluentValidation** and **6.5x faster than DataAnnotations**,
and allocates **63% less** than FluentValidation.

The benchmark fails at `[GlobalSetup]` if no Sannr validator is registered for the model, or if a
known-invalid model is accepted. Without those guards a mis-registered validator would silently
do nothing and report an excellent time.

## Mapping

`BookingRequest` to `BookingEntity`, five members, one of them computed.

| Method | Mean | Ratio | Allocated | Alloc ratio |
|---|---:|---:|---:|---:|
| Hand-written (baseline) | 56.21 ns | 1.00 | 136 B | 1.00 |
| **AutoMappic** | **68.64 ns** | **1.22** | **208 B** | **1.53** |
| AutoMapper | 152.81 ns | 2.72 | 136 B | 1.00 |

Read this honestly:

- AutoMappic is **2.2x faster than AutoMapper**, the reflection-based incumbent it exists to
  replace, and unlike AutoMapper it works under Native AOT.
- AutoMappic is **22% slower than hand-written code**. It does not beat a hand-written mapper and
  should not be described as if it does.
- AutoMappic **allocates 53% more than both** the hand-written mapper and AutoMapper. For a
  compile-time mapper that emits a direct projection this is unexpected, and it works against the
  memory-density argument. This is an open optimisation item for AutoMappic, not a property of
  the approach.

All three mappers are asserted to produce identical output in `[GlobalSetup]`.

### Toolchain note

`MappingBenchmarks` runs on `InProcessEmitToolchain`. With BenchmarkDotNet's default toolchain,
the AutoMappic generator runs inside the generated host project and emits an
`AutoMappic.Registration.g.cs` that does not compile (`CS0116`, `CS1106`, `CS0548`), so the
benchmark cannot start. The in-process toolchain reuses the already-compiled assembly and avoids
re-running the generator. This is a workaround for an upstream defect; these numbers are not
process-isolated the way the other two classes are.

## Serialization

The MemoryPack binary format Rapp uses for cache payloads, against System.Text.Json.

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| JsonSerialize (baseline) | 111.22 ns | 1.00 | 104 B |
| **BinarySerialize** | **43.40 ns** | **0.39** | 72 B |
| JsonDeserialize | 206.93 ns | 1.86 | 152 B |
| **BinaryDeserialize** | **47.87 ns** | **0.43** | 152 B |

Binary serialization is about **2.6x faster** than JSON and deserialization about **4.3x faster**,
on a small payload. Larger or more deeply nested payloads will shift these ratios.

## Native AOT runtime facts

Measured on the same machine, publishing `VikingAir.Api` with `-p:PublishAot=true` for
`win-arm64` and exercising the endpoint:

| | |
|---|---|
| Native binary size | 22.4 MB, self-contained (no runtime install required) |
| Time to "Application started" | ~1.8 s, measured from process start including host startup |
| Working set after first request | ~29 MB |

These are single-sample figures from a developer laptop and include a Redis connection attempt.
They are recorded to show the AOT path works end to end, not as a capacity-planning input. A
proper density argument needs a JIT-versus-AOT comparison on the target host, which has not been
taken.

Functional verification of the published native binary:

- a payload violating every rule is rejected with HTTP 400 and per-field messages from Sannr
- a valid payload is sanitised in place (`"  va777 "` becomes `"VA777"`)
- the response carries only a three-character passport suffix, never the full number

## What these numbers do not show

- No sustained-load, multi-tenant, or container-density measurement.
- No JIT-versus-AOT comparison of startup time and steady-state working set for the same
  application. That is the measurement that would most directly support the sustainability
  claim, and it has not been taken yet.
- Single hardware configuration, single run, developer laptop.
