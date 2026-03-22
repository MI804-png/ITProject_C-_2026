using System.ComponentModel.DataAnnotations;

namespace ITHelpdesk.Models;

public class Ticket
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public string? AssignedToUserId { get; set; }

    [Required]
    public int TicketStatusId { get; set; } = TicketStatusValues.OpenId;

    [Required]
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    public DateTime? DueAtUtc { get; set; }

    public string? ScreenshotPath { get; set; }

    public TicketStatus? Status { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketActivity> Activities { get; set; } = new List<TicketActivity>();
}
