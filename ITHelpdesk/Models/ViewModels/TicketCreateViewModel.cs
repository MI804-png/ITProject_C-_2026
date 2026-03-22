using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ITHelpdesk.Models.ViewModels;

public class TicketCreateViewModel
{
    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Priority")]
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    [Display(Name = "Target Due Date")]
    [DataType(DataType.DateTime)]
    public DateTime? DueAtUtc { get; set; }

    public IFormFile? Screenshot { get; set; }
}
