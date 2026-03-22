namespace ITHelpdesk.Models;

public enum TicketActivityType
{
    Created = 1,
    Updated = 2,
    CommentAdded = 3,
    StatusChanged = 4,
    Assigned = 5,
    WorkflowUpdated = 6,
    SlaBreached = 7
}
