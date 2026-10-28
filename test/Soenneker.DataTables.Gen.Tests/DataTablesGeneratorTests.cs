using System;
using System.Linq;

namespace Soenneker.DataTables.Gen.Tests;

public sealed class DataTablesGeneratorTests
{
    [Test]
    public void Explicitly_searchable_nested_column_matches_string_leaves_and_handles_nulls()
    {
        GeneratorTestCompilation.Execute("""
            using Soenneker.DataTables.Attributes.Column;
            using Soenneker.DataTables.Gen;
            [GenerateDataTable(IncludeNestedProperties = true)]
            public sealed class Row
            {
                [DataTableColumn(Searchable = true)] public Organization? Organization { get; set; }
            }
            public sealed class Organization { public string? Name { get; set; } public string? Id { get; set; } }
            """, """
            using System;
            using System.Linq;
            public static class Probe
            {
                public static void Run()
                {
                    var rows = new[] { new Row(), new Row { Organization = new() { Name = "Downtown", Id = "123" } } }.AsQueryable();
                    if (RowDataTable.ApplySearch(rows, "DOWNTOWN").Count() != 1) throw new Exception("Nested label did not match");
                    if (RowDataTable.ApplySearch(rows, "123").Count() != 1) throw new Exception("Nested ID did not match");
                    if (RowDataTable.ApplySearch(rows, "Downtown", ["Organization.Name"]).Count() != 1) throw new Exception("Nested path did not match");
                    if (RowDataTable.ApplySearch(rows, "missing").Any()) throw new Exception("Unknown value matched");
                }
            }
            """);
    }

    [Test]
    public void Existing_attributed_model_works_with_repository_options_without_a_generator_marker()
    {
        GeneratorTestCompilation.Execute("""
            using Soenneker.DataTables.Attributes.Column;
            public sealed class PhoneNumber
            {
                [DataTableColumn(Searchable = true, Orderable = true)] public string? Label { get; set; }
                [DataTableColumn(Searchable = true)] public string? Number { get; set; }
            }
            """, """
            using System;
            using System.Linq;
            using Soenneker.Extensions.IQueryables;
            using Soenneker.DataTables.Dtos.ServerSideRequest;
            public static class Probe
            {
                public static void Run()
                {
                    var request = new DataTableServerSideRequest
                    {
                        Length = 10, Search = new() { Value = "Downtown" },
                        Columns = [new() { Data = "Number", Searchable = true }]
                    };
                    var options = PhoneNumberDataTable.ToRequestDataOptions(request, includeUnlistedSearchableColumns: true);
                    var rows = new[] { new PhoneNumber { Label = "Downtown", Number = "123" }, new PhoneNumber { Label = "Uptown", Number = "456" } }.AsQueryable();
                    if (rows.AddRequestDataOptions(options).Single().Number != "123") throw new Exception("Existing repository query did not search the label");
                    if (PhoneNumberDataTable.Columns.Count != 2) throw new Exception("Column DTOs missing");
                }
            }
            """);
    }

    [Test]
    public void Existing_partial_and_inherited_models_are_discovered_once()
    {
        GeneratorTestCompilation.Execute("""
            using Soenneker.DataTables.Attributes.Searchable;
            public class Parent { [DataTableSearchable] public string? Name { get; set; } }
            """, """
            public partial class Child : Parent { }
            """, """
            using Soenneker.DataTables.Attributes.Column;
            public partial class Child { [DataTableColumn(Searchable = true)] public decimal Amount { get; set; } }
            """, """
            using System;
            using System.Linq;
            public static class Probe
            {
                public static void Run()
                {
                    if (!ChildDataTable.SearchableFields.Contains("Name") || !ChildDataTable.SearchableFields.Contains("Amount")) throw new Exception("Existing attributes were ignored");
                    if (ChildDataTable.Columns.Count != 2) throw new Exception("Partial type was duplicated");
                }
            }
            """);
    }

    [Test]
    public void Metadata_uses_real_attributes_and_safe_defaults()
    {
        GeneratorTestCompilation.Execute("""
            using System.Text.Json.Serialization;
            using Soenneker.DataTables.Gen;
            using Soenneker.DataTables.Attributes.Column;
            using Soenneker.Attributes.MapTo;
            [GenerateDataTable(GenerateSearchExpressions = false)]
            public class Row : BaseRow
            {
                [JsonPropertyName("label"), MapTo("contact.name"), DataTableColumn(Searchable = true)] public string Name { get; set; } = "";
                public string Unmarked { get; set; } = "";
                [JsonIgnore, DataTableColumn(Searchable = true)] public string Hidden { get; set; } = "";
                [JsonIgnore(Condition = JsonIgnoreCondition.Never), DataTableColumn(Orderable = true)] public int Count { get; set; }
            }
            """, """
            using Soenneker.DataTables.Attributes.Searchable;
            using Soenneker.DataTables.Attributes.Orderable;
            public class BaseRow
            {
                [DataTableSearchable, DataTableOrderable] public string Inherited { get; set; } = "";
            }
            """, """
            using System;
            public static class Probe
            {
                public static void Run()
                {
                    var columns = System.Linq.Enumerable.ToDictionary(RowDataTable.Columns, c => (string)c.Data!, System.StringComparer.OrdinalIgnoreCase);
                    if (RowDataTable.FieldMappings["LABEL"] != "contact.name" || !columns["label"].Searchable || columns["label"].Orderable) throw new Exception("Mapping or permissions differ");
                    if (columns["Unmarked"].Searchable || columns["Unmarked"].Orderable) throw new Exception("Unsafe defaults");
                    if (columns.ContainsKey("Hidden") || !columns["Count"].Orderable || !columns["Inherited"].Searchable) throw new Exception("Attribute handling failed");
                    columns["label"].Searchable = false;
                    if (!System.Linq.Enumerable.First(RowDataTable.Columns, c => (string)c.Data! == "label").Searchable) throw new Exception("DTO mutation leaked between callers");
                }
            }
            """);
    }

    [Test]
    public void Search_matches_labels_before_paging_and_respects_requested_fields()
    {
        GeneratorTestCompilation.Execute("""
            using Soenneker.DataTables.Gen;
            using Soenneker.DataTables.Attributes.Column;
            [GenerateDataTable]
            public sealed class NumberRow
            {
                [DataTableColumn(Searchable = true)] public string? Label { get; set; }
                [DataTableColumn(Searchable = true)] public string? Number { get; set; }
                public string? Secret { get; set; }
            }
            """, """
            using System;
            using System.Linq;
            public static class Probe
            {
                public static void Run()
                {
                    var rows = new[] { new NumberRow(), new NumberRow { Label = "Other", Secret = "Downtown" }, new NumberRow { Label = "Downtown West", Number = "123" } }.AsQueryable();
                    if (NumberRowDataTable.ApplySearch(rows, " WEST downtown ").Take(1).Single().Number != "123") throw new Exception("Label search failed");
                    if (NumberRowDataTable.ApplySearch(rows, "downtown", new[] { "Number" }).Any()) throw new Exception("Search escaped the requested fields");
                    if (NumberRowDataTable.ApplySearch(rows, "downtown", new[] { "Secret", "missing" }).Any()) throw new Exception("Unknown fields broadened search");
                    if (NumberRowDataTable.ApplySearch(rows, "downtown", Array.Empty<string>()).Any()) throw new Exception("Empty allowlist broadened search");
                    if (NumberRowDataTable.ApplySearch(rows, " ").Count() != 3) throw new Exception("Cleared search filtered rows");
                }
            }
            """);
    }

    [Test]
    public void Normalization_is_opt_in_and_can_be_case_sensitive()
    {
        GeneratorTestCompilation.Execute("""
            using Soenneker.DataTables.Gen;
            using Soenneker.DataTables.Attributes.Column;
            [GenerateDataTable]
            public sealed class Row
            {
                [DataTableColumn(Searchable = true)] public string Label { get; set; } = "A-B";
                [DataTableColumn(Searchable = true), DataTableSearchOptions(RemoveCharacters = "()+-.")] public string Phone { get; set; } = "+13125550123";
                [DataTableColumn(Searchable = true), DataTableSearchOptions(IgnoreCase = false)] public string Code { get; set; } = "Exact";
            }
            """, """
            using System;
            public static class Probe
            {
                public static void Run()
                {
                    var row = new Row();
                    if (RowDataTable.GetFieldPredicate("Label", "ab")!.Compile()(row)) throw new Exception("Default normalization removed punctuation");
                    if (!RowDataTable.CreateSearchPredicate("(312) 555-0123", new[] { "Phone" }).Compile()(row)) throw new Exception("Phone formatting failed");
                    if (RowDataTable.GetFieldPredicate("Phone", "---")!.Compile()(row)) throw new Exception("Empty normalized search matched everything");
                    if (RowDataTable.GetFieldPredicate("Code", "exact")!.Compile()(row)) throw new Exception("Case-sensitive option ignored");
                }
            }
            """);
    }

    [Test]
    public void Nested_paths_are_null_safe_and_cycles_terminate()
    {
        GeneratorTestCompilation.Execute("""
            using Soenneker.DataTables.Gen;
            [GenerateDataTable(IncludeNestedProperties = true)]
            public sealed class Row { public Contact? Contact { get; set; } public Row? Parent { get; set; } }
            """, """
            using Soenneker.DataTables.Attributes.Column;
            using System.Text.Json.Serialization;
            public sealed class Contact
            {
                [JsonPropertyName("first"), DataTableColumn(Searchable = true)] public string? First { get; set; }
                [DataTableColumn(Searchable = true)] public string? Last { get; set; }
            }
            """, """
            using System;
            public static class Probe
            {
                public static void Run()
                {
                    var match = RowDataTable.CreateSearchPredicate("smith donna").Compile();
                    if (match(new Row())) throw new Exception("Null parent matched");
                    if (!match(new Row { Contact = new Contact { First = "Donna", Last = "Smith" } })) throw new Exception("Nested search failed");
                    if (!RowDataTable.FieldMappings.ContainsKey("Contact.first")) throw new Exception("JSON path missing");
                }
            }
            """);
    }

    [Test]
    public void Enum_values_and_booleans_use_scalar_equality()
    {
        GeneratorTestCompilation.Execute("""
            using Soenneker.DataTables.Gen;
            using Soenneker.DataTables.Attributes.Column;
            [GenerateDataTable]
            public sealed class Row
            {
                [DataTableColumn(Searchable = true)] public Status? Status { get; set; }
                [DataTableColumn(Searchable = true)] public bool? Enabled { get; set; }
            }
            """, """
            [Soenneker.Gen.EnumValues.EnumValue<string>]
            public sealed partial class Status
            {
                public static readonly Status Active = new("Active");
                public static readonly Status Closed = new("Closed");
            }
            """, """
            using System;
            public static class Probe
            {
                public static void Run()
                {
                    var predicate = RowDataTable.GetFieldPredicate("Status", "ACTIVE")!;
                    if (!predicate.Compile()(new Row { Status = Status.Active })) throw new Exception("Enum value search failed");
                    if (predicate.ToString().Contains("item.Status.Value")) throw new Exception("Query accessed a fictitious JSON value property");
                    if (!RowDataTable.GetFieldPredicate("Enabled", "true")!.Compile()(new Row { Enabled = true })) throw new Exception("Boolean search failed");
                    if (RowDataTable.GetFieldPredicate("Enabled", "true")!.Compile()(new Row())) throw new Exception("Null boolean matched");
                }
            }
            """);
    }

    [Test]
    public void Invalid_models_and_ambiguous_fields_produce_diagnostics()
    {
        GeneratorTestCompilation.ExpectDiagnostic("SDTG001", "[Soenneker.DataTables.Gen.GenerateDataTable] public class Row<T> { }");
        GeneratorTestCompilation.ExpectDiagnostic("SDTG002", """
            [Soenneker.DataTables.Gen.GenerateDataTable]
            public class Row { public string? Name { get; set; } public string? name { get; set; } }
            """);
        GeneratorTestCompilation.ExpectDiagnostic("SDTG003", """
            [Soenneker.DataTables.Gen.GenerateDataTable]
            public class Row { [Soenneker.DataTables.Attributes.Column.DataTableColumn(Searchable = true)] public decimal Price { get; set; } }
            """);
        GeneratorTestCompilation.ExpectDiagnostic("SDTG004", "[Soenneker.DataTables.Gen.GenerateDataTable] public class Row { } public class RowDataTable { }");
    }

    [Test]
    public void Existing_request_dtos_map_filters_sorting_and_hidden_search_fields()
    {
        GeneratorTestCompilation.Execute("""
            using Soenneker.DataTables.Gen;
            using Soenneker.DataTables.Attributes.Column;
            using Soenneker.Attributes.MapTo;
            [GenerateDataTable]
            public sealed class Row
            {
                [MapTo("contact.name"), DataTableColumn(Data = "displayName", Searchable = true, Orderable = true, Title = "Contact", Width = "10rem", Order = 0)] public string? Name { get; set; }
                [DataTableColumn(Searchable = true)] public string? Label { get; set; }
                public string? Secret { get; set; }
            }
            """, """
            using System;
            using System.Linq;
            using Soenneker.DataTables.Dtos.ServerSideRequest;
            public static class Probe
            {
                public static void Run()
                {
                    var column = RowDataTable.Columns.First();
                    if ((string)column.Data! != "displayName" || column.Title != "Contact" || column.Width != "10rem") throw new Exception("DTO display metadata missing");
                    var request = new DataTableServerSideRequest
                    {
                        Length = 500, ContinuationToken = "next", Search = new() { Value = " downtown " },
                        Columns = [new() { Data = "displayName", Searchable = true, Orderable = true, Search = new() { Value = " Donna " } }, new() { Data = "Secret", Searchable = true, Orderable = true }],
                        Order = [new() { Column = 0, Dir = "DESC" }, new() { Column = 1, Dir = "asc" }, new() { Column = 99, Dir = "asc" }]
                    };
                    var options = RowDataTable.ToRequestDataOptions(request);
                    if (options.PageSize != 250 || options.ContinuationToken != "next" || options.Search != "downtown") throw new Exception("Request values not preserved");
                    if (!options.SearchFields.SequenceEqual(new[] { "contact.name" })) throw new Exception("Search allowlist ignored");
                    if (options.Filters.Single().Field != "contact.name" || (string)options.Filters.Single().Value! != "Donna") throw new Exception("Column filter mapping failed");
                    if (options.OrderBy.Single().Field != "contact.name" || options.OrderBy.Single().Direction != Soenneker.Enums.SortDirections.SortDirection.Desc) throw new Exception("Sort mapping failed");
                    if (!RowDataTable.ToRequestDataOptions(request, includeUnlistedSearchableColumns: true).SearchFields.Contains("Label")) throw new Exception("Unlisted label not searchable");
                    column.Searchable = false;
                    if (RowDataTable.ToRequestDataOptions(request).SearchFields.Count != 1) throw new Exception("Mutable UI column changed permission");
                }
            }
            """);
    }

    [Test]
    public void Generated_output_has_no_product_or_reflection_dependencies()
    {
        var generated = GeneratorTestCompilation.Generate("[Soenneker.DataTables.Gen.GenerateDataTable] public record Row;");
        string text = string.Join("\n", generated.Result.GeneratedTrees.Select(tree => tree.ToString()));
        foreach (string forbidden in new[] { "Leadping", "SearchText", "System.Reflection", "GetProperties(", "GetCustomAttribute", "Expression.Invoke", "Expression.Compile" })
            if (text.Contains(forbidden, StringComparison.Ordinal)) throw new Exception("Generated output contains " + forbidden);
        GeneratorTestCompilation.Execute("[Soenneker.DataTables.Gen.GenerateDataTable] public record Row;", "public static class Probe { public static void Run() { } }");
    }
}
