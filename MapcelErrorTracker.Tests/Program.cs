using System.Reflection;
using MapcelErrorTracker.Models;
using MapcelErrorTracker.Services;

var tests = new (string Name, Action Run)[]
{
    ("normalizes paging and case-insensitive sort fields", NormalizesPagingAndSortFields),
    ("forces importance sort to descending", ForcesImportanceSortDescending),
    ("keeps default status as open unresolved", KeepsDefaultStatusOpen),
    ("supports all statuses explicitly", SupportsAllStatusesExplicitly),
    ("normalizes explicit status and priority filters", NormalizesExplicitFilters),
    ("rejects invalid numeric priority filters", RejectsInvalidNumericPriority),
    ("builds filtered SQL with safe ORDER BY and paging", BuildsFilteredSql),
    ("does not duplicate last seen in default order", DoesNotDuplicateLastSeenOrder)
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(exception);
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Failures:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    Environment.Exit(1);
}

static void NormalizesPagingAndSortFields()
{
    var query = new ErrorListQuery
    {
        SortBy = "PRIORITY",
        SortDirection = "asc",
        Page = -5,
        PageSize = 500
    };

    var normalized = Normalize(query);

    AssertEqual(ErrorListSortFields.Priority, query.SortBy);
    AssertEqual("asc", query.SortDirection);
    AssertEqual(1, query.Page);
    AssertEqual(50, query.PageSize);
    AssertEqual(ErrorListSortFields.Priority, GetProperty<string>(normalized, "SortBy"));
    AssertEqual(50, GetProperty<int>(normalized, "PageSize"));
}

static void ForcesImportanceSortDescending()
{
    var query = new ErrorListQuery
    {
        SortBy = ErrorListSortFields.Importance,
        SortDirection = "asc"
    };

    var normalized = Normalize(query);

    AssertEqual("desc", query.SortDirection);
    AssertEqual("desc", GetProperty<string>(normalized, "SortDirection"));
}

static void KeepsDefaultStatusOpen()
{
    var query = new ErrorListQuery();
    var normalized = Normalize(query);

    AssertNull(query.Status);
    AssertNull(GetProperty<object?>(normalized, "Status"));
    AssertFalse(GetProperty<bool>(normalized, "IncludeAllStatuses"));
}

static void SupportsAllStatusesExplicitly()
{
    var query = new ErrorListQuery
    {
        Status = ErrorListQuery.AllStatusesValue
    };

    var normalized = Normalize(query);

    AssertEqual(ErrorListQuery.AllStatusesValue, query.Status);
    AssertTrue(GetProperty<bool>(normalized, "IncludeAllStatuses"));
}

static void NormalizesExplicitFilters()
{
    var query = new ErrorListQuery
    {
        Status = "enrevision",
        Priority = "alta"
    };

    var normalized = Normalize(query);

    AssertEqual(ErrorStatus.EnRevision.ToString(), query.Status);
    AssertEqual(ErrorPriority.Alta.ToString(), query.Priority);
    AssertEqual(ErrorStatus.EnRevision, GetProperty<ErrorStatus>(normalized, "Status"));
    AssertEqual(ErrorPriority.Alta, GetProperty<ErrorPriority>(normalized, "Priority"));
}

static void RejectsInvalidNumericPriority()
{
    var query = new ErrorListQuery
    {
        Priority = "999"
    };

    var normalized = Normalize(query);

    AssertNull(query.Priority);
    AssertNull(GetProperty<object?>(normalized, "Priority"));
}

static void BuildsFilteredSql()
{
    var query = new ErrorListQuery
    {
        Search = "abc",
        Program = "main",
        Status = ErrorStatus.Resuelto.ToString(),
        Priority = ErrorPriority.Media.ToString(),
        SortBy = "priority",
        SortDirection = "asc",
        Page = 2,
        PageSize = 25
    };
    var normalized = Normalize(query);
    var sql = BuildSql(normalized);

    AssertContains("COUNT_BIG(*)", sql);
    AssertContains("LIKE @search ESCAPE '\\'", sql);
    AssertContains("LIKE @program ESCAPE '\\'", sql);
    AssertContains("= @statusRank", sql);
    AssertContains("= @priorityRank", sql);
    AssertContains("ORDER BY [PriorityRank] ASC", sql);
    AssertContains("OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", sql);
    AssertDoesNotContain("SELECT [Company]", sql);
}

static void DoesNotDuplicateLastSeenOrder()
{
    var query = new ErrorListQuery();
    var normalized = Normalize(query);
    var sql = BuildSql(normalized);

    AssertContains("ORDER BY [LastSeen] DESC, [err_CodigoError] ASC", sql);
    AssertDoesNotContain("[LastSeen] DESC, [LastSeen] DESC", sql);
}

static object Normalize(ErrorListQuery query)
{
    var method = typeof(ErrorService).GetMethod(
        "NormalizeListQuery",
        BindingFlags.NonPublic | BindingFlags.Static);

    return method?.Invoke(null, [query])
        ?? throw new InvalidOperationException("NormalizeListQuery could not be invoked.");
}

static string BuildSql(object normalizedQuery)
{
    var method = typeof(ErrorService).GetMethod(
        "BuildListPageSql",
        BindingFlags.NonPublic | BindingFlags.Static);

    return method?.Invoke(null, [normalizedQuery]) as string
        ?? throw new InvalidOperationException("BuildListPageSql could not be invoked.");
}

static T GetProperty<T>(object instance, string propertyName)
{
    var property = instance.GetType().GetProperty(propertyName)
        ?? throw new InvalidOperationException($"Property {propertyName} was not found.");
    var value = property.GetValue(instance);

    if (value is null)
    {
        if (default(T) is null)
        {
            return default!;
        }

        throw new InvalidOperationException($"Property {propertyName} was null.");
    }

    return (T)value;
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void AssertNull(object? actual)
{
    if (actual is not null)
    {
        throw new InvalidOperationException($"Expected null, got {actual}.");
    }
}

static void AssertTrue(bool actual)
{
    if (!actual)
    {
        throw new InvalidOperationException("Expected true, got false.");
    }
}

static void AssertFalse(bool actual)
{
    if (actual)
    {
        throw new InvalidOperationException("Expected false, got true.");
    }
}

static void AssertContains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected SQL to contain: {expected}");
    }
}

static void AssertDoesNotContain(string unexpected, string actual)
{
    if (actual.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected SQL not to contain: {unexpected}");
    }
}
