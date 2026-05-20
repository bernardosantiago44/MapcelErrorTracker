using MapcelErrorTracker.Models;
using MapcelErrorTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace MapcelErrorTracker.Controllers;

[ApiController]
public class UsersController(
    IUsersService service,
    ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet("api/v1/users")]
    public async Task<ActionResult<IReadOnlyList<ProgrammerUser>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var users = await service.GetAllAsync(cancellationToken);
            return Ok(users);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load programmer users API response.");
            return StatusCode(500, "Internal server error");
        }
    }
}
