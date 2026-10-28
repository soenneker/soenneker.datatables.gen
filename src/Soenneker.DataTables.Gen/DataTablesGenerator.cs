using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using static Soenneker.DataTables.Gen.SymbolMetadata;

namespace Soenneker.DataTables.Gen;

/// <summary>Generates column metadata and typed search expressions from DataTables attributes.</summary>
[Generator]
public sealed class DataTablesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static output => GeneratedSupport.Emit(output));
        var models = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, token) =>
            {
                if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, token) is not INamedTypeSymbol model) return null;
                SyntaxReference first = model.DeclaringSyntaxReferences[0];
                if (first.SyntaxTree != ctx.Node.SyntaxTree || first.Span != ctx.Node.Span) return null;
                if (Attribute(model, "Soenneker.DataTables.Gen.GenerateDataTableAttribute") != null) return model;
                if (model.Arity != 0 || model.ContainingType != null || model.IsFileLocal || model.IsRefLikeType || model.IsStatic || model.TypeKind == TypeKind.Interface) return null;
                return Properties(model).Any(property => Attribute(property, ColumnAttribute) != null ||
                    Attribute(property, SearchableAttribute) != null || Attribute(property, OrderableAttribute) != null) ? model : null;
            }).Where(static model => model != null);
        context.RegisterSourceOutput(models, static (output, model) => DataTableEmitter.Emit(output, model!));
    }
}
