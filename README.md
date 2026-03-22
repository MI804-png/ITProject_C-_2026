# IT Helpdesk System

A simple but practical IT support ticket system built with ASP.NET Core MVC, Identity, and Entity Framework Core.

This project is designed for internal company support teams that need to track incoming issues, assign ownership, and resolve work without relying on spreadsheets or chat history.

## What This System Does

- Lets employees submit tickets with title, details, optional screenshot, priority, and optional due date.
- Supports role-based access:
  - User: creates and tracks their own tickets.
  - Technician: manages ticket workflow.
  - Admin: full management and role oversight.
- Gives support staff an operational queue with:
  - Search (title/description)
  - Status filter
  - Priority filter
  - "Unassigned only" and "Assigned to me" filters
  - KPI cards (open, in progress, resolved, overdue, etc.)
- Allows comments on tickets for collaboration and handoff context.
- Allows status and assignment updates from the ticket details page.
- Stores screenshot uploads in wwwroot/uploads/tickets with file-size and image-type checks.

## Tech Stack

- ASP.NET Core 8 MVC
- ASP.NET Core Identity (authentication + roles)
- Entity Framework Core (SQL Server)
- Razor Views + Bootstrap

## Project Structure

- ITHelpdesk/: main web application
- ITHelpdesk/Controllers/: request handling and workflow logic
- ITHelpdesk/Models/: domain and view models
- ITHelpdesk/Data/: DbContext, role seeding, migrations
- ITHelpdesk/Views/: Razor UI
- ITHelpdesk/wwwroot/: static assets and uploaded screenshots

## Quick Start (Local)

1. Restore and build:

```bash
dotnet restore ITHelpdesk/ITHelpdesk.csproj
dotnet build ITHelpdesk/ITHelpdesk.csproj
```

2. Apply database migrations:

```bash
dotnet ef database update --project ITHelpdesk/ITHelpdesk.csproj --startup-project ITHelpdesk/ITHelpdesk.csproj
```

3. Run the app:

```bash
dotnet run --project ITHelpdesk/ITHelpdesk.csproj
```

4. Open the app in browser (the URL printed in terminal).

## Seeded Admin Account

The app seeds one admin user from configuration:

- Email: admin@helpdesk.local
- Password: Admin123!Admin123!

This is configured in ITHelpdesk/appsettings.json.

Important:
- Change this password before using outside local/dev.
- Do not keep default credentials in production.

## Environment Notes

Default DB connection in appsettings uses LocalDB:

- Server=(localdb)\\mssqllocaldb
- Database=ITHelpdeskDb

If your machine does not have LocalDB, replace the connection string with your SQL Server instance.

## Security and Validation

- Ticket input uses data annotations and server-side validation.
- Screenshot uploads are restricted by:
  - Extension allowlist: png, jpg, jpeg, webp, gif
  - Max file size: 5 MB
  - Image content type check
- Authorization is enforced at controller/action level.

## Suggested Next Improvements

If you want to evolve this into a stronger production-ready system, prioritize:

1. Ticket activity timeline (who changed what, and when)
2. SLA policy + breach notifications
3. Email notifications for assignment/status changes
4. Soft-delete and retention policy for audit safety
5. Export/reporting dashboard for management metrics

## GitHub Remote

This workspace is connected to:

- https://github.com/MI804-png/ITProject_C-_2026.git

To push for the first time:

```bash
git add .
git commit -m "Initial helpdesk system upgrade"
git branch -M main
git push -u origin main
```
