using ITHelpdesk.Data;
using ITHelpdesk.Models;
using ITHelpdesk.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpdesk.Controllers;

[Authorize]
public class TicketsController : Controller
{
    private static readonly string[] StaffRoles = [AppRoles.Admin, AppRoles.Technician];
    private static readonly HashSet<string> AllowedScreenshotExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif"
    ];

    private const long MaxScreenshotBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public TicketsController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
    }

    public async Task<IActionResult> Index(
        string? searchTerm,
        int? statusId,
        TicketPriority? priority,
        bool onlyUnassigned = false,
        bool onlyMyAssigned = false)
    {
        var currentUserId = _userManager.GetUserId(User)!;
        var canManage = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Technician);

        var visibleTickets = _context.Tickets
            .Include(x => x.Status)
            .AsNoTracking();

        if (!canManage)
        {
            visibleTickets = visibleTickets.Where(x => x.CreatedByUserId == currentUserId);
        }

        var query = visibleTickets;

        if (canManage && onlyUnassigned)
        {
            query = query.Where(x => string.IsNullOrWhiteSpace(x.AssignedToUserId));
        }

        if (canManage && onlyMyAssigned)
        {
            query = query.Where(x => x.AssignedToUserId == currentUserId);
        }

        if (statusId.HasValue)
        {
            query = query.Where(x => x.TicketStatusId == statusId.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(x => x.Priority == priority.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x => x.Title.Contains(term) || x.Description.Contains(term));
        }

        var tickets = await query
            .OrderByDescending(x => x.Priority)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var ids = tickets
            .SelectMany(x => new[] { x.CreatedByUserId, x.AssignedToUserId })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct()
            .ToList();

        var userMap = await _userManager.Users
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email ?? x.UserName ?? x.Id);

        var model = new TicketIndexViewModel
        {
            CanManage = canManage,
            SearchTerm = searchTerm,
            SelectedStatusId = statusId,
            SelectedPriority = priority,
            OnlyUnassigned = onlyUnassigned,
            OnlyMyAssigned = onlyMyAssigned,
            TotalCount = await visibleTickets.CountAsync(),
            OpenCount = await visibleTickets.CountAsync(x => x.TicketStatusId == TicketStatusValues.OpenId),
            InProgressCount = await visibleTickets.CountAsync(x => x.TicketStatusId == TicketStatusValues.InProgressId),
            ResolvedCount = await visibleTickets.CountAsync(x => x.TicketStatusId == TicketStatusValues.ResolvedId),
            UnassignedCount = await visibleTickets.CountAsync(x => string.IsNullOrWhiteSpace(x.AssignedToUserId)),
            OverdueCount = await visibleTickets.CountAsync(x =>
                x.DueAtUtc.HasValue &&
                x.DueAtUtc.Value < DateTime.UtcNow &&
                x.TicketStatusId != TicketStatusValues.ResolvedId),
            Statuses = await _context.TicketStatuses
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync(),
            Tickets = tickets.Select(x => new TicketListItemViewModel
            {
                Id = x.Id,
                Title = x.Title,
                StatusName = x.Status?.Name ?? "Unknown",
                Priority = x.Priority,
                DueAtUtc = x.DueAtUtc,
                IsOverdue = x.DueAtUtc.HasValue &&
                            x.DueAtUtc.Value < DateTime.UtcNow &&
                            x.TicketStatusId != TicketStatusValues.ResolvedId,
                CreatedAtUtc = x.CreatedAtUtc,
                CreatedByDisplayName = userMap.GetValueOrDefault(x.CreatedByUserId, x.CreatedByUserId),
                AssignedToDisplayName = string.IsNullOrWhiteSpace(x.AssignedToUserId)
                    ? null
                    : userMap.GetValueOrDefault(x.AssignedToUserId, x.AssignedToUserId)
            }).ToList()
        };

        return View(model);
    }

    public IActionResult Create()
    {
        return View(new TicketCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TicketCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ticket = new Ticket
        {
            Title = model.Title,
            Description = model.Description,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = _userManager.GetUserId(User)!,
            Priority = model.Priority,
            DueAtUtc = NormalizeToUtc(model.DueAtUtc),
            TicketStatusId = TicketStatusValues.OpenId
        };

        if (model.Screenshot is not null && model.Screenshot.Length > 0)
        {
            try
            {
                ticket.ScreenshotPath = await SaveScreenshotAsync(model.Screenshot);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.Screenshot), ex.Message);
                return View(model);
            }
        }

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        _context.TicketActivities.Add(new TicketActivity
        {
            TicketId = ticket.Id,
            ActorUserId = ticket.CreatedByUserId,
            ActivityType = TicketActivityType.Created,
            Description = "Ticket created.",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        await EnsureSlaBreachActivityAsync(id);

        var ticket = await _context.Tickets
            .Include(x => x.Status)
            .Include(x => x.Comments.OrderByDescending(c => c.CreatedAtUtc))
            .Include(x => x.Activities.OrderByDescending(a => a.CreatedAtUtc))
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ticket is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User)!;
        var canManage = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Technician);
        var canView = canManage || ticket.CreatedByUserId == currentUserId;

        if (!canView)
        {
            return Forbid();
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ticket.CreatedByUserId
        };

        if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId))
        {
            ids.Add(ticket.AssignedToUserId);
        }

        foreach (var comment in ticket.Comments)
        {
            ids.Add(comment.AuthorUserId);
        }

        foreach (var activity in ticket.Activities)
        {
            if (!string.IsNullOrWhiteSpace(activity.ActorUserId))
            {
                ids.Add(activity.ActorUserId);
            }
        }

        var userMap = await _userManager.Users
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email ?? x.UserName ?? x.Id);

        var model = new TicketDetailsViewModel
        {
            Ticket = ticket,
            CanManage = canManage,
            CreatedByDisplayName = userMap.GetValueOrDefault(ticket.CreatedByUserId, ticket.CreatedByUserId),
            AssignedToDisplayName = string.IsNullOrWhiteSpace(ticket.AssignedToUserId)
                ? null
                : userMap.GetValueOrDefault(ticket.AssignedToUserId, ticket.AssignedToUserId),
            CommentAuthorDisplayNames = ticket.Comments
                .Select(x => x.AuthorUserId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x,
                    x => userMap.GetValueOrDefault(x, x),
                    StringComparer.OrdinalIgnoreCase),
            ActivityActorDisplayNames = ticket.Activities
                .Where(x => !string.IsNullOrWhiteSpace(x.ActorUserId))
                .Select(x => x.ActorUserId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x,
                    x => userMap.GetValueOrDefault(x, x),
                    StringComparer.OrdinalIgnoreCase)
        };

        if (canManage)
        {
            model.Statuses = await _context.TicketStatuses
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            var technicians = await _userManager.GetUsersInRoleAsync(AppRoles.Technician);
            model.Technicians = admins
                .Concat(technicians)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .Select(x => new UserOptionViewModel
                {
                    Id = x.Id,
                    DisplayName = x.Email ?? x.UserName ?? x.Id
                })
                .OrderBy(x => x.DisplayName)
                .ToList();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int ticketId, string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User)!;
        var canManage = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Technician);
        var canView = canManage || ticket.CreatedByUserId == currentUserId;

        if (!canView)
        {
            return Forbid();
        }

        _context.TicketComments.Add(new TicketComment
        {
            TicketId = ticketId,
            AuthorUserId = currentUserId,
            CommentText = commentText.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });

        _context.TicketActivities.Add(new TicketActivity
        {
            TicketId = ticketId,
            ActorUserId = currentUserId,
            ActivityType = TicketActivityType.CommentAdded,
            Description = "Comment added.",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Technician)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int ticketId, int ticketStatusId)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null)
        {
            return NotFound();
        }

        var validStatus = await _context.TicketStatuses.AnyAsync(x => x.Id == ticketStatusId);
        if (!validStatus)
        {
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        var oldStatusId = ticket.TicketStatusId;
        ticket.TicketStatusId = ticketStatusId;

        if (oldStatusId != ticketStatusId)
        {
            _context.TicketActivities.Add(new TicketActivity
            {
                TicketId = ticket.Id,
                ActorUserId = _userManager.GetUserId(User),
                ActivityType = TicketActivityType.StatusChanged,
                Description = $"Status changed from {oldStatusId} to {ticketStatusId}.",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await EnsureSlaBreachActivityAsync(ticket);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Technician)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int ticketId, string? assignedToUserId)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(assignedToUserId))
        {
            var canAssignUser = await IsAssignableStaffUserAsync(assignedToUserId);
            if (!canAssignUser)
            {
                return RedirectToAction(nameof(Details), new { id = ticketId });
            }
        }

        var oldAssignee = ticket.AssignedToUserId;
        ticket.AssignedToUserId = assignedToUserId;

        if (!string.Equals(oldAssignee, assignedToUserId, StringComparison.OrdinalIgnoreCase))
        {
            _context.TicketActivities.Add(new TicketActivity
            {
                TicketId = ticket.Id,
                ActorUserId = _userManager.GetUserId(User),
                ActivityType = TicketActivityType.Assigned,
                Description = string.IsNullOrWhiteSpace(assignedToUserId)
                    ? "Ticket unassigned."
                    : "Ticket assigned.",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await EnsureSlaBreachActivityAsync(ticket);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User)!;
        var canManage = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Technician);
        var canEdit = canManage || ticket.CreatedByUserId == currentUserId;

        if (!canEdit)
        {
            return Forbid();
        }

        var model = new TicketCreateViewModel
        {
            Title = ticket.Title,
            Description = ticket.Description,
            Priority = ticket.Priority,
            DueAtUtc = ticket.DueAtUtc?.ToLocalTime()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TicketCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ticket = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User)!;
        var canManage = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Technician);
        var canEdit = canManage || ticket.CreatedByUserId == currentUserId;

        if (!canEdit)
        {
            return Forbid();
        }

        ticket.Title = model.Title;
        ticket.Description = model.Description;
        ticket.Priority = model.Priority;
        ticket.DueAtUtc = NormalizeToUtc(model.DueAtUtc);

        if (model.Screenshot is not null && model.Screenshot.Length > 0)
        {
            try
            {
                ticket.ScreenshotPath = await SaveScreenshotAsync(model.Screenshot);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.Screenshot), ex.Message);
                return View(model);
            }
        }

        _context.TicketActivities.Add(new TicketActivity
        {
            TicketId = ticket.Id,
            ActorUserId = currentUserId,
            ActivityType = TicketActivityType.Updated,
            Description = "Ticket details updated.",
            CreatedAtUtc = DateTime.UtcNow
        });

        await EnsureSlaBreachActivityAsync(ticket);

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Technician)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWorkflow(
        int ticketId,
        int ticketStatusId,
        TicketPriority priority,
        DateTime? dueAtUtc,
        string? assignedToUserId)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null)
        {
            return NotFound();
        }

        var validStatus = await _context.TicketStatuses.AnyAsync(x => x.Id == ticketStatusId);
        if (!validStatus)
        {
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        if (!string.IsNullOrWhiteSpace(assignedToUserId))
        {
            var canAssignUser = await IsAssignableStaffUserAsync(assignedToUserId);
            if (!canAssignUser)
            {
                return RedirectToAction(nameof(Details), new { id = ticketId });
            }
        }

        var oldStatus = ticket.TicketStatusId;
        var oldPriority = ticket.Priority;
        var oldDue = ticket.DueAtUtc;
        var oldAssigned = ticket.AssignedToUserId;

        ticket.TicketStatusId = ticketStatusId;
        ticket.Priority = priority;
        ticket.DueAtUtc = NormalizeToUtc(dueAtUtc);
        ticket.AssignedToUserId = assignedToUserId;

        if (oldStatus != ticket.TicketStatusId ||
            oldPriority != ticket.Priority ||
            oldDue != ticket.DueAtUtc ||
            !string.Equals(oldAssigned, ticket.AssignedToUserId, StringComparison.OrdinalIgnoreCase))
        {
            _context.TicketActivities.Add(new TicketActivity
            {
                TicketId = ticket.Id,
                ActorUserId = _userManager.GetUserId(User),
                ActivityType = TicketActivityType.WorkflowUpdated,
                Description = "Workflow updated (status, priority, due date, or assignment).",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await EnsureSlaBreachActivityAsync(ticket);

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User)!;
        var canManage = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Technician);
        var canDelete = canManage || ticket.CreatedByUserId == currentUserId;

        if (!canDelete)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(ticket.ScreenshotPath))
        {
            var filePath = Path.Combine(_environment.WebRootPath, ticket.ScreenshotPath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        _context.Tickets.Remove(ticket);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SaveScreenshotAsync(IFormFile screenshot)
    {
        if (screenshot.Length > MaxScreenshotBytes)
        {
            throw new InvalidOperationException("Screenshot must be 5 MB or smaller.");
        }

        var extension = Path.GetExtension(screenshot.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedScreenshotExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only png, jpg, jpeg, webp, and gif screenshots are allowed.");
        }

        if (!string.IsNullOrWhiteSpace(screenshot.ContentType) && !screenshot.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Uploaded file must be an image.");
        }

        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "tickets");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(uploadsRoot, fileName);

        await using var stream = System.IO.File.Create(destination);
        await screenshot.CopyToAsync(stream);

        return $"/uploads/tickets/{fileName}";
    }

    private async Task<bool> IsAssignableStaffUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        foreach (var role in StaffRoles)
        {
            if (await _userManager.IsInRoleAsync(user, role))
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var dateTime = value.Value;
        if (dateTime.Kind == DateTimeKind.Utc)
        {
            return dateTime;
        }

        if (dateTime.Kind == DateTimeKind.Local)
        {
            return dateTime.ToUniversalTime();
        }

        return DateTime.SpecifyKind(dateTime, DateTimeKind.Local).ToUniversalTime();
    }

    private async Task EnsureSlaBreachActivityAsync(int ticketId)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null)
        {
            return;
        }

        await EnsureSlaBreachActivityAsync(ticket);
        await _context.SaveChangesAsync();
    }

    private async Task EnsureSlaBreachActivityAsync(Ticket ticket)
    {
        var isOverdue =
            ticket.DueAtUtc.HasValue &&
            ticket.DueAtUtc.Value < DateTime.UtcNow &&
            ticket.TicketStatusId != TicketStatusValues.ResolvedId;

        if (!isOverdue)
        {
            return;
        }

        var hasSlaEvent = await _context.TicketActivities
            .AnyAsync(x => x.TicketId == ticket.Id && x.ActivityType == TicketActivityType.SlaBreached);

        if (hasSlaEvent)
        {
            return;
        }

        _context.TicketActivities.Add(new TicketActivity
        {
            TicketId = ticket.Id,
            ActorUserId = null,
            ActivityType = TicketActivityType.SlaBreached,
            Description = "SLA breached: ticket is past due while unresolved.",
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
