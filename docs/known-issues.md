# Defects found by integrating the libraries

Viking Air exists to prove AutoMappic, Sannr, Rapp, Skugga, and Prova compose in a realistic
application. Building it surfaced twenty-five defects that none of the individual repositories' own
tests or samples caught, because each library is exercised there in isolation, in a single
assembly, on a single target framework, in a single locale.

That is the point of this repository, and it is the main argument for treating these six as one
programme rather than six unrelated projects. **Twenty-two of the twenty-five are now fixed**, and
each fix is verified by a test that fails without it.

Every item below states what was observed, what it blocked, how it was fixed, and how the fix is
proven. Items that remain open say so plainly.

| # | Library | Issue | Severity | Status |
|---|---|---|---|---|
| 1 | Sannr | ASP.NET Core integration failed open | High | **Fixed** |
| 2 | Sannr | Ambiguous `WithSannrValidation` overloads | Low | **Fixed** |
| 3 | Sannr | Vulnerable transitive `Microsoft.OpenApi` | High | **Fixed** |
| 4 | Sannr | `global.json` made the repo unbuildable | High | **Fixed** |
| 5 | Sannr | Dead `TestGenerator` shipped to consumers | Moderate | **Fixed** |
| 6 | AutoMappic | `internal` registration blocked cross-assembly use | High | **Fixed** |
| 7 | AutoMappic | Invalid code for hyphenated assembly names | High | **Fixed** |
| 8 | AutoMappic | Interceptors lost for transitive consumers | High | **Fixed** |
| 9 | AutoMappic | Test suite could not run on the .NET 10 SDK | Moderate | **Fixed** |
| 10 | AutoMappic | Public API annotated `RequiresDynamicCode` | High | Open |
| 11 | AutoMappic | `PrivateAssets="all"` fails at run time | Moderate | Open, documented |
| 12 | AutoMappic | Higher allocation than alternatives | Moderate | Open, measured |
| 15 | Skugga | Culture-dependent literals silently voided mock setups | High | **Fixed** |
| 16 | Skugga | OpenAPI generator emitted uncompilable code | High | **Fixed** |
| 17 | Rapp | Vulnerable transitive `Microsoft.OpenApi` | High | **Fixed** |
| 18 | Skugga | Packaging: unpackable solution, samples published, broken props chain | Moderate | **Fixed** |
| 19 | Rapp | Benchmark and dashboard published as NuGet packages | Moderate | **Fixed** |
| 20 | AutoMappic | CLI test project was in no solution and had never run | High | **Fixed** |
| 21 | AutoMappic | CLI and benchmarks targeted `net9.0`, not an LTS release | Moderate | **Fixed** |
| 22 | AutoMappic | Two transitive packages carried high-severity advisories | High | **Fixed** |
| 23 | Rapp | Tests ran only on `net10.0` while the package shipped `net8.0` | High | **Fixed** |
| 24 | Skugga | OpenAPI tests ran only on `net8.0` | Moderate | **Fixed** |
| 25 | Viking Air | `MessagePack` 2.5.192 carried eleven advisories | High | **Fixed** |

Two further defects were found in this repository itself (13 and 14) and are described at the end.

---

# Fixed

## 1. Sannr: ASP.NET Core integration did not enforce validation

**Severity: high. A security-relevant fail-open.**

`WithSannrValidation` did not reject invalid payloads. A request violating every rule on
`BookingRequest`:

```json
{ "flightCode": "X", "passportNumber": "ab", "seatPreference": "Balcony" }
```

was answered `200 Confirmed`, with the invalid values echoed back and cached. Reproduced against
Sannr 1.6.0 through both public entry points, under both JIT and Native AOT. All four
combinations accepted the payload.

**Root cause.** Two unrelated classes were both named `SannrValidatorRegistry`:

* `Sannr.SannrValidatorRegistry` — the real one, populated by the generator via
  `[ModuleInitializer]`.
* `Sannr.AspNetCore.SannrValidatorRegistry` — a second, separate class that nothing ever wrote to.

C# resolves an unqualified name against the containing namespace first, so every lookup written
inside `Sannr.AspNetCore` bound to the *empty* registry. The filter found no validator for the
request type, and `ValidateAsync` returned success when no validator was registered. The result
was an API with no input validation and no signal that anything was wrong.

This is the defect that most justifies the integration project. The generated validator was always
correct, and Sannr's unit tests proved it by calling the registry directly — which is precisely why
they could not see the bug. Only an end-to-end HTTP request through the real ASP.NET Core pipeline
could.

**Fixed in Sannr 1.7.0.** Registry lookups are fully qualified; the duplicate filter and extension
classes are deleted; the shadow registry is now an `[Obsolete]` forwarder that *throws* for an
unregistered type instead of reporting success. `WithSannrValidation` additionally fails closed: at
startup it verifies a generated validator exists for each parameter it is asked to guard and throws
if one is missing, controlled by `SannrValidationOptions.RequireValidator` (default `true`).

**Proof.** `VikingAir.Api/Program.cs` no longer contains a workaround — it calls the real
`app.MapGroup("/api").WithSannrValidation()`. Posting the payload above to the running API now
returns **HTTP 400** with the individual field errors. Sannr also gained seven HTTP-level
integration tests (`tests/Sannr.Tests/EndpointFilterIntegrationTests.cs`) that assert this through
a real request pipeline.

## 2. Sannr: `WithSannrValidation` was ambiguous between two extension classes

Sannr 1.6.0 exposed `WithSannrValidation` for the same receiver type from both
`RouteHandlerBuilderExtensions` and `SannrEndpointExtensions`, so calling it in extension-method
form failed to compile with `CS0121: The call is ambiguous`. Consumers had to call it through the
declaring type, defeating the purpose of an extension method.

**Fixed** as part of item 1 — the duplicate class was one half of the same namespace-shadowing
mistake, and removing it resolved both defects.

## 3. Sannr: shipped vulnerable transitive dependencies

Sannr 1.6.0 resolved `Microsoft.OpenApi` 2.4.1, carrying the **High**-severity
**GHSA-v5pm-xwqc-g5wc**. Any consumer running
`dotnet list package --vulnerable --include-transitive` inherited the finding.

Raising the floor surfaced a second advisory that had been masked by the first: `OpenTelemetry`
1.14.0, **Moderate**, **GHSA-g94r-2vxg-569j**.

**Fixed in Sannr 1.7.0:** floors raised to `Microsoft.OpenApi` 2.12.2 and OpenTelemetry 1.18.0.
`dotnet list package --vulnerable --include-transitive` is now clean across the solution. The
version entries carry comments naming the advisory so they are not casually lowered.

A third advisory, **GHSA-23fw-v26w-5fgq**, reached AutoMappic, Rapp, and Skugga through an explicit
`Microsoft.SourceLink.GitHub` reference. Source Link has been built into the .NET SDK since .NET 8,
so the reference was redundant; removing it eliminated the advisory in all three repositories.

## 4. Sannr: `global.json` made the repository unbuildable

Sannr's `global.json` combined a `latestPatch` roll-forward policy with `allowPrerelease: false`
pinned to an SDK band that is no longer installed on current machines. The repository could not be
built on **any** currently shipping SDK — a contributor cloning it would get an SDK resolution
error before reaching a single line of code.

**Fixed.** `global.json` is normalised across all five repositories to a policy that accepts the
supported SDK band.

## 5. Sannr: a dead `TestGenerator` shipped to every consumer

`Sannr.Gen` contained a leftover non-incremental `ISourceGenerator` named `TestGenerator`. It was
packed into the shipped analyzer and injected a dead generated file into *every* consuming
compilation, and it violated RS1035/RS1042. Newer analyzers surfaced it once the toolchain was
updated.

**Fixed:** deleted. Separately, the generator targeted `netstandard2.1`, which Roslyn does not
support for compiler extensions (RS1041) and which makes generator behaviour depend on the host
compiler version. It now targets `netstandard2.0`.

## 6. AutoMappic: generated registration was `internal`

The generator runs in every project that references an assembly containing mapping configuration
and emits a registration that calls the declaring assembly's `<Assembly>_Registration` class. That
class was `internal`, so every consuming project failed to compile:

```
error CS0122: 'VikingAir_Core_Registration' is inaccessible due to its protection level
```

This blocked the ordinary layering where models and mapping live in a Core library that the API
references — that is, essentially every non-trivial application.

**Fixed:** the registration class is emitted `public`, marked
`[EditorBrowsable(EditorBrowsableState.Never)]` so it stays out of IntelliSense.

**Proof:** `VikingAir.Core/AssemblyInfo.cs`, which existed only to grant `InternalsVisibleTo` to
every consuming project, has been **deleted**, and the solution still builds.

## 7. AutoMappic: invalid code generated for hyphenated assembly names

**This was originally recorded as "generator emits uncompilable code in BenchmarkDotNet host
projects". Investigation showed the real cause is much broader.**

The generator derives C# identifiers from the compilation's assembly name. Its sanitiser replaced
only a hardcoded list of characters — one that did not include `-`. A hyphen is legal in an
assembly name and illegal in a C# identifier, so a project named `my-app` emitted:

```csharp
public static class AutoMappic_Extension_my-app
```

which the compiler parsed as a subtraction expression:

```
error CS0116: A namespace cannot directly contain members such as fields, methods or statements
error CS1106: Extension method must be defined in a non-generic static class
error CS0548: property or indexer must have at least one accessor
```

Any project whose assembly name contains a hyphen could not consume AutoMappic at all. Hyphenated
assembly names are common. And because BenchmarkDotNet names its generated host assembly
`<Project>-<Job>-<N>`, *every* BenchmarkDotNet project that referenced AutoMappic failed to build —
which is how the defect was found.

**Fixed:** sanitisation now guarantees a valid identifier for every input and prefixes a leading
digit.

**Proof:** `VikingAir.Benchmarks` no longer uses the `InProcessEmitToolchain` workaround and runs
on BenchmarkDotNet's default out-of-process toolchain, restoring process isolation for the mapping
benchmarks. AutoMappic gained
`tests/AutoMappic.Tests/AssemblyNameSanitisationTests.cs`, which parses the generated registration
for `my-app`, `VikingAir.Benchmarks-DefaultJob-1`, `Contoso.Api-v2`, and `7Eleven.Api` and requires
it to be syntactically valid C#.

## 8. AutoMappic: interceptors silently disabled for transitive consumers

AutoMappic's interception depends on an MSBuild property that must name the generated namespace.
Two problems:

* The property was packed only to `build/`, not `buildTransitive/`. A project that picked up
  AutoMappic transitively never got the property, so interception silently did not apply and calls
  fell back to the reflection-based path — slower, allocating, and AOT-unsafe, with no diagnostic.
* The property name changed between language versions. C# 12 / .NET 8 uses
  `InterceptorsPreviewNamespaces`; C# 13 / .NET 9+ uses `InterceptorsNamespaces` and raises
  **CS9137** if the preview property is also set. Setting both breaks .NET 9+; setting either alone
  breaks the other.

**Fixed:** `AutoMappic.targets` selects the correct property by target framework version and is
packed to both `build/` and `buildTransitive/`.

**Silent performance degradation is the worst failure mode for a library whose entire value
proposition is measured in nanoseconds and bytes.**

## 9. AutoMappic: the test suite could not run on the .NET 10 SDK

`dotnet test` on the .NET 10 SDK refuses the legacy VSTest bridge for Microsoft.Testing.Platform
projects, so the suite did not run at all — it reported no failures because it executed no tests.

**Fixed** by opting in via `global.json`. Once the tests ran, they immediately exposed a further
defect: a value converter formatted currency with the ambient culture, so the suite failed on any
locale with a non-US decimal separator (found on a Swedish machine, where `$99.99` became
`$99,99`). Fixed to use `InvariantCulture`.

---

# Open

## 10. AutoMappic: the `Profile` / `IMapper` API is not AOT-safe

Publishing `VikingAir.Api` with `PublishAot=true` produces IL2026 and IL3050 warnings from
AutoMappic's own API surface:

```
warning IL3050: Using member 'AutoMappic.IMapper.Map<TDestination>(Object)' which has
'RequiresDynamicCodeAttribute' can break functionality when AOT compiling.
```

`MapperConfiguration`, `Profile`, `CreateMap`, `ForMember`, and `IMapper.Map<T>` are all annotated
as requiring dynamic code. The published binary does work, so the interceptor is evidently
applying, but every AOT consumer of the documented API gets trim and AOT warnings, and anyone
treating IL3050 as an error cannot build at all.

This matters for positioning: AutoMappic's headline claim is mapping without reflection, and its
most discoverable API is the AutoMapper-compatible one that is marked as needing reflection.
AutoMappic's own `samples/AotBenchmark` uses this same API.

**Why it is still open:** the fix is a design decision, not a defect repair. Either the intercepted
paths are annotated so the warnings do not fire when the generator handles the call, or a distinct
AOT-safe API is documented and the reflection-based one is marked clearly as a compatibility shim.
Both are breaking changes to the public surface and should be made deliberately rather than
alongside a batch of bug fixes.

## 11. AutoMappic: `PrivateAssets="all"` produces a runtime failure, not a compile error

The obvious way to stop the generator flowing downstream is `PrivateAssets="all"` on the
`PackageReference`. That also withholds `AutoMappic.Core.dll`, which the generated mapping code
needs at run time. Downstream projects compile successfully and fail when the mapping executes:

```
System.IO.FileNotFoundException: Could not load file or assembly 'AutoMappic.Core'
```

**Why it is still open:** `PrivateAssets="all"` also suppresses the package's MSBuild targets
import, so the library cannot emit a build-time diagnostic — by the time the misconfiguration
exists, the code that would warn about it has been excluded. The durable fix is to split the
runtime library into its own package so the generator can be made private without taking the
runtime with it. That is a packaging change with migration consequences for existing consumers.
Documented here and in AutoMappic's README as a known sharp edge.

## 12. AutoMappic: allocates more than both alternatives

From [docs/benchmarks.md](benchmarks.md), mapping a five-member object:

| Method | Mean | Allocated |
|---|---:|---:|
| Hand-written | 56.21 ns | 136 B |
| AutoMappic | 68.64 ns | 208 B |
| AutoMapper | 152.81 ns | 136 B |

AutoMappic is comfortably faster than AutoMapper but allocates 53% more than both it and the
hand-written baseline. For a compile-time mapper emitting a direct projection, 72 bytes of overhead
per map is unexpected, and it matters specifically because reduced memory pressure is central to
the density argument these libraries are sold on.

**Why it is still open:** this is an optimisation with a real risk of changing generated-code
semantics, and it needs the allocation traced to a specific emitted construct before anything is
changed. Now that item 7 is fixed, the benchmarks run under BenchmarkDotNet's default
out-of-process toolchain with a memory diagnoser, so the measurement is trustworthy enough to
investigate against. It is tracked rather than hidden.

---

# Defects in this repository

## 13. The test project was not in the solution

`VikingAir.Tests` was absent from `VikingAir.sln`. `dotnet test VikingAir.sln` therefore discovered
nothing, exited 0, and reported success while running no tests. The CI workflow's test step was
passing for exactly that reason.

**Fixed** by adding the project to the solution. The suite now has 15 tests covering validation,
mapping, cache round-tripping, and the Skugga payment-gateway double.

## 14. The CI workflow could never have worked

The previous `viking-air-ci.yml` installed **.NET 8** for projects targeting `net10.0`, ran
`dotnet workload install buildtools` — not a real workload — and asserted the existence of an AOT
binary at a `bin/Release/net8.0/...` path that cannot exist for this solution.

**Fixed:** replaced by `ci.yml`, `aot-validation.yml`, and `ospo-compliance.yml`. `ci.yml` builds
and tests on the supported matrix and carries an advisory preview leg. It sets `fetch-depth: 0`,
which is required rather than cosmetic: Nerdbank.GitVersioning computes version height by walking
history and fails the build outright on a shallow clone.

---

## A note on how Rapp and Skugga were cleared

An earlier revision of this document said Rapp and Skugga "produced no functional defects during
this integration". **That was wrong, and the way it was wrong is worth recording.**

It was true only because the integration had been validated by building and testing each library's
*primary test project*. Running `dotnet build` and `dotnet test` across the **entire solution** —
samples, playgrounds and secondary test projects included — for the first time surfaced three
further defects immediately, two of them high severity. The claim had been made from an incomplete
gate, not from evidence.

The lesson is the same one the rest of this document makes: a green check mark is only worth the
scope it actually covered. Those three defects are 15, 16 and 17 below.

---

# Found in the second pass

These were found by building and testing the full solutions rather than the primary test projects,
and by the fact that the machine running the build is configured for a Swedish locale.

## 15. Skugga: culture-dependent literals silently voided mock setups

**Severity: high. A silent wrong-answer in a testing library.**

Three tests in `samples/AspNetCoreWebApi.Moq.Migration/Step2.WithSkugga.Tests` failed. Every
failure looked the same: a configured mock returned `default` instead of the configured value, as
though `Setup` had never been called. No exception, no compiler warning, no diagnostic.

Confirmed pre-existing — reproduced against a `git worktree` at the original commit, so it was not
introduced by the multi-targeting work.

**Root cause.** `GeneratorHelpers.FormatConstantValue` formatted numeric literals with
culture-sensitive string interpolation:

```csharp
if (value is decimal m) return $"{m}m";   // <-- ambient culture
```

Generated source is **C# code**, and C# numeric literals are defined in terms of the invariant
culture regardless of the culture the compiler runs under. On a machine using `,` as the decimal
separator, a setup written as:

```csharp
mock.Setup(x => x.CalculateDiscount(999.99m, "Electronics")).Returns(99.99m);
```

emitted the argument array as:

```csharp
new object?[] { 999,99m, "Electronics" }   // three elements, not two
```

The argument **count** then never matched the real invocation, so `MockSetup.Matches` rejected it
and the setup was silently skipped. `MockSetup.AreArgumentsEquivalent` was correct throughout — the
defect was purely in what the generator emitted.

This is the worst possible failure mode for a mocking library: the test still compiles and still
runs, but it is no longer testing what it says it tests. It either passes vacuously or fails while
pointing at the system under test rather than at the mock.

**Impact.** Every developer outside an invariant-like locale. Roughly half the world's .NET
developers configure a comma decimal separator.

**Fix.** All numeric emission now formats through `CultureInfo.InvariantCulture`, with round-trip
(`"R"`) formatting for `float` and `double` so the literal reconstructs the exact value, and an
`IFormattable` fallback so that culture-specific negative signs cannot leak in either. The same bug
was present in `AutoScribeCodeGenerator`, which *emitted a copy of the same helper into generated
code*; that copy was fixed too.

**Proof.** `tests/Skugga.Core.Tests/DecimalArgumentMatchingTests.cs` covers `decimal`, `double`,
`float` and a mixed `int`/`string` control. Two of its four cases fail without the fix. The three
sample tests now pass: `Step2.WithSkugga.Tests` is 12/12, `Skugga.Core.Tests` 465/465.

## 16. Skugga: the OpenAPI generator emitted code that would not compile

**Severity: high. Same root cause as 15, opposite failure mode.**

`tests/Skugga.OpenApi.Tests` did not build at all:

```
IAllOfTestApi_Mock.g.cs(25,94): error CS0747: Invalid initializer member declarator
```

The generated file contained:

```csharp
new AllOf_Product { Id = 123, Name = "Widget", Price = 29,99, InStock = true }
```

`Price = 29,99` — the same culture-sensitive formatting, this time inside an object initializer,
where the stray comma starts a new member and the compiler rejects it outright.

The pairing is instructive. One code path failed loudly at compile time; the other failed silently
at run time. Only the loud one would ever have been noticed, and it was noticed only because the
full solution was finally built.

**Fix.** A single invariant formatter in `ExampleGenerator`, applied to all 20 duplicated emission
sites. Header values in `MockGenerator` and diagnostic text in `DocumentValidator` were made
invariant as well — an HTTP header carrying `29,99` is wrong on the wire regardless of locale.

**Proof.** `Skugga.OpenApi.Tests` now builds and passes 195/201 (6 pre-existing skips), having
previously been incapable of building. Generated output now reads `Price = 29.99`.

## 17. Rapp: a vulnerable transitive dependency in the playground

**Severity: high (advisory), low (exposure).**

`dotnet restore Rapp.sln` failed:

```
error NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability
(GHSA-v5pm-xwqc-g5wc)
```

`Rapp.Playground` referenced `Microsoft.AspNetCore.OpenApi` 10.0.3, which pulls
`Microsoft.OpenApi` 2.0.0 transitively. The shipped `Rapp` package itself was never affected — but
the solution could not be restored, which meant nobody could build the repository from a clean
clone, and CI would have failed on its first run.

This is the identical advisory already fixed in Sannr (item 3). Finding it twice in one programme
is the argument for a shared dependency policy across the five repositories.

**Fix.** `CentralPackageTransitivePinningEnabled` was already enabled, so declaring
`<PackageVersion Include="Microsoft.OpenApi" Version="2.12.2" />` in `Directory.Packages.props`
lifts the transitive reference to a patched build. The pin carries a comment explaining when it can
be removed.

**Proof.** `Rapp.sln` restores and builds clean; 88/88 tests pass.

## 18. Skugga: three packaging defects, found by publishing packages from CI

**Severity: moderate. All three would have broken or embarrassed the first CI run.**

Adding a `dotnet pack` step to CI — something none of these repositories had — immediately found
three problems:

1. **The solution could not be packed at all.** `Skugga.OpenApi.Generator` and
   `Skugga.OpenApi.Tasks` set `IncludeBuildOutput=false` (their assemblies go to `analyzers/` and
   `tasks/`, not `lib/`), while the repository set `IncludeSymbols=true`. NuGet was asked to build
   a symbol package with no build output in it and failed with `NU5017`. Confirmed pre-existing by
   reproducing against the previous release commit.

2. **Samples and tests were published as packages.** `dotnet pack` produced
   `Step1-WithMoq.nupkg`, `Step2-WithSkugga.nupkg`, `OrdersApi.nupkg` and
   `Skugga.Benchmarks.nupkg` next to the three real ones. With the artifact-upload step now in CI,
   those would have been published as release artifacts.

3. **The benchmarks project inherited none of the repository's build settings.**
   `tests/Skugga.Benchmarks/Directory.Build.props` omitted the `GetPathOfFileAbove` import, and
   MSBuild stops walking up at the first such file it finds. The project therefore silently lost
   `LangVersion`, `Nullable`, analysis level, AOT flags, deterministic build settings and
   `IsPackable=false`. The tell was in the file itself: it re-declared
   `InterceptorsPreviewNamespaces` with a comment saying the generator needed it — a workaround for
   the missing inheritance rather than a fix for it.

   This one has a bearing on the numbers in this repository's README: the benchmark project
   producing the performance claims was compiled under different settings from the library it
   measures.

**Fix.** `IncludeSymbols=false` on the two analyzer/task projects; packability restricted to
`src/`; the missing import restored. `dotnet pack Skugga.slnx` now exits 0 and produces exactly
`Skugga`, `Skugga.OpenApi` and `Skugga.OpenApi.Tasks`. `Skugga.OpenApi.Tasks` had also been pinned
at 1.0.0 while shipping beside 1.6.0 packages, and now tracks the same version.

## 19. Rapp: benchmark and dashboard published as NuGet packages

**Severity: moderate. Fixed.**

The same pack step produced `Rapp.Benchmark.nupkg` and `Rapp.Dashboard.nupkg` alongside
`Rapp.nupkg`. Rapp keeps every project under `src/`, so a path-based rule does not work; the four
non-shipping projects are named explicitly in `Directory.Build.props`.

`dotnet pack Rapp.sln` now produces exactly `Rapp.1.3.0.nupkg`.

**Verification for both.** The contents of every shipping package were inspected directly, which is
the check that would have caught defect 8 and defect 11 earlier:

```
Sannr.1.7.0.nupkg    analyzers/dotnet/cs/Sannr.Gen.dll, lib/{net8.0,net10.0}/Sannr.{Core,AspNetCore}.dll
Skugga.1.6.0.nupkg   analyzers/…/Skugga.{Generator,OpenApi.Generator}.dll,
                     {build,buildTransitive}/Skugga.targets, lib/{net8.0,net10.0}/Skugga.Core.dll
Rapp.1.3.0.nupkg     analyzers/dotnet/cs/Rapp.Gen.dll, lib/{net8.0,net10.0}/Rapp.dll
```

---

# What Rapp and Skugga received

Both received the same packaging and supply-chain corrections as the others (multi-targeting,
SourceLink removal, CI matrix), and both now multi-target `net8.0` and `net10.0` rather than
forcing consumers onto the newest runtime.

---

# Found in the third pass: target frameworks, dependencies, and Prova

The third pass asked two questions of every repository: *does every target framework the package
ships actually get tested*, and *does restore report any advisory at all*. Both questions found
defects in projects that were already green.

It also brought a sixth project into the programme — **Prova**, the test framework the suite's own
tests run on — which turned out to hold more defects than the other five combined.

## 20. AutoMappic: the CLI test project was in no solution and had never run

**Severity: high. Fixed.**

`tests/AutoMappic.Cli.Tests` existed, compiled, and contained tests, but appeared in no solution
and no build script. It had never been restored, built, or executed at any point in its history.

Adding it to `AutoMappic.sln` immediately failed to restore, which exposed defect 21. This is the
same shape as defect 13 in this repository, and it recurred in Prova four separate times: **a test
that never ran is not a test.**

**Fix:** the project is in the solution and its four tests run on every CI build.

## 21. AutoMappic: the CLI and benchmarks targeted `net9.0`

**Severity: moderate. Fixed.**

`AutoMappic.Cli`, `AutoMappic.Benchmarks`, `SampleApp`, and `AotBenchmark` each pinned
`<TargetFramework>net9.0</TargetFramework>`. `net9.0` is an STS release whose support window closes
before the LTS that the libraries ship against, so the tool would fall out of support while the
library it drives was still supported. It also made the CLI untestable alongside the suite:
`NU1201 Project AutoMappic.Cli is not compatible with net8.0`.

**Fix:** all four use `$(ToolkitAppTargetFrameworks)`, so the framework choice is made once per
repository instead of being restated per project.

## 22. AutoMappic: two transitive packages carried high-severity advisories

**Severity: high. Fixed.**

| Package | Version | Reached via | Advisories |
| :--- | :--- | :--- | :--- |
| `Microsoft.OpenApi` | 2.0.0 | `Microsoft.AspNetCore.OpenApi` 10.0.1 | 1 high |
| `System.Security.Cryptography.Xml` | 9.0.0 | `Microsoft.CodeAnalysis.Workspaces.MSBuild` | 8 high |

The second was invisible until defect 21 was fixed, because the CLI had not previously been
restored in a build whose warnings anyone read. Fixing one defect is what made the next one
observable — a pattern that repeated throughout this work.

**Fix:** both pinned to patched versions in `Directory.Packages.props`, each with a comment
recording why the pin exists.

## 23. Rapp: tests ran only on `net10.0` while the package shipped `net8.0`

**Severity: high. Fixed.**

`Rapp.Tests.csproj` declared no target framework, so it inherited
`<TargetFramework>net10.0</TargetFramework>` from `Directory.Build.props`. The library itself used
`<TargetFrameworks>`, so `net8.0` was built and published — but never executed. Rapp shipped an
`net8.0` target framework on which not one test had ever run.

This is the same MSBuild trap that hid Prova's `net8.0` leg: **a singular `<TargetFramework>` in
`Directory.Build.props` silently defeats every project's plural `<TargetFrameworks>`**, and it does
so without a warning.

**Fix:** the global property is removed; test projects use `$(ToolkitTestTargetFrameworks)` and
executables `$(ToolkitAppTargetFrameworks)`. Rapp's test count went from 88 to 176 without a single
new test being written — the other 88 had simply never run.

## 24. Skugga: the OpenAPI tests ran only on `net8.0`

**Severity: moderate. Fixed.**

`Skugga.OpenApi.Tests` pinned `net8.0` while the rest of the suite multi-targeted. Its 201 tests
now run on both frameworks.

## 25. Viking Air: `MessagePack` 2.5.192 carried eleven advisories

**Severity: high. Fixed.**

The AppHost referenced `Aspire.Hosting.NodeJs` 9.5.2 alongside the Aspire 13.1.0 SDK. That version
drift pulled `MessagePack` 2.5.192, which carries eleven advisories, two of them high severity.

**Fix:** `MessagePack` pinned to 2.5.303, the latest patched 2.x, chosen over 3.x to avoid the 3.0
breaking changes in a transitive dependency. Restore across all six repositories is now free of
`NU1902` and `NU1903`.

---

# 26. Prova: the test framework the rest of the suite depends on

**24 defects recorded, 21 fixed.** Full detail in
[Prova's own `docs/known-issues.md`](https://github.com/Digvijay/Prova/blob/main/docs/known-issues.md).

Prova is the AOT-safe, zero-reflection test framework that AutoMappic's tests already ran on, so it
belongs in the programme on dependency grounds alone. Reviewing it produced the single strongest
argument in this submission, because the defects were nested: each fix was the thing that made the
next one visible.

1. `dotnet test` did not work at all on the .NET 10 SDK.
2. Fixing that revealed `IsTestingPlatformApplication` was inverted — **CI had been running zero
   tests while reporting success**.
3. Running the tests revealed `dotnet test` **exited 0 while tests were failing**.
4. With a truthful exit code, 15 real failures appeared — and only **11 of 66** generator tests
   were executing at all.
5. The concurrency isolation attributes were accepted, documented, and **silently ignored** by the
   adapter.
6. `[BeforeAll]`, `[BeforeEach]`, `[AfterAll]`, and `[AfterEach]` compiled and **never ran**.
7. The analyzer test project was in no solution; once run, its code fix was found to **corrupt line
   endings** in any CRLF file it touched.
8. The FsCheck emission tests had been asserting against an **empty generator run**, because the
   verification harness never referenced the assembly whose attributes it was testing.

The recurring theme, and the one worth naming in the submission: **a documented public API that
compiles and does nothing.** It appeared five times independently in Prova alone. Static analysis
does not find this class of defect, and neither does a passing test suite — only actually running
the thing does.

Prova now runs **241 tests on `net8.0` and `net10.0`** with a truthful exit code, **114 on .NET 11
RC1**, and every fix is pinned by a test that fails without it.

---

# Programme totals

| | Count |
| :--- | ---: |
| Defects recorded across the six repositories | 49 |
| Fixed and verified by a test | 43 |
| Open and documented | 6 |

Verified on `net8.0` and `net10.0` across all six repositories, and on .NET 11 RC1 as an advisory
CI leg, so that next year's breaking changes surface during the preview window rather than on
release day.
