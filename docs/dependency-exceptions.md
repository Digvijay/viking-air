# Dependency exceptions

The `OSPO compliance` workflow fails the build when `dotnet list package --vulnerable
--include-transitive` reports any advisory. This file is the only supported way to accept a
finding temporarily, and every entry must be time-bound and owned.

An exception is acceptable only when **all** of the following hold:

1. No fixed version is available, or the fixed version is not yet reachable from the feeds
   used by this project.
2. The vulnerable code path is not reachable in this project, or a mitigation is in place.
3. An owner and an expiry date are recorded below.
4. A tracking issue exists.

Expired entries must be removed or renewed. An expired entry is treated as a build failure,
not as an accepted risk.

## Active exceptions

| Package | Version | Advisory | Reachable here? | Mitigation | Owner | Expires | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| MessagePack | 2.5.192 | 2 High, 9 Moderate | No | Transitive via `Aspire.Hosting.*`, reachable only from `VikingAir.AppHost`, which is local development orchestration. It is not referenced by `VikingAir.Api`, is not part of any published artefact, and is not in the Native AOT output. Forcing MessagePack 3.x is not attempted because it is a breaking change for the Aspire dashboard protocol. | @Digvijay | 2026-12-31 | TBD |

### Notes on the MessagePack entry

The honest position is that this is an Aspire tooling dependency that the project does not
control. It clears when Aspire ships a build that resolves MessagePack 3.x. Until then the
mitigation is containment, not remediation: `VikingAir.AppHost` is excluded from anything that
ships. Re-evaluate on every Aspire update rather than waiting for the expiry date.

## Retired exceptions

| Package | Advisory | Resolution | Date |
| --- | --- | --- | --- |
| Microsoft.AspNetCore.OpenApi | High | Package removed. It was referenced by `VikingAir.Api` but never called. | 2026-09-25 |
| Microsoft.OpenApi | GHSA-v5pm-xwqc-g5wc (High) | Pinned to 2.12.2 in `VikingAir.Core` and `VikingAir.Api`. Reached transitively through Sannr 1.6.0, which resolves 2.4.1. Sannr should raise its floor. | 2026-09-25 |