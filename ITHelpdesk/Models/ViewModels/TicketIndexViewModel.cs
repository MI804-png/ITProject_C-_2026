namespace ITHelpdesk.Models.ViewModels;

public class TicketIndexViewModel
{
    public bool CanManage { get; set; }
    public string? SearchTerm { get; set; }
    public int? SelectedStatusId { get; set; }
    public TicketPriority? SelectedPriority { get; set; }
    public bool OnlyUnassigned { get; set; }
    public bool OnlyMyAssigned { get; set; }
    public int TotalCount { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }
    public int UnassignedCount { get; set; }
    public int OverdueCount { get; set; }
    public List<TicketStatus> Statuses { get; set; } = new();
    public List<TicketListItemViewModel> Tickets { get; set; } = new();
}
