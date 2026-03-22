using System.ComponentModel.DataAnnotations;

namespace ITHelpdesk.Models;

public class TicketComment
{
    public int Id { get; set; }

    [Required]
    public int TicketId { get; set; }

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string CommentText { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}
