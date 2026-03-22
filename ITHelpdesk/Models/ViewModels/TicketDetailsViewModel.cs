namespace ITHelpdesk.Models.ViewModels;

public class TicketDetailsViewModel
{
    public Ticket Ticket { get; set; } = new();
    public bool CanManage { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string? AssignedToDisplayName { get; set; }
    public List<TicketStatus> Statuses { get; set; } = new();
    public List<UserOptionViewModel> Technicians { get; set; } = new();
    public Dictionary<string, string> CommentAuthorDisplayNames { get; set; } = new();
    public Dictionary<string, string> ActivityActorDisplayNames { get; set; } = new();

    public bool IsOverdue =>
        Ticket.DueAtUtc.HasValue &&
        Ticket.DueAtUtc.Value < DateTime.UtcNow &&
        Ticket.TicketStatusId != TicketStatusValues.ResolvedId;
}
