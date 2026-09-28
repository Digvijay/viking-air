# Contributing to Viking Air

Viking Air is the integration demonstration for four compile-time .NET libraries:
[AutoMappic](https://github.com/Digvijay/AutoMappic) (mapping),
[Sannr](https://github.com/Digvijay/Sannr) (validation),
[Rapp](https://github.com/Digvijay/Rapp) (binary caching), and
[Skugga](https://github.com/Digvijay/Skugga) (test doubles).

Its purpose is to prove those libraries work together in a realistic ASP.NET Core application
that publishes with Native AOT. Changes are evaluated against that purpose.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](CODE_OF_CONDUCT.md).
Contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with questions or concerns.

## Before you start

- For anything beyond a small fix, open an issue first so the approach can be agreed.
- Security issues must not be raised in public issues or pull requests. See [SECURITY.md](SECURITY.md).

## Prerequisites

- .NET SDK 10.0 or later
- Node.js 20 or later (for `VikingAir.Web`)
- Docker, if you want to run the Aspire AppHost with Redis

## Build, test, benchmark

```bash
dotnet restore VikingAir.sln
dotnet build   VikingAir.sln --configuration Release
dotnet test    VikingAir.sln --configuration Release

# Benchmarks. A dry job only checks that they run and that their guards hold.
dotnet run --configuration Release --project VikingAir.Benchmarks -- --job dry --filter '*'

# Publishable numbers require a real job on quiescent hardware.
dotnet run --configuration Release --project VikingAir.Benchmarks -- --filter '*'
```

### Native AOT

The point of the demo is that the API publishes and runs as a native binary:

```bash
dotnet publish VikingAir.Api/VikingAir.Api.csproj -c Release -p:PublishAot=true -o ./aot
./aot/VikingAir.Api
```

A change that breaks Native AOT publication is a breaking change.

## Working behind a private or proxied NuGet feed

Do not commit an internal feed URL to this repository. Configure it locally instead, either with
`dotnet restore --source <feed>` or a `NuGet.config` that you keep untracked.

## Standards for changes

- **No runtime reflection on a supported path.** The entire premise is compile-time generation.
- **Benchmarks must measure what they claim.** Benchmarks carry correctness guards that throw
  when a library under test is not actually exercised. Do not remove a guard to make a
  benchmark pass; fix the wiring instead.
- **Claims must be reproducible.** Any performance number added to documentation must come from
  committed benchmark output, and must state the hardware, OS, runtime version, and job.
- **No new build warnings.**
- Public behaviour changes need test coverage.

## Dependencies

CI fails when `dotnet list package --vulnerable --include-transitive` reports an advisory. If no
fixed version exists, record a time-bound entry in
[docs/dependency-exceptions.md](docs/dependency-exceptions.md) rather than suppressing the gate.

## Contributor License Agreement

This project is maintained independently. If it is later transferred to Microsoft, contributions
will require acceptance of the
[Microsoft Contributor License Agreement](https://cla.opensource.microsoft.com), and contributors
will be asked to sign at that point. By contributing now you agree that your contribution is
licensed under the repository's [MIT licence](LICENSE).

## Pull requests

Keep changes focused, explain the reasoning in the description, and complete the checklist in the
pull request template. A maintainer review is required before merge.
