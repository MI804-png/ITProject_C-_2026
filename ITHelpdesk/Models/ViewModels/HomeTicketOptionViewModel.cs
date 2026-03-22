namespace ITHelpdesk.Models.ViewModels;

public class HomeTicketOptionViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
}
