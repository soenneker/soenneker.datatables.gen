using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Soenneker.DataTables.Gen.SymbolMetadata;

namespace Soenneker.DataTables.Gen;

internal static class ColumnDtoEmitter
{
    internal static void Emit(StringBuilder code, List<IPropertySymbol[]> columns)
    {
        code.Append("""
                /// <summary>Creates fresh DataTableColumn DTOs so callers can customize display settings without mutating generated permissions.</summary>
                public static global::System.Collections.Generic.List<global::Soenneker.DataTables.Dtos.Column.DataTableColumn> Columns => CreateColumns();
                /// <summary>Creates DataTables column DTOs in their declared display order.</summary>
                public static global::System.Collections.Generic.List<global::Soenneker.DataTables.Dtos.Column.DataTableColumn> CreateColumns() =>
                    new global::System.Collections.Generic.List<global::Soenneker.DataTables.Dtos.Column.DataTableColumn>
                    {
            """);
        foreach (IPropertySymbol[] column in columns.OrderBy(DisplayOrder))
        {
            AttributeData? attribute = Attribute(column.Last(), ColumnAttribute);
            TypedConstant data = attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == "Data").Value ?? default;
            code.Append("        new global::Soenneker.DataTables.Dtos.Column.DataTableColumn\n        {\n            Data = ")
                .Append(data.Kind != TypedConstantKind.Array && data.Value == null ? Quote(ExternalName(column)) : data.ToCSharpString()).Append(",\n            Searchable = ")
                .Append(Column(column).Searchable ? "true" : "false").Append(",\n            Orderable = ").Append(Column(column).Orderable ? "true" : "false").Append(",\n");
            if (attribute != null)
                foreach (var pair in attribute.NamedArguments)
                    if (pair.Key != "Data" && pair.Key != "Searchable" && pair.Key != "Orderable")
                        code.Append("            ").Append(pair.Key).Append(" = ").Append(pair.Value.ToCSharpString()).Append(",\n");
            code.Append("        },\n");
        }
        code.Append("    };\n");
        code.Append("""
                /// <summary>Maps external column names to server field paths independently of UI display settings.</summary>
                public static global::System.Collections.Generic.IReadOnlyDictionary<string, string> FieldMappings { get; } =
                    new global::System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                        new global::System.Collections.Generic.Dictionary<string, string>(global::System.StringComparer.OrdinalIgnoreCase)
                        {
            """);
        foreach (IPropertySymbol[] column in columns)
            code.Append("            [").Append(Quote(ExternalName(column))).Append("] = ").Append(Quote(Field(column))).Append(",\n");
        code.Append("        });\n");
    }

    private static int DisplayOrder(IPropertySymbol[] column) => Column(column).Order == -1 ? int.MaxValue : Column(column).Order;
}
