namespace ITHelpdesk.Models.ViewModels;

public class HomeIndexViewModel
{
    public bool IsAuthenticated { get; set; }
    public List<HomeTicketOptionViewModel> AvailableTickets { get; set; } = new();
    public int? SelectedTicketId { get; set; }
    public string? AssistantResponse { get; set; }
    public string? SelectedTicketTitle { get; set; }
    public bool CanUseAssistant => IsAuthenticated && AvailableTickets.Count > 0;
}
