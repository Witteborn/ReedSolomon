using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CompatibilityValidation;

const string UsageText = """
    Usage:
      Compatibility report <output.json>
      Compatibility measure <operation> <payloadBytes>
      Compatibility compare <resultsDirectory> <output.md>
    """;

if (args.Length == 0)
{
    Console.Error.WriteLine(UsageText);

    return 1;
}

var codecVersion = typeof(FormatProbe).Assembly
    .GetCustomAttributes<AssemblyMetadataAttribute>()
    .Single(attribute => attribute.Key == "CodecVersion")
    .Value!;

switch (args[0])
{
    case "report":
        WriteReport(codecVersion, args[1]);

        return 0;

    case "measure":
        var result = PerformanceProbe.Run(codecVersion, args[1], int.Parse(args[2]));

        Console.WriteLine(JsonSerializer.Serialize(result));

        return 0;

    case "compare":
        ReportWriter.Write(args[1], args[2]);

        return 0;

    default:
        Console.Error.WriteLine(UsageText);

        return 1;
}

static void WriteReport(string codecVersion, string outputPath)
{
    var codecAssembly = typeof(Codec).Assembly;
    var debugFlags = codecAssembly.GetCustomAttribute<DebuggableAttribute>();
    var assemblyBytes = File.ReadAllBytes(codecAssembly.Location);
    var report = new CompatibilityReport(
        codecVersion,
        RuntimeInformation.FrameworkDescription,
        Convert.ToHexString(SHA256.HashData(assemblyBytes)),
        debugFlags?.IsJITOptimizerDisabled ?? false,
        ManagedProbe.CreateSampleShards(),
        FormatProbe.Run(),
        ManagedProbe.Run(),
        BehaviorProbe.Run());
    var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

    File.WriteAllText(outputPath, reportJson);
}
