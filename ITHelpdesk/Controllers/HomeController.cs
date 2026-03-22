using System.Diagnostics;
using ITHelpdesk.Data;
using ITHelpdesk.Models;
using ITHelpdesk.Models.ViewModels;
using ITHelpdesk.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpdesk.Controllers;

public class HomeController : Controller
{
    private static readonly string[] StaffRoles = [AppRoles.Admin, AppRoles.Technician];

    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ITicketAssistantService _ticketAssistantService;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ITicketAssistantService ticketAssistantService)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _ticketAssistantService = ticketAssistantService;
    }

    public async Task<IActionResult> Index(int? ticketId)
    {
        var model = await BuildHomeIndexViewModelAsync(ticketId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AskAssistant(int ticketId)
    {
        var model = await BuildHomeIndexViewModelAsync(ticketId);
        return View(nameof(Index), model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<HomeIndexViewModel> BuildHomeIndexViewModelAsync(int? selectedTicketId)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var model = new HomeIndexViewModel
        {
            IsAuthenticated = isAuthenticated,
            SelectedTicketId = selectedTicketId
        };

        if (!isAuthenticated)
        {
            return model;
        }

        var currentUserId = _userManager.GetUserId(User)!;
        var canManage = StaffRoles.Any(User.IsInRole);

        var ticketQuery = _context.Tickets
            .Include(x => x.Status)
            .AsNoTracking();

        if (!canManage)
        {
            ticketQuery = ticketQuery.Where(x => x.CreatedByUserId == currentUserId);
        }

        var tickets = await ticketQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(25)
            .ToListAsync();

        model.AvailableTickets = tickets.Select(x => new HomeTicketOptionViewModel
        {
            Id = x.Id,
            Title = x.Title,
            StatusName = x.Status?.Name ?? "Unknown",
            Priority = x.Priority
        }).ToList();

        if (!selectedTicketId.HasValue)
        {
            return model;
        }

        var selectedTicket = tickets.FirstOrDefault(x => x.Id == selectedTicketId.Value);
        if (selectedTicket is null)
        {
            return model;
        }

        var comments = await _context.TicketComments
            .AsNoTracking()
            .Where(x => x.TicketId == selectedTicket.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        model.SelectedTicketTitle = selectedTicket.Title;
        model.AssistantResponse = _ticketAssistantService.BuildGuidance(selectedTicket, comments);

        return model;
    }
}
