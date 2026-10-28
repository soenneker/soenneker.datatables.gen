using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Soenneker.DataTables.Attributes.Column;
using Soenneker.DataTables.Attributes.Searchable;
using Soenneker.DataTables.Attributes.Orderable;

namespace Soenneker.DataTables.Gen;

internal static class SymbolMetadata
{
    internal const string ColumnAttribute = "Soenneker.DataTables.Attributes.Column." + nameof(DataTableColumnAttribute);
    internal const string SearchableAttribute = "Soenneker.DataTables.Attributes.Searchable." + nameof(DataTableSearchableAttribute);
    internal const string OrderableAttribute = "Soenneker.DataTables.Attributes.Orderable." + nameof(DataTableOrderableAttribute);
    internal static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    internal static string Quote(string value) => SymbolDisplay.FormatLiteral(value, true);
    internal static AttributeData? Attribute(ISymbol symbol, string name) => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);
    internal static bool Flag(AttributeData? attribute, string name, bool fallback = false) => attribute?.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is bool value ? value : fallback;
    internal static string? Text(AttributeData? attribute, string name) => attribute?.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value as string;
    internal static string? Argument(AttributeData? attribute) => attribute?.ConstructorArguments.FirstOrDefault().Value as string;
    internal static string JsonName(IPropertySymbol property) => Argument(Attribute(property, "System.Text.Json.Serialization.JsonPropertyNameAttribute")) ?? property.Name;

    internal static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
            foreach (IPropertySymbol property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!names.Add(property.Name) || property.IsStatic || property.IsIndexer || property.GetMethod?.DeclaredAccessibility != Accessibility.Public)
                    continue;
                AttributeData? ignore = Attribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute");
                // JsonIgnore defaults to Always (1); Never and conditional ignores still describe serialized columns.
                if (ignore != null && (!ignore.NamedArguments.Any(a => a.Key == "Condition") || ignore.NamedArguments.Any(a => a.Key == "Condition" && a.Value.Value is int value && value == 1)))
                    continue;
                yield return property;
            }
    }

    internal static List<IPropertySymbol[]> PropertyPaths(INamedTypeSymbol type, bool nested)
    {
        var result = new List<IPropertySymbol[]>();
        Visit(type, new IPropertySymbol[0], nested, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), result);
        return result;
    }

    internal static Soenneker.DataTables.Dtos.Column.DataTableColumn Column(IPropertySymbol[] path)
    {
        IPropertySymbol property = path.Last();
        var column = ColumnMetadataReader.Read(property, string.Join(".", path.Select(JsonName)));
        // Explicit permissions on an object also cover its text leaves. A leaf's own attributes take precedence.
        if (path.Length > 1 && property.Type.SpecialType == SpecialType.System_String &&
            Attribute(property, ColumnAttribute) == null && Attribute(property, SearchableAttribute) == null && Attribute(property, OrderableAttribute) == null)
        {
            var parent = Column(path.Take(path.Length - 1).ToArray());
            column.Searchable = parent.Searchable;
            column.Orderable = parent.Orderable;
        }
        return column;
    }

    internal static string ExternalName(IPropertySymbol[] path) => Column(path).Data as string ?? string.Join(".", path.Select(JsonName));
    internal static string Field(IPropertySymbol[] path) => Argument(Attribute(path.Last(), "Soenneker.Attributes.MapTo.MapToAttribute")) ?? string.Join(".", path.Select(JsonName));
    internal static string Access(IPropertySymbol[] path) => "item." + string.Join(".", path.Select(property => "@" + property.Name));
    internal static string Guard(IPropertySymbol[] path) => string.Concat(Enumerable.Range(1, path.Length - 1)
        .Where(length => path[length - 1].Type.IsReferenceType)
        .Select(length => Access(path.Take(length).ToArray()) + " != null && "));

    private static void Visit(INamedTypeSymbol type, IPropertySymbol[] parents, bool nested, HashSet<ITypeSymbol> ancestors, List<IPropertySymbol[]> paths)
    {
        if (!ancestors.Add(type)) return;
        foreach (IPropertySymbol property in Properties(type))
        {
            IPropertySymbol[] path = parents.Concat(new[] { property }).ToArray();
            paths.Add(path);
            if (nested && property.Type is INamedTypeSymbol child && !child.IsGenericType && child.SpecialType == SpecialType.None &&
                child.TypeKind != TypeKind.Enum && !IsEnumValue(child) && !child.AllInterfaces.Any(i => i.SpecialType == SpecialType.System_Collections_IEnumerable) &&
                !child.ContainingNamespace.ToDisplayString().StartsWith("System", StringComparison.Ordinal))
                Visit(child, path, nested, ancestors, paths);
        }
        ancestors.Remove(type);
    }

    internal static bool IsEnumValue(INamedTypeSymbol type) => type.GetAttributes().Any(a =>
        a.AttributeClass?.MetadataName == "EnumValueAttribute`1" &&
        a.AttributeClass.ContainingNamespace.ToDisplayString() == "Soenneker.Gen.EnumValues" &&
        a.AttributeClass.TypeArguments[0].SpecialType == SpecialType.System_String) ||
        type.GetMembers("Value").OfType<IPropertySymbol>().Any(p => p.Type.SpecialType == SpecialType.System_String) && EnumValues(type).Length > 0;

    internal static IFieldSymbol[] EnumValues(INamedTypeSymbol type) => type.GetMembers().OfType<IFieldSymbol>().Where(f =>
        f.IsStatic && f.DeclaredAccessibility == Accessibility.Public && SymbolEqualityComparer.Default.Equals(f.Type, type)).ToArray();
}
