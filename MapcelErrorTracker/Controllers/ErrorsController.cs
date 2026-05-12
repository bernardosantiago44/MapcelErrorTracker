using MapcelErrorTracker.Exceptions;
using Microsoft.AspNetCore.Mvc;
using MapcelErrorTracker.Models;
using MapcelErrorTracker.Services;
using Serilog;

namespace MapcelErrorTracker.Controllers;

public class ErrorsController(ErrorStore store, IErrorService service) : Controller
{
    public IActionResult Index(ErrorListQuery query)
    {
        try
        {
            return View(store.GetList(query));
        }
        catch
        {
            return View(ErrorListViewModel.Error(query, "No se pudo cargar la lista de errores. Intenta de nuevo."));
        }
    }

    public IActionResult Details(int id)
    {
        var error = store.GetById(id);
        if (error is null) return NotFound();
        return View(error);
    }

    [HttpGet("api/v1/errors/{id:long:min(1)}")]
    public async Task<ActionResult<Dictionary<string, string>>> FindById(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await service.FindByIdAsync(id, cancellationToken);
            return Ok(response);
        }
        catch (NotFoundException)
        {
            Log.Warning("Error with id {id} does not exist", id);
            return NotFound("Error item with id " + id + " not found.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "ErrorsController.FindById: {exception}", exception.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public IActionResult UpdateStatus(int id, string status)
    {
        if (Enum.TryParse<ErrorStatus>(status, out var parsed))
            store.UpdateStatus(id, parsed);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public IActionResult UpdateStatusFromList(int id, string status, string? returnUrl)
    {
        if (Enum.TryParse<ErrorStatus>(status, out var parsed))
            store.UpdateStatus(id, parsed);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult UpdatePriority(int id, string priority)
    {
        if (Enum.TryParse<ErrorPriority>(priority, out var parsed))
            store.UpdatePriority(id, parsed);
        return RedirectToAction(nameof(Details), new { id });
    }
}
