using MapcelErrorTracker.Models;

namespace MapcelErrorTracker.Services;

public interface IUsersService
{
    Task<IReadOnlyList<ProgrammerUser>> GetAllAsync(CancellationToken cancellationToken);
}
