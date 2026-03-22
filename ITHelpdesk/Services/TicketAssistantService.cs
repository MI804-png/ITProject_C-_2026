using System.Text;
using ITHelpdesk.Models;

namespace ITHelpdesk.Services;

public class TicketAssistantService : ITicketAssistantService
{
    public string BuildGuidance(Ticket ticket, IReadOnlyCollection<TicketComment> comments)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Suggested help for ticket: {ticket.Title}");
        builder.AppendLine();
        builder.AppendLine("What this issue looks like:");
        builder.AppendLine($"- Status: {ticket.Status?.Name ?? "Unknown"}");
        builder.AppendLine($"- Priority: {ticket.Priority}");
        builder.AppendLine($"- Summary: {Summarize(ticket.Description)}");

        builder.AppendLine();
        builder.AppendLine("Recommended next steps:");

        foreach (var step in BuildSteps(ticket, comments))
        {
            builder.AppendLine($"- {step}");
        }

        builder.AppendLine();
        builder.AppendLine("Suggested response to the user:");
        builder.AppendLine(BuildSuggestedResponse(ticket));

        return builder.ToString().Trim();
    }

    private static IEnumerable<string> BuildSteps(Ticket ticket, IReadOnlyCollection<TicketComment> comments)
    {
        var steps = new List<string>
        {
            "Confirm the exact error message, affected device, and when the issue started.",
            "Check whether the issue can be reproduced on another machine or user account.",
            "Review recent changes such as password resets, updates, or network changes."
        };

        var description = ticket.Description.ToLowerInvariant();

        if (description.Contains("password") || description.Contains("login") || description.Contains("sign in"))
        {
            steps.Add("Verify account lockout, password expiration, and multi-factor authentication status.");
        }

        if (description.Contains("printer") || description.Contains("print"))
        {
            steps.Add("Check printer connectivity, default printer selection, and print spooler status.");
        }

        if (description.Contains("network") || description.Contains("internet") || description.Contains("wifi") || description.Contains("vpn"))
        {
            steps.Add("Check IP connectivity, DNS resolution, VPN status, and whether other users are affected.");
        }

        if (description.Contains("slow") || description.Contains("performance") || description.Contains("lag"))
        {
            steps.Add("Review CPU, memory, disk usage, and background applications on the affected machine.");
        }

        if (description.Contains("email") || description.Contains("outlook") || description.Contains("mail"))
        {
            steps.Add("Validate mailbox connectivity, account profile health, and send/receive status.");
        }

        if (ticket.ScreenshotPath is not null)
        {
            steps.Add("Inspect the attached screenshot for exact error wording, prompts, or system state.");
        }

        if (comments.Count > 0)
        {
            steps.Add("Review the latest ticket comments for already attempted fixes and user feedback.");
        }

        if (ticket.Priority is TicketPriority.High or TicketPriority.Critical)
        {
            steps.Add("Treat this as a priority issue and update the user with an initial response quickly.");
        }

        return steps.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildSuggestedResponse(Ticket ticket)
    {
        if (ticket.Priority is TicketPriority.High or TicketPriority.Critical)
        {
            return "We are treating this as a high-priority issue. We are reviewing the reported symptoms and will update you with the next action shortly.";
        }

        return "Thank you for reporting this issue. We are reviewing the details and will follow up with the next troubleshooting step or resolution update.";
    }

    private static string Summarize(string description)
    {
        var normalized = description.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180] + "...";
    }
}
