using Microsoft.AspNetCore.Mvc;
using MapcelErrorTracker.Models;
using MapcelErrorTracker.Services;

namespace MapcelErrorTracker.Controllers;

public class ErrorsController(ErrorStore store) : Controller
{
    public IActionResult Index(string? program, string? status, string? priority, string? search)
    {
        var query = store.GetAll().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(program))
            query = query.Where(e => e.Program == program);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ErrorStatus>(status, out var parsedStatus))
            query = query.Where(e => e.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<ErrorPriority>(priority, out var parsedPriority))
            query = query.Where(e => e.Priority == parsedPriority);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Code.ToLowerInvariant().Contains(q) ||
                e.Module.ToLowerInvariant().Contains(q) ||
                e.Description.ToLowerInvariant().Contains(q));
        }

        var errors = query.ToList();

        var all = store.GetAll();
        ViewBag.Programs = all.Select(e => e.Program).Distinct().OrderBy(x => x).ToList();
        ViewBag.HighPriorityCount = all.Count(e => e.Priority == ErrorPriority.High && e.Status != ErrorStatus.Resolved);
        ViewBag.SelectedProgram = program;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedPriority = priority;
        ViewBag.Search = search;

        return View(errors);
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
    public IActionResult UpdatePriority(int id, string priority)
    {
        if (Enum.TryParse<ErrorPriority>(priority, out var parsed))
            store.UpdatePriority(id, parsed);
        return RedirectToAction(nameof(Details), new { id });
    }
}
