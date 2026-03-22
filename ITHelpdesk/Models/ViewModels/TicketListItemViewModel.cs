namespace ITHelpdesk.Models.ViewModels;

public class TicketListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public bool IsOverdue { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string? AssignedToDisplayName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
