using System.Text;

namespace Soenneker.DataTables.Gen;

internal static class RequestDtoEmitter
{
    internal static void Emit(StringBuilder code) => code.Append("""
            /// <summary>Maps a DataTables request to repository options using generated permissions. Opt into unlisted searchable columns for labels displayed inside another column.</summary>
            public static global::Soenneker.Dtos.RequestDataOptions.RequestDataOptions ToRequestDataOptions(
                global::Soenneker.DataTables.Dtos.ServerSideRequest.DataTableServerSideRequest request, bool includeUnlistedSearchableColumns = false)
            {
                if (request == null) throw new global::System.ArgumentNullException(nameof(request));
                var options = new global::Soenneker.Dtos.RequestDataOptions.RequestDataOptions
                {
                    PageSize = global::System.Math.Min(250, global::System.Math.Max(1, request.Length > 0 ? request.Length : 50)),
                    ContinuationToken = request.ContinuationToken,
                    Search = string.IsNullOrWhiteSpace(request.Search?.Value) ? null : request.Search!.Value!.Trim(),
                    SearchFields = new global::System.Collections.Generic.List<string>(),
                    Filters = new global::System.Collections.Generic.List<global::Soenneker.Dtos.Filters.ExactMatch.ExactMatchFilter>(),
                    OrderBy = new global::System.Collections.Generic.List<global::Soenneker.Dtos.Options.OrderBy.OrderByOption>()
                };
                var searchable = new global::System.Collections.Generic.HashSet<string>(SearchableFields, global::System.StringComparer.OrdinalIgnoreCase);
                var orderable = new global::System.Collections.Generic.HashSet<string>(OrderableFields, global::System.StringComparer.OrdinalIgnoreCase);
                var selected = new global::System.Collections.Generic.HashSet<string>(global::System.StringComparer.OrdinalIgnoreCase);
                if (options.Search != null && includeUnlistedSearchableColumns)
                    foreach (string field in SearchableFields)
                        if (selected.Add(field)) options.SearchFields.Add(field);
                if (request.Columns == null) return options;
                foreach (var column in request.Columns)
                {
                    if (column.Data == null || !column.Searchable || !FieldMappings.TryGetValue(column.Data, out string? field) || !searchable.Contains(field)) continue;
                    if (options.Search != null && selected.Add(field)) options.SearchFields.Add(field);
                    if (!string.IsNullOrWhiteSpace(column.Search?.Value))
                        options.Filters.Add(new global::Soenneker.Dtos.Filters.ExactMatch.ExactMatchFilter { Field = field, Value = column.Search!.Value!.Trim() });
                }
                if (request.Order == null) return options;
                foreach (var order in request.Order)
                {
                    if (order.Column < 0 || order.Column >= request.Columns.Count) continue;
                    var column = request.Columns[order.Column];
                    if (column.Data == null || !column.Orderable || !FieldMappings.TryGetValue(column.Data, out string? field) || !orderable.Contains(field)) continue;
                    var direction = string.Equals(order.Dir, "asc", global::System.StringComparison.OrdinalIgnoreCase)
                        ? global::Soenneker.Enums.SortDirections.SortDirection.Asc
                        : string.Equals(order.Dir, "desc", global::System.StringComparison.OrdinalIgnoreCase)
                            ? global::Soenneker.Enums.SortDirections.SortDirection.Desc : null;
                    if (direction != null) options.OrderBy.Add(new global::Soenneker.Dtos.Options.OrderBy.OrderByOption { Field = field, Direction = direction });
                }
                return options;
            }
        """);
}
