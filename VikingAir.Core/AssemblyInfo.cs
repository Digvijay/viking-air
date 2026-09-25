using System.Runtime.CompilerServices;

// AutoMappic's generator runs in every project that references VikingAir.Core and emits a
// registration calling this assembly's generated VikingAir_Core_Registration class. The
// generator declares that class `internal`, so without these grants each consuming project
// fails to compile with:
//
//   error CS0122: 'VikingAir_Core_Registration' is inaccessible due to its protection level
//
// Upstream fix: AutoMappic should emit the registration class as public, or emit a public
// entry point for cross-assembly registration. Tracked as an AutoMappic issue.

[assembly: InternalsVisibleTo("VikingAir.Api")]
[assembly: InternalsVisibleTo("VikingAir.Benchmarks")]
[assembly: InternalsVisibleTo("VikingAir.EvolutionDemo")]
[assembly: InternalsVisibleTo("VikingAir.Tests")]
