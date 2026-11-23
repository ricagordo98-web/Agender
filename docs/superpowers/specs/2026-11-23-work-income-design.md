# Work (Income Sources) – Design

Approved by the user on 2026-11-23.

## Goal
Turn the "Trades" section into a "Trabalho" area where the user sees every side income in one place: Trader trades (automatic, gains and losses) plus manual sources such as online surveys, with room to add more sources from the UI. Add a PDF income report for all sources or a selection. Remove the "Trader" agenda from the time agenda.

## Decisions
- Manual entries are dated records (date, amount, platform, optional note); weeks and months are computed from them.
- Each manual entry has a free-text platform (e.g. Prolific, YouGov) with suggestions from previous entries of the same source.
- No pending/received state: an entry counts on its date.
- Approach: generic `IncomeSource` + `IncomeEntry` tables; trades stay in `TraderTrades` and are shown as a built-in source. A pure `IncomeLedger` merges both into one series.
- The Trader integration no longer creates time entries. A migration deletes the trade entries, the Ganho/Perda/Aberta projects and the "Trader" agenda; trades are kept.

## Data model (per user, `ConfigureOwnership`)
- `IncomeSource`: `Id`, `Name` (≤ 60), `Emoji` (≤ 16), `Color` (`EntryColor`), `IsArchived`, `SortOrder`, `CreatedAt`. Name unique per user (case-insensitive).
- `IncomeEntry`: `Id`, `SourceId` (FK, cascade delete), `Date` (`DateOnly`), `Amount` (`decimal`), `Platform` (≤ 60), `Note` (≤ 300), `CreatedAt`, `UpdatedAt`. Index `(UserId, SourceId, Date)`.
- `TraderTrade.TimeEntryId` is removed.

### Migrations
1. `AddIncomeSources`: the two tables.
2. `RemoveTraderAgenda` (same migration is fine): deletes `TimeEntries` referenced by `TraderTrades.TimeEntryId`; for each agenda named "Trader" deletes its Ganho/Perda/Aberta projects, moves anything else left in it to the user's default agenda, re-activates the default agenda if the Trader one was active, deletes the agenda; drops `TraderTrades.TimeEntryId`.

## Services
- `IIncomeService` (Shared) with DTO records in `Contracts/IncomeDtos.cs`:
  - `GetSourcesAsync()`, `CreateSourceAsync(IncomeSourceInput)`, `UpdateSourceAsync(Guid, IncomeSourceInput)`, `DeleteSourceAsync(Guid)`.
  - `GetEntriesAsync(DateOnly from, DateOnly to, Guid? sourceId)`, `CreateEntryAsync(IncomeEntryInput)`, `UpdateEntryAsync(Guid, IncomeEntryInput)`, `DeleteEntryAsync(Guid)`.
  - `GetPlatformsAsync(Guid sourceId)`: distinct platforms, most recently used first.
- `IncomeService` (Infrastructure, EF, `IAgendaDbContextFactory`), validation via `ArgumentException` (amount ≠ 0, lengths, name required, source exists).
- `ApiIncomeService` (MAUI) over HTTP.
- `TraderIngestService` drops `UpsertEntry`/`EnsureAgendaAsync` and the agenda constants.

## API (`/api/income`, authorized)
- `GET/POST /sources`, `PUT/DELETE /sources/{id}`.
- `GET /entries?from&to&sourceId`, `POST /entries`, `PUT/DELETE /entries/{id}`.
- `GET /sources/{id}/platforms`.
- FluentValidation on inputs; errors as ProblemDetails via the existing handler.

## Ledger (Agender.Shared/Reports, pure)
- `IncomeLedger.Build(sources, entries, trades)` → list of `LedgerItem(Date, SourceKey, SourceName, Color, Amount, Platform, Label)`; trades contribute closed trades on their exit date with `NetPnl`.
- Helpers: totals (gains, losses, net), by day, by week (Monday start), by month, by source, by platform; best/worst weeks.
- Trade-specific stats keep using `TraderStats` (win rate, invested) plus profit factor and best/worst trade.

## UI
- Nav: "Trades" → "Trabalho" (`/trabalho`). `/trades` redirects to `/trabalho/trades`.
- `WorkTabs`: Resumo, Trades, one tab per active manual source, "+ Nova fonte".
- Resumo (`/trabalho`): KPIs (month net, gains, losses, vs previous month, weekly average), 12-month stacked bar chart by source with net line, donut by source with list, recent activity, "Exportar PDF".
- Trades (`/trabalho/trades`): existing calendar and tables with KPI cards matching the area, profit factor, best/worst trade, collapsed trade details (expand on click).
- Manual source (`/trabalho/fonte/{id}`): month navigation, ⋯ menu (edit, archive, delete, export PDF), KPIs (month, this week, 8-week average, entry count, top platform), quick-add bar (date, amount, platform with suggestions, note; Enter saves and refocuses amount; remembers last platform), weekly calendar with day totals and heat tint, day detail with inline edit/delete (toast with undo on delete), tables by week and by platform.
- Source editor modal: name, emoji, color.

## PDF (`IncomePdf`, QuestPDF, same look as `ReportPdf`)
- Dialog: sources (all or selected), period presets (this month, last month, this year, last 12 months, custom), include detail list.
- Content: header, KPIs (net, gains, losses, weekly average, records, best week), per-source summary table (gains, losses, net, share, count), monthly evolution table, best/worst 3 weeks, per-source sections (manual: by platform; trades: win rate, profit factor, best/worst trade, fees, by symbol), optional chronological detail, footer with page numbers.
- Download via the existing Reports download path (browser / `INativeFileSaver`).

## Testing
- `IncomeLedger` (week boundaries across months, merge with trades, losses, best/worst weeks).
- `IncomeService` against SQLite in-memory (CRUD, validation, user isolation, platform suggestions, cascade).
- API tests for income endpoints.
- `IncomePdf` builds a non-empty PDF for all sources and for one.
- Migration test: trader entries/projects/agenda removed, unrelated data untouched.
- Trader ingest tests updated: no time entries are created.
