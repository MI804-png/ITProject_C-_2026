using System.ComponentModel.DataAnnotations;

namespace ITHelpdesk.Models;

public class TicketStatus
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
