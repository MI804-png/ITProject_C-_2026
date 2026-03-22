using System.ComponentModel.DataAnnotations;

namespace ITHelpdesk.Models;

public class TicketActivity
{
    public int Id { get; set; }

    [Required]
    public int TicketId { get; set; }

    public string? ActorUserId { get; set; }

    [Required]
    public TicketActivityType ActivityType { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}
