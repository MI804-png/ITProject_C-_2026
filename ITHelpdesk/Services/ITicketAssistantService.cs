using ITHelpdesk.Models;

namespace ITHelpdesk.Services;

public interface ITicketAssistantService
{
    string BuildGuidance(Ticket ticket, IReadOnlyCollection<TicketComment> comments);
}
