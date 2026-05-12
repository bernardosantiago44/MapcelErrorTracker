using MapcelErrorTracker.Models;

namespace MapcelErrorTracker.Services;

public interface IErrorService
{
    Task<ErrorListViewModel> GetListAsync(ErrorListQuery query, CancellationToken cancellationToken);

    Task<ErrorItem> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<Dictionary<string, string>> FindByIdAsync(long id, CancellationToken cancellationToken);

    Task UpdateStatusAsync(long id, ErrorStatus status, CancellationToken cancellationToken);

    Task UpdatePriorityAsync(long id, ErrorPriority priority, CancellationToken cancellationToken);
}
