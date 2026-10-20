# Agender

Minute-level agenda with a task repository (drag & drop onto the agenda) and a note per day.

## Projects
- `Agender.Shared` – domain, service contracts, UI state and all UI (Razor class library)
- `Agender.Infrastructure` – EF Core/SQLite, Identity, per-user data isolation, service implementations
- `Agender.Web` – the server: Blazor Web App, account pages, Minimal API under `/api`
- `Agender` – MAUI Blazor Hybrid app; logs in to the server and uses the same data through the API
- `Agender.Tests` – xUnit tests (domain, services, store, API)

## Run
```
dotnet run --project Agender.Web
dotnet run --project Agender -f net10.0-windows10.0.19041.0
dotnet test
```
The data lives on the server in SQLite (`Agender.Web/App_Data/agender.db`; override with `Agender:DatabasePath`). Migrations run at startup.

Start the server first, create an account at `/conta/registar`, then open the MAUI app, enter the server address (default `http://localhost:5018`) and sign in. Registration can be disabled with `Agender:AllowRegistration=false`.
Android needs an HTTPS server address (cleartext HTTP is blocked by default).

## Add a migration
```
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> -p Agender.Infrastructure -s Agender.Infrastructure -o Data/Migrations
```

## Usage
- Drag on an empty area of a day to create an entry; click an entry to edit it; drag to move; drag its top/bottom edge to resize.
- Drag a task from the sidebar onto the grid to log 30 minutes with the task title.
- Touch screens: long-press a task or an entry to drag it; tap an entry to edit; tap an empty slot to create.
- Click the note at the top of a day to write in place (autosaves).
- Snap precision (1/5/15 min) and theme picker (system, light, dark, ocean, forest, sunset, rose) are in the toolbar.

## Timer
Start a timer from the floating button (bottom right) or from the play button on a task. It is stored on the server, so it keeps running with the app closed and shows on every device. Stopping it creates the entry (split per day if it crosses midnight, minimum 1 minute); completing the task being timed also stops it.

## Google Calendar
Optional and per user, with no configuration on the server. Each user pastes the JSON key of a Google **service account** in Settings and shares a calendar with the service account's email. The toolbar then gets an **Agenda | Google Calendar** switch. In the Google view the agenda shows the Google events of the visible week (all-day events as chips in the day header; events written by the app are marked "Agender") and hides the time entries; the tasks stay in the sidebar. Clicking an event creates a time entry from it (not sent back to Google), clicking an "Agender" event opens its entry, and anything added while the Google view is on (dropping a task, dragging on the grid, "Novo registo") is flagged "Enviar para o Google Calendar" by default, so it shows up in Google. Entries are sent to a calendar (the same one, or a separate one given in the optional second field); the app is the source of truth: edits made in Google to those events are overwritten on the next sync, and a failed sync is retried with "Sincronizar agora" in Settings.

To try the interface without a Google account, start the server with `Agender__DemoGoogle=true`: a fake Google backend with a few recurring events accepts any key and calendar id (development only, nothing leaves the machine).

Setup (once per user, Google Cloud Console and Google Calendar):
1. In a Google Cloud project, enable the **Google Calendar API**.
2. Create a **service account** (IAM & Admin > Service Accounts) and, under Keys, add a **JSON key**. Keep the file private.
3. In Google Calendar, open the calendar's settings > *Share with specific people* and add the service account's email (`client_email` in the JSON) with the permission **Make changes to events**. To keep entries apart from your own events, create a second calendar (e.g. "Agender"), share it the same way and give its id in Settings as the optional "calendar for entries".
4. In Agender, open Settings > Google Calendar, paste the JSON, enter the calendar id (your email for the main calendar; for other calendars it is under *Integrate calendar*) and choose "Ligar ao Google".

The key is stored encrypted with ASP.NET Data Protection and is never sent back to the client, so in production persist the Data Protection key ring or the connection must be redone after a restart. Only keys of type `service_account` are accepted and the server always uses Google's own token endpoint.

## Agenda sharing

Under **Partilhas** (Sharing) you can show your agenda (read-only, without Google Calendar) in two ways:

- **With an Agender user**: enter their email and they can see your agenda under *Partilhas > Ver agenda*. You can remove their access at any time.
- **With a link**: generates a secret address (`/p/<token>`) that works without an account for 2 days. The link is shown only once and only its hash is stored. Generating a new link invalidates the previous one.

### Public access for the link (desktop use)

The desktop launcher starts the server with `Agender:EnableTunnel=true`. When you generate a read-only link, the server starts a free [Cloudflare quick tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/do-more-with-tunnels/trycloudflare/) (`cloudflared`, downloaded to `%LOCALAPPDATA%\Agender` on first use) and the link is built with the public `https://<random>.trycloudflare.com` address. The tunnel stops when the link expires or is disabled. Requests that arrive through the tunnel can only reach the shared agenda page (`/p/...`) and static assets; sign-in, the API and the rest of the app return 404. The computer must stay on while someone is viewing the agenda.

## Agendas

Work is organised in **agendas** (for example *Trabalho* or *Pessoal*); each agenda groups its own projects, tasks and time entries. Every user starts with a default agenda called *Geral*. Manage them under **Agendas** (the former Projects page).

- Several agendas can be shown on the grid at the same time. With two or more agendas the toolbar lists them next to the date navigation: the dot shows or hides an agenda, and clicking the name makes it the **active** one.
- The active agenda is where new tasks, projects, time entries and timers go. The task sidebar, the project filter and the project pickers only show the active agenda's items.
- Entry colours always come from the project. Deleting an agenda moves its projects, tasks and entries to *Geral*. Reports and the shared (read-only) agenda still cover all agendas.
