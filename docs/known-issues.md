# Defects found by integrating the libraries

Viking Air exists to prove AutoMappic, Sannr, Rapp, Skugga, and Prova compose in a realistic
application. Building it surfaced defects that none of the individual repositories' own tests or
samples caught, because each library is exercised there in isolation, in a single assembly, on a
single target framework, in a single locale.

That is the point of this repository, and it is the main argument for treating these six as one
programme rather than six unrelated projects. **Nothing is open.** One item is partially fixed —
Skugga's half of entry 34, an IDE-performance defect with no effect on build output, where the
remaining half is a large value-model extraction and the available shortcut was a correctness bug.
One item is blocked on a release rather than a defect — entry 42, AutoMappic's dependency on a
Prova version that has not been published. Every other entry is fixed, except one that failed to
reproduce and is recorded as withdrawn rather than dropped.

Entries 39–42 deserve separate mention: they were found by opening the pull requests, which ran CI
on GitHub-hosted x64 runners for the first time. Four defects appeared in minutes, two of them
meaning that the workflows claiming to prove every library trim- and AOT-clean had never actually
enforced a single one of those warnings. Everything before entry 39 was found on one Windows ARM64
machine, which is exactly why those four survived.

Every item below states what was observed, what it blocked, how it was fixed, and how the fix is
proven. Anything not fully closed says so plainly.

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
| 10 | AutoMappic | Public API annotated `RequiresDynamicCode` | High | **Fixed** |
| 11 | AutoMappic | `PrivateAssets="all"` fails at run time | Moderate | **Withdrawn, did not reproduce** |
| 12 | AutoMappic | Higher allocation than alternatives | Moderate | **Fixed** |
| 13 | Viking Air | Test project was not in the solution | High | **Fixed** |
| 14 | Viking Air | CI workflow could never have worked | High | **Fixed** |
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
| 26 | Prova | Thirty-one defects in the test framework (summarised below) | High | **Fixed** |
| 27 | Viking Air | CI never ran on the default branch | Moderate | **Fixed** |
| 28 | Viking Air | Shared target-framework policy was never imported | High | **Fixed** |
| 29 | Viking Air | CI depended on package versions that were never published | High | **Fixed** |
| 30 | Sannr | Fluent validators silently not generated | High | **Fixed** |
| 31 | Sannr | Non-reproducible generator output and debug files in consumers | Moderate | **Fixed** |
| 32 | Rapp | Test generators shipped to every consumer | Moderate | **Fixed** |
| 33 | Skugga | Generator assemblies leaked into consumers as compile references | Moderate | **Fixed** |
| 34 | Rapp, Skugga | Generator pipelines defeat incremental caching | Low | **Fixed (Rapp) / Partially fixed (Skugga)** |
| 35 | Rapp | Shipped library serialized every value twice, once to JSON by reflection | High | **Fixed** |
| 36 | Rapp | Samples' JSON size comparison no longer populated | Low | **Fixed** |
| 37 | Rapp | Three sample projects were in no solution the build ever compiled | Moderate | **Fixed** |
| 38 | Rapp | Six project files defined a symbol that does nothing | Low | **Fixed** |
| 39 | All five | `-p:PublishAot=true` disabled the AOT gate it was meant to enforce | High | **Fixed** |
| 40 | All five | IL-warning list split on its commas, so nothing was enforced | High | **Fixed** |
| 41 | Prova | A test asserted one of three behaviours and inherited which one | Moderate | **Fixed** |
| 42 | AutoMappic | Depends on a Prova version that was never published | High | **Blocked on a release** |

Entries 13, 14, 25 and 27–29 are defects in this repository itself.

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

# Resolved after direct investigation

The three items that stood here were recorded as open API-design questions. Investigating them
directly turned two into ordinary defects and retired the third, so the "awaiting a design
decision" framing was wrong. They are summarised here and recorded in full in AutoMappic''s own
`docs/known-issues.md`.

## 10. AutoMappic: `Profile.CreateMap` was annotated as requiring dynamic code

`[RequiresDynamicCode]` on the generic `CreateMap` was factually wrong — the method assigns a
field and adds to a list, and the generator reads `CreateMap` calls from the syntax tree rather
than instantiating anything. Removed. `[RequiresUnreferencedCode]` on the same method and the
annotations on `IMapper` are kept deliberately: trimming and the reflection fallback are real, and
the trimming analyzer requires interface and implementation annotations to match. What was
actually wrong was the documentation, which now names the generated extension methods as the
AOT-safe API instead of implying the whole `Profile` surface is AOT-clean.

Worth recording: three attempts to reproduce this found nothing, including for a deliberate
`Type.MakeGenericType` control probe. `PublishAot=true` alone does not switch the analyzers on.
An AOT clean bill of health obtained without a control probe should not be believed.

## 11. AutoMappic: `PrivateAssets="all"` producing a runtime failure — withdrawn

Did not reproduce. Built against a real consumer from a locally packed package,
`AutoMappic.Core.dll` is copied to output, appears in `deps.json`, and interception works.
`PrivateAssets` governs flow to downstream projects, and the package ships its targets under both
`build/` and `buildTransitive/`. The narrower case of a library that itself packs was not tested
and is untested rather than disproven.

## 12. AutoMappic: allocating more than both alternatives

A boxing defect, not a trade-off. Generated bodies boxed the identity key on every map while the
interceptor had tracking disabled, so the two calls that consumed it were no-ops. Allocation is
now identical to hand-written mapping.

The larger finding was underneath it: **no benchmark in AutoMappic could build at all.**
BenchmarkDotNet rebuilds the referenced graph with `/p:ArtifactsPath`, relocating `obj/`, and
Nerdbank.GitVersioning then fails with MSB3030 on a staging file it never wrote. Every method
reported `NA`, which means the performance table published in the README — the evidence for the
project''s central claim — could not have come from that source tree. `dotnet build` on the
benchmark project succeeds, because the failure happens only in the nested build BenchmarkDotNet
performs at run time. A green build is not evidence that a benchmark runs.

The benchmarks were also pinned to `RuntimeMoniker.Net90` while the project targeted `net10.0` and
captioned its results .NET 10.0.12, and they covered only the current release while the library
ships `net8.0` too. This is the same defect already recorded against Sannr and Rapp — a shipped
framework that is never exercised — and it was not recognised as the same defect until the
benchmarks were looked at directly. Benchmarks now derive their frameworks from the shipped
library list, which immediately earned its keep: AutoMappic is marginally faster than hand-written
mapping on .NET 10 and about 12% slower on .NET 8.

**Theme.** Three of this programme''s findings now share one shape: a published number whose
producer was broken — Sannr''s benchmark table for a benchmark that was never wired up, Prova''s
tests that could not fail the build, and AutoMappic''s performance table from a suite that could
not compile. A measurement nobody can reproduce is not weaker evidence than a missing one; it is
worse, because it looks like evidence.
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

**31 defects recorded, 31 fixed.** Full detail in
[Prova's own `docs/known-issues.md`](https://github.com/Digvijay/Prova/blob/master/docs/known-issues.md).

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
9. Closing the last three items uncovered six more. `Prova.Aspire.Sample` had been recorded as an
   empty directory; it was in fact three tracked projects in no solution, **carrying vulnerable
   packages precisely because they were outside the audit**. A parity test written to stop that
   recurring then found two further projects in no solution — both of which **had never compiled**.
10. And, found only while writing tests for something else: **`Assert.Equal` on two equal arrays
    failed.** It compared collections by reference. `Assert.NotEqual` did not exist at all.
11. Running CI's coverage command locally before pushing it showed that Prova's generated entry
    point **registered none of the platform's extensions** besides the dump providers. Referencing
    code coverage, TRX or retry did nothing except make the run fail with zero tests executed.

Two themes are worth naming in the submission.

The first is **a documented public API that compiles and does nothing.** It appeared five times
independently in Prova alone. Static analysis does not find this class of defect, and neither does
a passing test suite — only actually running the thing does.

The second is sharper, and it generalises beyond Prova: **a project outside the build graph is also
outside the audit.** Every "restore is clean" and "the tests pass" claim is scoped to what a
solution file happens to list. Three separate times in this repository, code that was tracked in
git, shipped to anyone who cloned it, and referenced from the documentation was compiled by nothing
and therefore checked by nothing. The fix that matters is not the five csprojs — it is
`SolutionParityTests`, which asserts that no project on disk is outside a solution and fails the
build when the next one appears. A check that cannot drift beats a fix that can.

Prova now passes **460 tests across `net8.0`, `net10.0` and `net11.0` RC1** with a truthful exit
code. Every code defect is pinned by a test that fails without its fix; the build and CI defects are
verified by running the command that failed.

---

# Found while preparing CI to run on GitHub-hosted runners

Every result above was produced locally. Preparing the workflows to run on GitHub for the first
time, and running each workflow's commands locally before pushing it, found the following. The
library-side findings from the same pass are recorded in their own repositories: Prova #29 and
#30, AutoMappic #16 and #17, Rapp #6, Sannr #8 and Skugga #8.

## 27. Viking Air: CI never ran on the default branch

**Severity: moderate. Fixed.**

`ci.yml` triggered on `main`; this repository's default branch is `master`. No pull request here
had ever been built by CI. Skugga had the same defect.

**Fix:** the workflow triggers on `master`, and the build job runs on `ubuntu-latest` and
`windows-latest`.

## 28. Viking Air: the shared target-framework policy was never imported

**Severity: high. Fixed.**

`Directory.TargetFrameworks.props` defines the frameworks every repository in the programme builds,
including the opt-in `net11.0` preview leg. Viking Air had the file but no `Directory.Build.props`,
so nothing imported it. Every project hardcoded `net10.0`, and the CI job labelled as the preview
leg built `net10.0` only and reported success. A check that cannot fail is not a check.

**Fix:** `Directory.Build.props` imports the policy, and every project uses
`$(ToolkitAppTargetFrameworks)`. With the preview enabled, the solution now builds and tests
`net10.0` and `net11.0`.

## 29. Viking Air: CI depended on package versions that were never published

**Severity: high. Fixed.**

The projects referenced AutoMappic `0.7.0-gccd4f2ee8f`, Rapp 1.3.0, Sannr 1.7.0 and Skugga 1.6.0.
None of those exist on nuget.org: the AutoMappic version was a local commit build, and the other
three are the unreleased versions that contain the fixes recorded here. Restore worked only on the
machine that had built them, so CI could never have restored the solution, on any runner.

Publishing the libraries first would have fixed the restore but not the purpose. This repository
exists to test the libraries' current source together, and pinning it to published packages would
test only what was last released.

**Fix:** `eng/build-libraries.ps1` clones Prova, AutoMappic, Rapp, Sannr and Skugga at the same
branch as the pull request (falling back to each default branch), packs them into a local feed,
and exports the resulting versions, which `Directory.Build.props` reads. All three workflows run it
before restoring. A cross-repository change is tested by using one branch name in each repository.
Verified locally: the script produced 16 packages, and Viking Air restored, built with zero
warnings, and passed its tests against them.

---

# Found by chasing warnings on the .NET 11 release candidate

Running every repository on SDK `11.0.100-rc.1.26425.128` passed, but not cleanly: Sannr and Skugga
built with warnings. Treating each warning as a question rather than noise found the entries below.
None was caused by .NET 11; each was an existing defect that the new SDK's analyzers or a clean
look made visible. The per-repository records are Sannr #9–#12, Rapp #7–#11, Skugga #9–#11 and
Prova #31.

## 30. Sannr: fluent validators were silently not generated

**Severity: high. Fixed.**

`ValidatorConfig<T>` classes produced no validator unless the project had also opted into OpenAPI
schema generation. There was no diagnostic. The fluent test project could not compile because of
it, and nobody knew, because it was not in the solution.

## 31. Sannr: non-reproducible generator output, and debug files in every consumer

**Severity: moderate. Fixed.**

A static set leaked between compilations, so validators vanished on the second build in the IDE;
hint names contained `Guid.NewGuid()`; templates stamped `DateTime.Now`; and four debug files were
added to every consuming compilation. Entry 5 had been recorded as fixed when only part of the
scaffolding was removed — which is why this entry exists.

## 32. Rapp: test generators shipped to every consumer

**Severity: moderate. Fixed.**

The same defect as entry 5, in a second library: a `TestGenerator` class and a `TestGenerator.g.cs`
output, packed into the analyzer. The fact that it recurred is the argument for the hygiene tests
now in both repositories, which assert which generators are registered and that an unrelated
compilation receives no generated source.

## 33. Skugga: generator assemblies leaked into consumers as compile references

**Severity: moderate. Fixed.**

An unused `GetTargetPath` hook in `Skugga.Core` made every project that referenced it compile
against three Roslyn generator assemblies, causing `MSB3277`, and one test project compiled only
because of the leak. The published package was not affected; this was verified by comparing its
file list before and after.

## 34. Rapp and Skugga: generator pipelines defeat incremental caching

**Severity: low. Rapp fixed; Skugga partially fixed.**

Rapp's pipeline carried `INamedTypeSymbol` and `ClassDeclarationSyntax` values, and Skugga's
predicate accepted every invocation in the compilation and then combined the result with the whole
`Compilation` — a value it destructured and never read. In both, code generation re-ran on every
keystroke in every consuming project. Build output is correct, so this cost IDE CPU rather than
correctness — but a programme that argues for doing less work at build time should not ship it.

Both generators in Rapp now use `ForAttributeWithMetadataName` and equatable value models, and
`GeneratorIncrementalityTests` asserts every tracked step reports `Cached` or `Unchanged`, with a
deliberately defective generator as a control so the harness is proven able to fail.

Skugga's predicate and the pointless compilation combine are fixed and pinned by tests that were
first shown to fail against the old code. Its `TargetInfo` still carries symbols, and the five
downstream generators are symbol-driven, so full caching needs a value-model extraction across a
large surface and remains open in that repository. A shortcut — symbol display-string key equality
— was considered and rejected: two compilations can present the same key for an interface whose
members changed, and the generator would then serve stale generated code. A caching fix that can
emit stale output is worse than the cost it removes.

An audit of the other three generators for the same pattern has not been done.

## 35. Rapp: the shipped library serialized every value twice, once to JSON by reflection

**Severity: high. Fixed.**

This is the most consequential finding for the programme's thesis, and it was found by replacing a
flaky wall-clock test with an allocation assertion. Rapp's documentation said the package had zero
telemetry overhead and that a `RAPP_TELEMETRY` symbol in the consuming project would enable a
JSON-size comparison. In fact the symbol was defined for the whole repository, library included,
so every cache write in the shipped package serialized the value a second time and a third time to
JSON by reflection, and every cache hit serialized the result to JSON. The output was discarded.
Under Native AOT the reflection call failed inside an empty `catch`.

`Serialize` into a reused buffer allocated 568 bytes per call; it now allocates none, and a test
enforces it. Rapp's published benchmark figures were measured with the overhead included.

## 36. Rapp: the samples' JSON size comparison was no longer populated

**Severity: low. Fixed.** The samples displayed numbers produced by the code removed in entry 35.

The measurement now lives in `Rapp.Dashboard.RappSizeComparison`: opt-in, called from the samples'
own cache-miss paths, annotated for trimming and AOT with a `JsonTypeInfo` overload for callers who
need it to stay Native-AOT-safe. `TelemetryOverheadTests` listens to the `Rapp` meter and asserts
the library emits no size measurement; built with `-p:DefineConstants=RAPP_TELEMETRY` it fails with
400 of them.

## 37. Rapp: the three sample projects were in no solution the build ever compiled

**Severity: moderate. Fixed.**

Rapp's samples were referenced only by `Samples/Rapp.Samples.sln`. CI built `Rapp.sln`, so the
repository's only demonstration of how the library is meant to be used had never been compiled by
any pipeline. Building them raised `CA1873` twice in the gRPC sample immediately — log arguments
evaluated before the level was checked. The warning is small; that nothing would ever have reported
it is not.

This is the third instance of the theme first recorded as entry 13 and again as entry 20: a project
outside the build graph is outside the audit. The samples are now in `Rapp.sln`, so
`TreatWarningsAsErrors` covers them, and the logging is `[LoggerMessage]`-generated.

## 38. Rapp: six project files defined a symbol that does nothing

**Severity: low. Fixed.**

The three samples, the test project, the playground and the dashboard each appended
`RAPP_TELEMETRY` to `DefineConstants`. A `#if` is evaluated where the code containing it is
compiled, and every `#if RAPP_TELEMETRY` block is in `Rapp`'s own sources — so none of the six
defines changed a single byte of output. They are removed.

They are worth recording because that exact misunderstanding is what entry 35 was: the symbol read
as an opt-in switch while the cost it guarded was being paid unconditionally by everyone.

---
# Found by running CI on GitHub-hosted x64 runners for the first time

Every entry above this line was produced on one Windows ARM64 developer machine. Opening the six
pull requests ran the workflows on GitHub-hosted Linux and Windows x64 runners for the first time,
and four more defects appeared within minutes. None of them is a defect in any library's code;
all four are defects in how the repositories *verify* their code, which is precisely the class a
developer machine cannot find.

## 39. All five repositories: `-p:PublishAot=true` disabled the AOT gate it was meant to enforce

**Severity: high. Fixed in AutoMappic, Sannr, Rapp, Skugga and Viking Air.**

Each `aot-validation.yml` passed `-p:PublishAot=true` to `dotnet publish`. The flag was redundant
everywhere — every target project already declares `PublishAot` — and it was actively harmful.

A `-p:` switch on the command line creates a **global property**, and MSBuild propagates global
properties into every `ProjectReference` it builds. Each of these repositories references a
`netstandard2.0` analyzer or generator project, which cannot be AOT-compiled, so every run failed
with `error NETSDK1207: Ahead-of-time compilation is not supported for the target framework.`

The identical property declared inside a project file does **not** flow across a
`ProjectReference`. That asymmetry between a command-line `-p:` and a project-file property is the
single most reusable thing this programme learned from running CI, and it is invisible locally
because nobody publishes from the command line with that flag by hand.

**Fix:** removed from all five workflows. AOT stays configured in the project files.

## 40. All five repositories: the IL-warning list was split on its commas, so nothing was enforced

**Severity: high. Fixed in AutoMappic, Sannr, Rapp, Skugga and Viking Air.**

The same step in each workflow passed:

```
-p:WarningsAsErrors=IL2026,IL2046,IL2062,IL2067,...
```

The dotnet CLI splits `-p:` values on commas. Every code after the first was parsed as its own
switch, and each run died with `MSBUILD : error MSB1006: Property is not valid. Switch: IL2046`
before compiling anything.

The consequence is worse than a broken build. These workflows were the evidence for the claim that
every library is trim- and AOT-clean, and they had never enforced a single one of those thirteen
warnings. A bare `;` would not have worked either, since it is the property separator.

**Fix:** the codes are joined with `%3B`, the escaped semicolon, which reaches MSBuild as one
property value. Separately, `dotnet publish` on a multi-targeted project requires an explicit
`--framework` (NETSDK1129), so AutoMappic, Rapp and Viking Air now name `net10.0` on the publish
step — and only there, because passing it to a solution-wide build breaks the `netstandard2.0`
generator projects.

## 41. Prova: a test asserted one of three deliberate behaviours and inherited which one

**Severity: medium. Fixed.**

`ConsoleLogger` picks its output syntax from the environment: `GITHUB_ACTIONS` selects GitHub's
`::error::` workflow commands, `TF_BUILD` selects Azure Pipelines' `##vso[task.logissue]` commands,
otherwise it writes `[ERR]` in colour. All three are intended. The tests constructed the logger
with its detecting constructor and asserted `[ERR]`, which is true on a laptop and false on
Actions, where the test process inherits `GITHUB_ACTIONS=true`. Two tests failed on the first
hosted run.

The fix that tempts is to unset the variables in the test, which mutates process-global state the
runner itself reads, or to accept either output, which asserts nothing.

**Fix:** the environment read became a seam — a `ConsoleLogHost` enum, a constructor that takes it,
and a static `DetectHost()`. The tests name the host and assert all three syntaxes, plus detection
itself. Verified by running the whole suite with `GITHUB_ACTIONS=true` and `TF_BUILD=True`
exported: identical results to a clean shell. This is the same defect as entry 27 in Prova's own
ledger — a hardcoded version string — wearing different clothes: a value taken from the
surroundings must be injectable, or the tests only ever assert the surroundings they ran in.

## 42. AutoMappic: the test suite depends on a Prova version that was never published

**Severity: high. Not fixable inside AutoMappic.**

`Directory.Packages.props` pins `Prova` to `0.6.0`, which exists only in the local NuGet cache of
the machine this review was carried out on. nuget.org's newest published Prova is `0.5.0`, so the
first hosted run — with a cold cache — failed at restore with `NU1102: Unable to find package Prova
with version (>= 0.6.0)`.

Pinning back to `0.5.0` does not help: the build then fails with several hundred `CS0246` errors,
because the APIs the tests use arrived in `0.6.0`. The dependency is right; the release is missing.

**Fix:** none available in AutoMappic. Prova `0.6.0` must be published — its
`Directory.Build.props` already declares that version and its tag-triggered `publish.yml` produces
it — after which AutoMappic restores unchanged. Recorded rather than worked around, because
pinning to a version that cannot compile, or vendoring a copy, would conceal a real release gap.
It is the one item in this programme that is genuinely blocked, and it is blocked on a release,
not on a defect.

Prova's `publish.yml` was hardened before being trusted with that release: it packed with
`--no-build` while overriding the package version from the git tag, so a tag that disagreed with
`Directory.Build.props` would have shipped a correctly-named package full of differently-versioned
assemblies, greenly. It also ran no tests before pushing to nuget.org. It now fails on a
tag/version mismatch and runs the suite on the artefacts it is about to publish.

---
# Programme totals

| Repository | Recorded | Fixed | Withdrawn | Partially fixed | Blocked externally |
| :--- | ---: | ---: | ---: | ---: | ---: |
| Prova | 34 | 34 | 0 | 0 | 0 |
| AutoMappic | 20 | 18 | 1 | 0 | 1 |
| Sannr | 14 | 14 | 0 | 0 | 0 |
| Rapp | 15 | 15 | 0 | 0 | 0 |
| Skugga | 13 | 12 | 0 | 1 | 0 |
| Viking Air (entries 13, 14, 25, 27–29, 39, 40) | 8 | 8 | 0 | 0 | 0 |
| **Total** | **104** | **101** | **1** | **1** | **1** |

Counted from the table in each repository's own `docs/known-issues.md`; the library entries in
this file are summaries of those and are not counted twice. "Fixed" means verified either by a test
that fails without the fix or, for build and CI defects, by re-running the command that failed.

The bookkeeping has itself been wrong: an earlier version of this table said 56 and did not
reconcile with the files, and Sannr's entry 6 was recorded as fixed when only part of the defect
had been removed. Each was found
by re-reading the files rather than trusting the summary, which is the same lesson as every other
finding in this document.

One item is partially fixed: Skugga's half of entry 34, where the cheap and
provable half is done and the remaining half is a value-model extraction across five symbol-driven
generators and 1922 tests. It is recorded as partially fixed rather than fixed because the
incrementality test for Skugga's output stage does not yet pass, and rather than closed-with-a-
shortcut because the available shortcut was a correctness bug.

One item is blocked externally: entry 42, AutoMappic's dependency on an unpublished Prova `0.6.0`.
It is blocked on a release, not on a defect, and is recorded rather than papered over.

Limits on all of the above, stated because they bound what the totals are worth:

* Entries 1–38 were produced on a single Windows ARM64 machine. That caveat is now partly retired:
  CI runs on GitHub-hosted x64 Linux and Windows runners, and Rapp, Skugga and Viking Air are green
  there across CI, OSPO compliance and benchmarks. Sannr is green on OSPO compliance, CodeQL and
  dependency submission. AutoMappic cannot complete until entry 42 is resolved. Entries 39–42 are
  the direct yield of finally running on that hardware, and it took minutes to find four defects
  that months of local testing could not.
* All six repositories restore, build and pass every test on `net8.0` and `net10.0`, and on
  `net11.0` with SDK `11.0.100-rc.1.26425.128`. A release candidate is not a release; the `net11.0`
  leg is an opt-in CI job so that breaking changes surface during the preview window, and should
  be re-run against the GA SDK.
* 88 of 90 measures how hard these repositories were looked at, not that they are defect-free.