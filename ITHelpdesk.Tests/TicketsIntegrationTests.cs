using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ITHelpdesk.Tests;

public class TicketsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TicketsIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TicketsIndex_ReturnsUnauthorized_WhenMissingAuthHeader()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/Tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TicketCrudWorkflow_Works_ForStaffUser()
    {
        using var client = CreateAuthenticatedClient("staff-user-1", "Admin,Technician");

        var createGet = await client.GetAsync("/Tickets/Create");
        Assert.Equal(HttpStatusCode.OK, createGet.StatusCode);

        var createToken = await ExtractAntiForgeryTokenAsync(createGet);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        var createResponse = await client.PostAsync(
            "/Tickets/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = createToken,
                ["Title"] = $"Integration Ticket {stamp}",
                ["Description"] = "Created by integration test.",
                ["Priority"] = "3",
                ["DueAtUtc"] = ""
            }));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var redirectLocation = createResponse.Headers.Location!;
        var detailsUri = redirectLocation.IsAbsoluteUri
            ? redirectLocation
            : new Uri(client.BaseAddress!, redirectLocation);
        var detailsPath = detailsUri.PathAndQuery;

        var idMatch = Regex.Match(detailsPath, @"/Tickets/Details/(\d+)(?:\?|$)");
        if (!idMatch.Success)
        {
            idMatch = Regex.Match(detailsPath, @"[?&]id=(\d+)");
        }

        Assert.True(idMatch.Success, $"Could not parse ticket id from redirect path: {detailsPath}");
        var ticketId = idMatch.Groups[1].Value;
        Assert.NotEqual("0", ticketId);

        var detailsGet = await client.GetAsync(detailsPath);
        Assert.True(detailsGet.StatusCode == HttpStatusCode.OK, $"Details request failed. Status={detailsGet.StatusCode}, Path={detailsPath}, TicketId={ticketId}");

        var detailsToken = await ExtractAntiForgeryTokenAsync(detailsGet);
        var commentResponse = await client.PostAsync(
            "/Tickets/AddComment",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = detailsToken,
                ["ticketId"] = ticketId,
                ["commentText"] = "Integration test comment"
            }));
        Assert.Equal(HttpStatusCode.Redirect, commentResponse.StatusCode);

        var detailsGet2 = await client.GetAsync($"/Tickets/Details/{ticketId}");
        var detailsToken2 = await ExtractAntiForgeryTokenAsync(detailsGet2);
        var workflowResponse = await client.PostAsync(
            "/Tickets/UpdateWorkflow",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = detailsToken2,
                ["ticketId"] = ticketId,
                ["ticketStatusId"] = "2",
                ["priority"] = "4",
                ["dueAtUtc"] = "",
                ["assignedToUserId"] = ""
            }));
        Assert.Equal(HttpStatusCode.Redirect, workflowResponse.StatusCode);

        var editGet = await client.GetAsync($"/Tickets/Edit/{ticketId}");
        Assert.Equal(HttpStatusCode.OK, editGet.StatusCode);
        var editToken = await ExtractAntiForgeryTokenAsync(editGet);

        var editResponse = await client.PostAsync(
            $"/Tickets/Edit/{ticketId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = editToken,
                ["Title"] = $"Integration Ticket Edited {stamp}",
                ["Description"] = "Edited by integration test.",
                ["Priority"] = "2",
                ["DueAtUtc"] = ""
            }));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        var detailsGet3 = await client.GetAsync($"/Tickets/Details/{ticketId}");
        var detailsToken3 = await ExtractAntiForgeryTokenAsync(detailsGet3);
        var deleteResponse = await client.PostAsync(
            $"/Tickets/Delete/{ticketId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = detailsToken3,
                ["id"] = ticketId
            }));
        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        var deletedDetails = await client.GetAsync($"/Tickets/Details/{ticketId}");
        Assert.Equal(HttpStatusCode.NotFound, deletedDetails.StatusCode);
    }

    [Fact]
    public async Task CreateTicket_WithInvalidScreenshotType_ShowsValidationError()
    {
        using var client = CreateAuthenticatedClient("staff-user-2", "Admin,Technician");

        var createGet = await client.GetAsync("/Tickets/Create");
        Assert.Equal(HttpStatusCode.OK, createGet.StatusCode);

        var token = await ExtractAntiForgeryTokenAsync(createGet);

        var multipart = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Invalid screenshot test"), "Title" },
            { new StringContent("Try uploading non-image file"), "Description" },
            { new StringContent("2"), "Priority" },
            { new StringContent(string.Empty), "DueAtUtc" }
        };

        var fakeText = new ByteArrayContent("not an image"u8.ToArray());
        fakeText.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        multipart.Add(fakeText, "Screenshot", "bad.txt");

        var response = await client.PostAsync("/Tickets/Create", multipart);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Only png, jpg, jpeg, webp, and gif screenshots are allowed.", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HomeAssistant_ReturnsGuidance_ForSelectedTicket()
    {
        using var client = CreateAuthenticatedClient("staff-user-3", "Admin,Technician");

        var createGet = await client.GetAsync("/Tickets/Create");
        var createToken = await ExtractAntiForgeryTokenAsync(createGet);

        var createResponse = await client.PostAsync(
            "/Tickets/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = createToken,
                ["Title"] = "VPN connection is failing",
                ["Description"] = "User cannot connect to VPN after password reset.",
                ["Priority"] = "4",
                ["DueAtUtc"] = ""
            }));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        var detailsPath = createResponse.Headers.Location!.ToString();
        var idMatch = Regex.Match(detailsPath, @"(\d+)");
        Assert.True(idMatch.Success);
        var ticketId = idMatch.Groups[1].Value;

        var homeGet = await client.GetAsync("/");
        var homeToken = await ExtractAntiForgeryTokenAsync(homeGet);

        var assistantResponse = await client.PostAsync(
            "/Home/AskAssistant",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = homeToken,
                ["ticketId"] = ticketId
            }));

        var html = await assistantResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, assistantResponse.StatusCode);
        Assert.Contains("AI Ticket Assistant", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VPN connection is failing", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Check IP connectivity, DNS resolution, VPN status, and whether other users are affected.", html, StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient CreateAuthenticatedClient(string userId, string roles)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);

        return client;
    }

    private static async Task<string> ExtractAntiForgeryTokenAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"\\s+type=\"hidden\"\\s+value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            match = Regex.Match(
                html,
                "type=\"hidden\"\\s+name=\"__RequestVerificationToken\"\\s+value=\"([^\"]+)\"",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        Assert.True(match.Success, "Anti-forgery token was not found in response HTML.");
        return match.Groups[1].Value;
    }
}
