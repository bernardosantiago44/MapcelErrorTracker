using Microsoft.AspNetCore.Mvc;
using MapcelErrorTracker.Models;
using MapcelErrorTracker.Services;

namespace MapcelErrorTracker.Controllers;

public class ErrorsController(ErrorStore store) : Controller
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
