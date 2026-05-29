using MapcelErrorTracker.Exceptions;
using Microsoft.AspNetCore.Mvc;
using MapcelErrorTracker.Models;
using MapcelErrorTracker.Services;

namespace MapcelErrorTracker.Controllers;

public class ErrorsController(
    IErrorService service,
    IUsersService usersService,
    ILogger<ErrorsController> logger) : Controller
{
    public async Task<IActionResult> Index(ErrorListQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var model = await service.GetListAsync(query, cancellationToken);
            return View(model);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load the errors index.");
            return View(ErrorListViewModel.Error(query, "No se pudo cargar la lista de errores. Intenta de nuevo."));
        }
    }

    [HttpGet("Errors/Details/{id:long:min(1)}")]
    public async Task<IActionResult> Details(long id, string? returnUrl, CancellationToken cancellationToken)
    {
        try
        {
            var error = await service.GetByIdAsync(id, cancellationToken);
            error.AvailableAssignees = (await usersService.GetAllAsync(cancellationToken)).ToList();
            ViewData["ReturnUrl"] = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : null;
            return View(error);
        }
        catch (NotFoundException)
        {
            logger.LogWarning("Error with id {ErrorId} does not exist.", id);
            return NotFound();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            logger.LogWarning(exception, "Invalid error id {ErrorId} requested.", id);
            return BadRequest("Invalid error id.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load error details for id {ErrorId}.", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("api/v1/errors/{id:long:min(1)}")]
    public async Task<ActionResult<ErrorItem>> GetById(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await service.GetByIdAsync(id, cancellationToken);
            return Ok(response);
        }
        catch (NotFoundException)
        {
            logger.LogWarning("Error with id {ErrorId} does not exist.", id);
            return NotFound("Error item with id " + id + " not found.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load API error response for id {ErrorId}.", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(
        long id,
        string status,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryParseStatus(status, out var parsed))
        {
            logger.LogWarning("Invalid status value {Status} received for error {ErrorId}.", status, id);
            return RedirectToDetails(id, returnUrl);
        }

        try
        {
            await service.UpdateStatusAsync(id, parsed, cancellationToken);
            if (parsed.Equals(ErrorStatus.Resuelto))
            {
                await service.ResolveErrorAsync(id, cancellationToken);
            }
            else
            {
                await service.UnresolveErrorAsync(id, cancellationToken);
            }
        }
        catch (NotFoundException)
        {
            logger.LogWarning("Status update failed because error {ErrorId} does not exist.", id);
            return NotFound();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            logger.LogWarning(exception, "Invalid error id {ErrorId} received during status update.", id);
            return BadRequest("Invalid error id.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to update status for error {ErrorId}.", id);
            return StatusCode(500, "Internal server error");
        }

        return RedirectToDetails(id, returnUrl, refreshParent: true);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatusFromList(
        long id,
        string status,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryParseStatus(status, out var parsed))
        {
            logger.LogWarning("Invalid status value {Status} received for error {ErrorId}.", status, id);
            return RedirectToIndexOrReturnUrl(returnUrl);
        }

        try
        {
            await service.UpdateStatusAsync(id, parsed, cancellationToken);
        }
        catch (NotFoundException)
        {
            logger.LogWarning("Status update failed because error {ErrorId} does not exist.", id);
            return NotFound();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            logger.LogWarning(exception, "Invalid error id {ErrorId} received during status update.", id);
            return BadRequest("Invalid error id.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to update status for error {ErrorId}.", id);
            return StatusCode(500, "Internal server error");
        }

        return RedirectToIndexOrReturnUrl(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePriority(
        long id,
        string priority,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ErrorPriority>(priority, out var parsed))
        {
            logger.LogWarning("Invalid priority value {Priority} received for error {ErrorId}.", priority, id);
            return RedirectToDetails(id, returnUrl);
        }

        try
        {
            await service.UpdatePriorityAsync(id, parsed, cancellationToken);
        }
        catch (NotFoundException)
        {
            logger.LogWarning("Priority update failed because error {ErrorId} does not exist.", id);
            return NotFound();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            logger.LogWarning(exception, "Invalid error id {ErrorId} received during priority update.", id);
            return BadRequest("Invalid error id.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to update priority for error {ErrorId}.", id);
            return StatusCode(500, "Internal server error");
        }

        return RedirectToDetails(id, returnUrl, refreshParent: true);
    }

    [HttpPost]
    public async Task<IActionResult> AssignUsers(
        long id,
        int[]? userIds,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.AssignUsersAsync(id, userIds ?? [], cancellationToken);
        }
        catch (NotFoundException)
        {
            logger.LogWarning("Assignment update failed because error {ErrorId} does not exist.", id);
            return NotFound();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            logger.LogWarning(
                exception,
                "Invalid assignment values received for error {ErrorId}.",
                id);
            return BadRequest("Invalid assignment.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to assign programmers to error {ErrorId}.", id);
            return StatusCode(500, "Internal server error");
        }

        return RedirectToDetails(id, returnUrl, refreshParent: true);
    }

    private static bool TryParseStatus(string? status, out ErrorStatus parsed)
    {
        parsed = default;

        return !string.IsNullOrWhiteSpace(status) &&
               Enum.TryParse(status, ignoreCase: true, out parsed) &&
               Enum.IsDefined(parsed);
    }

    private IActionResult RedirectToIndexOrReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToDetails(long id, string? returnUrl, bool refreshParent = false)
    {
        return RedirectToAction(nameof(Details), new
        {
            id,
            returnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : null,
            refreshParent = refreshParent ? true : (bool?)null
        });
    }
}
