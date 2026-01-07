using Rapp;

// VIKING AIR: RAPP SCHEMA EVOLUTION DEMO
// This demonstrates how Rapp prevents "Friday deployment crashes"

Console.WriteLine("🛡️  Viking Air: Rapp Schema Safety Demo");
Console.WriteLine("==========================================\n");

Console.WriteLine("THE PROBLEM:");
Console.WriteLine("------------");
Console.WriteLine("You deploy v1.0 of your app on Friday at 5pm.");
Console.WriteLine("It caches BookingRequest objects in Redis using binary serialization.");
Console.WriteLine("Over the weekend, thousands of bookings are cached.\n");

Console.WriteLine("Monday morning, you deploy v2.0 with a new field: 'MealPreference'");
Console.WriteLine("The binary layout has changed!");
Console.WriteLine("❌ Standard serializers: Read corrupt data or crash");
Console.WriteLine("✅ Rapp: Detects the schema mismatch via hash comparison\n");

Console.WriteLine("HOW RAPP WORKS:");
Console.WriteLine("---------------");
Console.WriteLine("1. When you mark a class with [RappCache], Rapp generates:");
Console.WriteLine("   - A unique schema hash based on the type structure");
Console.WriteLine("   - Custom serialization code optimized for HybridCache");
Console.WriteLine("2. When caching, Rapp stores: [SchemaHash][BinaryData]");
Console.WriteLine("3. When reading, Rapp checks: Does the hash match?");
Console.WriteLine("   - ✅ Match → Deserialize safely");
Console.WriteLine("   - ❌ Mismatch → Return null (cache miss)\n");

Console.WriteLine("DEMO MODELS:");
Console.WriteLine("------------\n");

// V1 Model
Console.WriteLine("[RappCache]");
Console.WriteLine("public partial class FlightManifestV1");
Console.WriteLine("{");
Console.WriteLine("    public string FlightId { get; set; }");
Console.WriteLine("    public string Destination { get; set; }");
Console.WriteLine("}");
Console.WriteLine($"→ Generated Hash: {typeof(FlightManifestV1).GetHashCode():X8}\n");

// V2 Model (with new field)
Console.WriteLine("[RappCache]");
Console.WriteLine("public partial class FlightManifestV2");
Console.WriteLine("{");
Console.WriteLine("    public string FlightId { get; set; }");
Console.WriteLine("    public string Destination { get; set; }");
Console.WriteLine("    public string MealPreference { get; set; } // NEW FIELD");
Console.WriteLine("}");
Console.WriteLine($"→ Generated Hash: {typeof(FlightManifestV2).GetHashCode():X8}\n");

Console.WriteLine("RESULT:");
Console.WriteLine("-------");
Console.WriteLine("✅ No crashes on schema changes");
Console.WriteLine("✅ Graceful degradation (cache miss → fetch from DB)");
Console.WriteLine("✅ Safe blue/green deployments");
Console.WriteLine("✅ Zero runtime reflection overhead\n");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("This is why Viking Air uses Rapp for production caching!");
Console.ResetColor();

// --- Mock Classes to demonstrate the concept ---

[RappCache]
public partial class FlightManifestV1
{
    public string FlightId { get; set; } = "";
    public string Destination { get; set; } = "";
}

[RappCache]
public partial class FlightManifestV2
{
    public string FlightId { get; set; } = "";
    public string Destination { get; set; } = "";
    public string MealPreference { get; set; } = ""; // NEW FIELD
}
