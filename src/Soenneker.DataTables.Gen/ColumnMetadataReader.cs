using System.Linq;
using Microsoft.CodeAnalysis;
using Soenneker.DataTables.Dtos.Column;
using static Soenneker.DataTables.Gen.SymbolMetadata;

namespace Soenneker.DataTables.Gen;

internal static class ColumnMetadataReader
{
    internal static DataTableColumn Read(IPropertySymbol property, string data)
    {
        var column = new DataTableColumn { Data = data };
        AttributeData? attribute = Attribute(property, ColumnAttribute);
        if (attribute == null)
        {
            column.Searchable = Attribute(property, SearchableAttribute) != null;
            column.Orderable = Attribute(property, OrderableAttribute) != null;
            return column;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            object? value = Value(argument.Value);
            switch (argument.Key)
            {
                case nameof(DataTableColumn.Data): column.Data = value ?? data; break;
                case nameof(DataTableColumn.Name): column.Name = (string?)value; break;
                case nameof(DataTableColumn.Searchable): column.Searchable = (bool)value!; break;
                case nameof(DataTableColumn.Orderable): column.Orderable = (bool)value!; break;
                case nameof(DataTableColumn.Visible): column.Visible = (bool)value!; break;
                case nameof(DataTableColumn.Title): column.Title = (string?)value; break;
                case nameof(DataTableColumn.Width): column.Width = (string?)value; break;
                case nameof(DataTableColumn.ClassName): column.ClassName = (string?)value; break;
                case nameof(DataTableColumn.CellType): column.CellType = (string?)value; break;
                case nameof(DataTableColumn.ContentPadding): column.ContentPadding = (string?)value; break;
                case nameof(DataTableColumn.DefaultContent): column.DefaultContent = (string?)value; break;
                case nameof(DataTableColumn.Type): column.Type = (string?)value; break;
                case nameof(DataTableColumn.Footer): column.Footer = (string?)value; break;
                case nameof(DataTableColumn.AriaTitle): column.AriaTitle = (string?)value; break;
                case nameof(DataTableColumn.OrderData): column.OrderData = value; break;
                case nameof(DataTableColumn.OrderDataType): column.OrderDataType = (string?)value; break;
                case nameof(DataTableColumn.OrderSequence): column.OrderSequence = argument.Value.IsNull ? null : argument.Value.Values.Select(item => (string)item.Value!).ToList(); break;
                case nameof(DataTableColumn.Order): column.Order = (int)value!; break;
                case nameof(DataTableColumn.ResponsivePriority): column.ResponsivePriority = (int)value!; break;
            }
        }
        return column;
    }

    private static object? Value(TypedConstant value) => value.IsNull ? null : value.Kind == TypedConstantKind.Array
        ? value.Values.Select(Value).ToArray()
        : value.Value;
}
