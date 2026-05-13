namespace MapcelErrorTracker.Exceptions;

public sealed class NotFoundException(string resourceName)
    : Exception($"Resource not found: {resourceName}");