[![](https://img.shields.io/nuget/v/soenneker.datatables.gen.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.datatables.gen/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.datatables.gen/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.datatables.gen/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.datatables.gen.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.datatables.gen/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.DataTables.Gen
### Compile-time DataTables column mappings, permissions, and typed search expressions.

## Installation

```
dotnet add package Soenneker.DataTables.Gen
```

## Usage

Reference this package as an analyzer and reference the column attributes in your application:

```xml
<PackageReference Include="Soenneker.DataTables.Gen" Version="11.0.0" PrivateAssets="all" />
<PackageReference Include="Soenneker.DataTables.Attributes.Column" Version="11.0.38" />
<PackageReference Include="Soenneker.DataTables.Dtos.Column" Version="11.0.40" />
<PackageReference Include="Soenneker.DataTables.Dtos.ServerSideRequest" Version="11.0.41" />
<PackageReference Include="Soenneker.Dtos.RequestDataOptions" Version="11.0.245" />
```

Install the generator in the project containing your existing model sources. Non-generic, top-level classes, records, and structs with `DataTableColumn`, `DataTableSearchable`, or `DataTableOrderable` properties are discovered automatically, including inherited attributes and partial declarations. No generator marker or replacement model is required.

```csharp
using Soenneker.DataTables.Attributes.Column;
public sealed class PhoneNumber
{
    [DataTableColumn(Searchable = true, Orderable = true)]
    public string? Label { get; set; }

    [DataTableColumn(Searchable = true)]
    public string? Number { get; set; }
}
```

The generator creates `PhoneNumberDataTable` in the model's namespace:

```csharp
var columns = PhoneNumberDataTable.Columns;
var fields = PhoneNumberDataTable.SearchableFields;
var options = PhoneNumberDataTable.ToRequestDataOptions(request,
    includeUnlistedSearchableColumns: true);
// Uses Soenneker.Extensions.IQueryables:
var results = query.AddRequestDataOptions(options).Take(20);
```

`Columns` and `CreateColumns()` return fresh lists of the existing `DataTableColumn` DTO. Display settings such as title, width, visibility, and explicit column order come from the column attributes. Changing these DTOs does not change generated permissions. `FieldMappings` is a separate read-only dictionary from external column data names to mapped server field paths. `JsonPropertyName` controls external names and `Soenneker.Attributes.MapTo` controls mapped field paths. The separate `DataTableSearchable` and `DataTableOrderable` attributes are also supported; a combined `DataTableColumn` attribute takes precedence. Unmarked properties are neither searchable nor orderable. There are no special permissions for names such as `Id` or `Name`.

Automatic discovery emits column DTOs, field permissions, and the request adapter. It uses the existing request/query contracts rather than imposing a second query engine. This also supports client-side DTOs whose `MapTo` paths refer to server documents.

Optional typed predicates are enabled explicitly with `[GenerateDataTable(GenerateSearchExpressions = true)]`. They access the annotated model itself and accept either column names or mapped field paths. `[GenerateDataTable]` can also configure nested property discovery or disable the request adapter. Existing models do not need this attribute for the default flow.

## Server-side requests

```csharp
RequestDataOptions options = PhoneNumberDataTable.ToRequestDataOptions(request);
```

The adapter accepts `DataTableServerSideRequest` and produces the existing `RequestDataOptions`, `ExactMatchFilter`, and `OrderByOption` DTOs. It preserves the continuation token, clamps page size to 1–250 (default 50), trims search terms, and validates requested columns against generated permissions. Invalid fields, sort indexes, and sort directions are ignored.

Use `includeUnlistedSearchableColumns: true` to include all attributed searchable fields, such as a label rendered inside a phone-number column. By default, global search respects the request's searchable column selection. `GenerateRequestMapping = false` disables the adapter when only column metadata or typed predicates are needed; the server-request and request-options package references can then be omitted.

## Razor scope

The current Roslyn generator discovers models declared in `.cs` files. Razor components can consume their generated companions. It does not discover table markup or members introduced by Razor generation in the same compilation. Supporting `.razor` declarations requires a Razor-aware input stage; this package currently has no such stage.

## Optional typed matching

The following applies only when `GenerateSearchExpressions` is enabled. The default request adapter leaves matching behavior to the existing repository/query library.

- Every whitespace-separated term must match at least one permitted field. Matching is case-insensitive by default.
- `ApplySearch` composes an `IQueryable` predicate without fetching rows, compiling delegates, or using reflection. Apply it before pagination.
- Supply `fields` to restrict the search. Unknown or non-searchable fields never expand permissions; an empty field list matches no rows for a nonempty search.
- Clearing the search returns the original query.
- Punctuation is preserved unless `DataTableSearchOptions.RemoveCharacters` explicitly removes it from both the value and the term. `IgnoreCase = false` enables case-sensitive matching for that property.
- Strings, booleans, nullable booleans, native enums, and string-valued Soenneker EnumValues are supported. EnumValues are compared as scalar instances rather than querying a synthetic `.Value` document path.
- `IncludeNestedProperties = true` includes attributed properties of nested objects and generates null checks. Collections, generic nested objects, and cycles are not traversed.
- Inherited public properties and conditional `JsonIgnore` attributes are supported; unconditionally ignored properties are excluded.

The query provider must translate the generated operations (`Contains`, `ToLower`, optional `Replace`, and equality). This package does not depend on Quark, Cosmos, or a particular repository. Provider-specific query behavior remains the consumer's responsibility. No persisted search field is required.

## Diagnostics

| Code | Meaning |
| --- | --- |
| SDTG001 | Unsupported generic, nested, file-local, static, or ref-like model |
| SDTG002 | Duplicate column name or mapped field path, compared without case sensitivity |
| SDTG003 | A searchable property has an unsupported type; use metadata-only mode or change its search declaration |
| SDTG004 | The generated companion name is already declared |

The generator references the netstandard2.0 DataTables attribute packages and uses the shared `DataTableColumn` DTO internally. Its package includes the compiler-time dependencies beside the analyzer; consuming applications do not reference the generator assembly at runtime. It is built with Roslyn 5.9 and requires a compatible compiler host.
