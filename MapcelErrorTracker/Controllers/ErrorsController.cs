using Microsoft.AspNetCore.Mvc;
using MapcelErrorTracker.Models;
using MapcelErrorTracker.Services;

namespace MapcelErrorTracker.Controllers;

public class ErrorsController(ErrorStore store) : Controller
{
    public IActionResult Index(
        string? program, string? status, string? priority, string? search,
        string? company, string? module, string? assignee,
        bool hideResolved = false, bool highPriorityOnly = false)
    {
        var all = store.GetAll();
        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(company))
            query = query.Where(e => e.Company == company);

        if (!string.IsNullOrWhiteSpace(program))
            query = query.Where(e => e.Program == program);

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(e => e.Module == module);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ErrorStatus>(status, out var parsedStatus))
            query = query.Where(e => e.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<ErrorPriority>(priority, out var parsedPriority))
            query = query.Where(e => e.Priority == parsedPriority);

        if (!string.IsNullOrWhiteSpace(assignee))
            query = query.Where(e => e.Assignee == assignee);

        if (hideResolved)
            query = query.Where(e => e.Status != ErrorStatus.Resolved);

        if (highPriorityOnly)
            query = query.Where(e => e.Priority == ErrorPriority.Alta);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Code.ToLowerInvariant().Contains(q) ||
                e.Module.ToLowerInvariant().Contains(q) ||
                e.Description.ToLowerInvariant().Contains(q) ||
                e.Company.ToLowerInvariant().Contains(q) ||
                e.Program.ToLowerInvariant().Contains(q));
        }

        var errors = query.ToList();

        // Dropdown options
        ViewBag.Companies = all.Select(e => e.Company).Distinct().OrderBy(x => x).ToList();
        ViewBag.Programs = all.Select(e => e.Program).Distinct().OrderBy(x => x).ToList();
        ViewBag.Modules = all.Select(e => e.Module).Distinct().OrderBy(x => x).ToList();
        ViewBag.Assignees = all.Where(e => !string.IsNullOrEmpty(e.Assignee))
            .Select(e => e.Assignee).Distinct().OrderBy(x => x).ToList();

        // KPI metrics (computed over all data, not filtered)
        var now = DateTime.UtcNow;
        ViewBag.ActiveCount = all.Count(e => e.Status != ErrorStatus.Resolved);
        ViewBag.NewTodayCount = all.Count(e => e.FirstSeen.Date == now.Date);
        ViewBag.HighPriorityCount = all.Count(e => e.Priority == ErrorPriority.Alta && e.Status != ErrorStatus.Resolved);
        ViewBag.UnassignedCount = all.Count(e => string.IsNullOrEmpty(e.Assignee) && e.Status != ErrorStatus.Resolved);
        ViewBag.PostponedCount = all.Count(e => e.Status == ErrorStatus.Postponed);
        ViewBag.RecurrentCount = all.Count(e => e.Occurrences >= 50);
        ViewBag.SilencedCount = all.Count(e => e.IsSilenced);

        // Selected filter values
        ViewBag.SelectedCompany = company;
        ViewBag.SelectedProgram = program;
        ViewBag.SelectedModule = module;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedPriority = priority;
        ViewBag.SelectedAssignee = assignee;
        ViewBag.Search = search;
        ViewBag.HideResolved = hideResolved;
        ViewBag.HighPriorityOnly = highPriorityOnly;

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
    public IActionResult UpdateStatusFromList(int id, string status)
    {
        if (Enum.TryParse<ErrorStatus>(status, out var parsed))
            store.UpdateStatus(id, parsed);
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
