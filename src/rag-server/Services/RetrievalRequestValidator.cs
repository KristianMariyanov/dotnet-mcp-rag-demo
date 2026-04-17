using DotNetConf.RagServer.Contracts;

namespace DotNetConf.RagServer.Services;

public static class RetrievalRequestValidator
{
    public static Dictionary<string, string[]> Validate(RetrievalRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            errors["query"] = ["Query is required."];
            return errors;
        }

        if (request.Query.Length > 800)
        {
            errors["query"] = ["Query must be 800 characters or less."];
        }

        if (request.ResultCount is < 1 or > 10)
        {
            errors["resultCount"] = ["Result count must be between 1 and 10."];
        }

        ValidateFilters(request.Filters, errors);
        return errors;
    }

    private static void ValidateFilters(
        RetrievalFilters? filters,
        IDictionary<string, string[]> errors)
    {
        if (filters is null)
        {
            return;
        }

        ValidateFilterValues("filters.technologies", filters.Technologies, errors);
        ValidateFilterValues("filters.seniority", filters.Seniority, errors);
        ValidateFilterValues("filters.categories", filters.Categories, errors);
        ValidateFilterValues("filters.locations", filters.Locations, errors);
        ValidateFilterValues("filters.workModels", filters.WorkModels, errors);
    }

    private static void ValidateFilterValues(
        string key,
        IReadOnlyList<string> values,
        IDictionary<string, string[]> errors)
    {
        if (values.Count > 12)
        {
            errors[key] = ["A filter can contain at most 12 values."];
            return;
        }

        if (values.Any(static value => string.IsNullOrWhiteSpace(value) || value.Length > 80))
        {
            errors[key] = ["Filter values must be non-empty and 80 characters or less."];
        }
    }
}
