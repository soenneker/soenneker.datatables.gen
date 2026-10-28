using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Soenneker.DataTables.Gen.Tests;

internal static class GeneratorTestCompilation
{
    private static readonly MetadataReference[] _references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Concat(new[]
        {
            typeof(Soenneker.DataTables.Attributes.Column.DataTableColumnAttribute).Assembly.Location,
            typeof(Soenneker.DataTables.Attributes.Searchable.DataTableSearchableAttribute).Assembly.Location,
            typeof(Soenneker.DataTables.Attributes.Orderable.DataTableOrderableAttribute).Assembly.Location,
            typeof(Soenneker.Attributes.MapTo.MapToAttribute).Assembly.Location,
            typeof(Soenneker.Extensions.IQueryables.IQueryablesExtension).Assembly.Location,
            typeof(Soenneker.DataTables.Dtos.Column.DataTableColumn).Assembly.Location,
            typeof(Soenneker.DataTables.Dtos.ServerSideRequest.DataTableServerSideRequest).Assembly.Location,
            typeof(Soenneker.Dtos.RequestDataOptions.RequestDataOptions).Assembly.Location,
            typeof(Soenneker.Dtos.Filters.ExactMatch.ExactMatchFilter).Assembly.Location,
            typeof(Soenneker.Dtos.Options.OrderBy.OrderByOption).Assembly.Location,
            typeof(Soenneker.Enums.SortDirections.SortDirection).Assembly.Location
        }).Distinct().Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    internal static (Compilation Compilation, GeneratorDriverRunResult Result) Generate(params string[] source)
    {
        var compilation = CSharpCompilation.Create("GeneratedTest" + Guid.NewGuid().ToString(),
            source.Select(text => CSharpSyntaxTree.ParseText(text)), _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new DataTablesGenerator().AsSourceGenerator(), new Soenneker.Gen.EnumValues.EnumValueSourceGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);
        return (output, driver.GetRunResult());
    }

    internal static void Execute(params string[] source)
    {
        var generated = Generate(source);
        Diagnostic[] errors = generated.Compilation.GetDiagnostics().Concat(generated.Result.Diagnostics).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(d => d.ToString())));
        using var stream = new MemoryStream();
        var emit = generated.Compilation.Emit(stream);
        if (!emit.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
        stream.Position = 0;
        var context = new AssemblyLoadContext("GeneratedTest", isCollectible: true);
        try
        {
            Assembly assembly = context.LoadFromStream(stream);
            assembly.GetType("Probe")!.GetMethod("Run")!.Invoke(null, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
        finally { context.Unload(); }
    }

    internal static void ExpectDiagnostic(string id, string source)
    {
        var generated = Generate(source);
        if (!generated.Result.Diagnostics.Any(d => d.Id == id))
            throw new InvalidOperationException("Expected " + id + ": " + string.Join(Environment.NewLine, generated.Result.Diagnostics));
        if (generated.Result.Results.Any(result => result.Exception != null))
            throw new InvalidOperationException("The generator threw an exception.");
    }
}
