using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

string project = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
string assets = Path.Combine(project, "Assets");
if (!Directory.Exists(assets))
{
    Console.Error.WriteLine("Missing Unity Assets directory.");
    return 1;
}

string[] sources = Directory.EnumerateFiles(assets, "*.cs", SearchOption.AllDirectories)
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToArray();
if (sources.Length < 15)
{
    Console.Error.WriteLine("Missing original Unity 6 C# runtime, editor, or tests.");
    return 1;
}

var parseOptions = CSharpParseOptions.Default
    .WithLanguageVersion(LanguageVersion.Latest)
    .WithPreprocessorSymbols("UNITY_EDITOR", "UNITY_6000_0_OR_NEWER");

var errors = new List<Diagnostic>();
foreach (string file in sources)
{
    string text = await File.ReadAllTextAsync(file);
    var sourceTree = CSharpSyntaxTree.ParseText(text, parseOptions, file);
    errors.AddRange(sourceTree.GetDiagnostics()
        .Where(x => x.Severity == DiagnosticSeverity.Error));
}
foreach (Diagnostic diagnostic in errors.OrderBy(x => x.Location.SourceTree?.FilePath, StringComparer.Ordinal))
    Console.Error.WriteLine(diagnostic.ToString());

if (errors.Count > 0)
{
    Console.Error.WriteLine($"FAILED: {errors.Count} C# syntax problems.");
    return 1;
}
Console.WriteLine($"PASS: Parsed {sources.Length} Unity 6 C# scripts with the SDK Roslyn compiler.");
Console.WriteLine("Syntax-only. Unity assemblies, API compatibility, scene loading, and gameplay still require Unity Editor.");
return 0;
