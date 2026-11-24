using System.Globalization;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Agender.Shared.Reports;

public sealed record IncomeReportInput(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<LedgerSource> Sources,
    IReadOnlyList<LedgerItem> Items,
    IReadOnlyList<TraderTradeDto> Trades,
    bool IncludeDetail,
    DateTime GeneratedAt,
    IReadOnlyList<IncomeEntryDto>? PendingEntries = null,
    IReadOnlyList<IncomeEntryDto>? UnconvertedEntries = null);

public static class IncomePdf
{
    private const string Brand = "#0F766E";
    private const string BrandDark = "#134E4A";
    private const string BrandSoft = "#F0FDFA";
    private const string BrandBorder = "#99F6E4";
    private const string Ink = "#1F2937";
    private const string Muted = "#6B7280";
    private const string Line = "#E5E7EB";
    private const string Zebra = "#F9FAFB";
    private const string Gain = "#047857";
    private const string Loss = "#B91C1C";
    private const int TopWeeks = 3;
    private const int TopSymbols = 10;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    static IncomePdf() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Build(IncomeReportInput input)
    {
        Dictionary<string, LedgerSource> sources = input.Sources.ToDictionary(s => s.Key);
        List<LedgerItem> items = [.. IncomeLedger.Between(input.Items, input.From, input.To).Where(i => sources.ContainsKey(i.SourceKey)).OrderBy(i => i.Date)];

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(style => style.FontSize(9.5f).FontColor(Ink));

            page.Header().Column(header =>
            {
                header.Item().ShowOnce().Element(item => ComposeHeader(item, input));
                header.Item().SkipOnce().Height(18);
            });
            page.Content().PaddingHorizontal(36).PaddingVertical(18).Column(column =>
            {
                column.Spacing(20);
                column.Item().Element(item => ComposeKpis(item, items, input));
                column.Item().Element(item => ComposeSources(item, items, input.Sources));
                column.Item().Element(item => ComposeMonths(item, items, input));
                column.Item().Element(item => ComposeWeeks(item, items));

                foreach (LedgerSource source in input.Sources)
                {
                    List<LedgerItem> own = [.. items.Where(i => i.SourceKey == source.Key)];

                    if (source.IsTrades)
                    {
                        List<TraderTradeDto> trades = [.. input.Trades.Where(t => t.ExitDate is { } d && d >= input.From && d <= input.To)];
                        column.Item().Element(item => ComposeTrades(item, source, own, trades));
                    }
                    else
                    {
                        PendingTotals sourcePending = IncomeLedger.Pending((input.PendingEntries ?? []).Where(e => IncomeLedger.SourceKey(e.SourceId) == source.Key));
                        PendingTotals sourceUnconverted = IncomeLedger.Unconverted(input.UnconvertedEntries ?? [], [source]).Select(x => x.Totals).FirstOrDefault() ?? PendingTotals.Empty;
                        column.Item().Element(item => ComposeManual(item, source, own, sourcePending, sourceUnconverted));
                    }
                }

                if (input.IncludeDetail && items.Count > 0)
                {
                    column.Item().Element(item => ComposeDetail(item, items, sources));
                }
            });
            page.Footer().Element(footer => ComposeFooter(footer, input.GeneratedAt));
        })).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, IncomeReportInput input) =>
        container.Background(Brand).PaddingHorizontal(36).PaddingVertical(22).Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("AGENDER · TRABALHO").FontSize(9).Bold().LetterSpacing(0.25f).FontColor("#99F6E4");
            column.Item().Text("Relatório de rendimentos").FontSize(24).Bold().FontColor(Colors.White);
            column.Item().Text(PeriodText(input.From, input.To)).FontSize(11).FontColor("#CCFBF1");
            column.Item().PaddingTop(4).Text($"Fontes: {string.Join(", ", input.Sources.Select(s => s.Currency == IncomeCurrency.Eur ? s.Name : $"{s.Name} ({IncomeCurrencies.Symbol(s.Currency)})"))}").FontSize(9).FontColor("#99F6E4");

            if (input.Sources.Any(s => s.Currency != IncomeCurrency.Eur))
            {
                column.Item().Text("Valores em euros. Nas fontes noutra moeda conta o valor recebido depois da conversão.").FontSize(8).FontColor("#CCFBF1");
            }
        });

    private static void ComposeKpis(IContainer container, List<LedgerItem> items, IncomeReportInput input)
    {
        LedgerTotals totals = IncomeLedger.Totals(items);
        (DateOnly Start, LedgerTotals Totals)? best = IncomeLedger.ByWeek(items).OrderByDescending(w => w.Totals.Net).Select(w => ((DateOnly, LedgerTotals)?)w).FirstOrDefault();

        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Row(row =>
            {
                row.Spacing(10);
                List<string> notes = [];
                IReadOnlyList<(IncomeCurrency Currency, PendingTotals Totals)> pending = IncomeLedger.PendingByCurrency(input.PendingEntries ?? [], input.Sources);
                IReadOnlyList<(IncomeCurrency Currency, PendingTotals Totals)> unconverted = IncomeLedger.Unconverted(input.UnconvertedEntries ?? [], input.Sources);

                if (pending.Count > 0)
                {
                    notes.Add($"+ {string.Join(" · ", pending.Select(p => Money(p.Totals.Amount, false, p.Currency)))} pendente de aprovação");
                }

                if (unconverted.Count > 0)
                {
                    notes.Add($"+ {string.Join(" · ", unconverted.Select(u => Money(u.Totals.Amount, false, u.Currency)))} por converter");
                }

                Kpi(row.RelativeItem(), "Resultado líquido", Money(totals.Net), Tone(totals.Net),
                    notes.Count > 0 ? string.Join("\n", notes) : null, highlight: true);
                Kpi(row.RelativeItem(), "Ganhos", Money(totals.Gains, signed: false), Gain);
                Kpi(row.RelativeItem(), "Perdas", Money(totals.Losses, signed: false), totals.Losses > 0 ? Loss : BrandDark);
            });
            column.Item().Row(row =>
            {
                row.Spacing(10);
                Kpi(row.RelativeItem(), "Média por semana", Money(IncomeLedger.WeeklyAverage(items, input.From, input.To)), BrandDark);
                Kpi(row.RelativeItem(), "Registos e trades", totals.Count.ToString(Culture), BrandDark);
                Kpi(row.RelativeItem(), "Melhor semana", best is { } week ? $"{Money(week.Totals.Net)}" : "—", BrandDark,
                    best is { } bestWeek ? WeekText(bestWeek.Start) : null);
            });
        });
    }

    private static void Kpi(IContainer container, string label, string value, string color, string? caption = null, bool highlight = false) =>
        container.Background(highlight ? BrandSoft : Colors.White).Border(1).BorderColor(highlight ? BrandBorder : Line).CornerRadius(6).Padding(10).Column(column =>
        {
            column.Item().Text(label.ToUpperInvariant()).FontSize(7).Bold().LetterSpacing(0.1f).FontColor(Muted);
            column.Item().PaddingTop(2).Text(value).FontSize(17).Bold().FontColor(color);

            if (caption is not null)
            {
                column.Item().Text(caption).FontSize(8).FontColor(Muted);
            }
        });

    private static void ComposeSources(IContainer container, List<LedgerItem> items, IReadOnlyList<LedgerSource> sources)
    {
        Dictionary<string, LedgerTotals> bySource = IncomeLedger.BySource(items).ToDictionary(s => s.Key, s => s.Totals);
        decimal totalGains = IncomeLedger.Totals(items).Gains;

        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, "Resumo por fonte"));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(44);
                    columns.ConstantColumn(72);
                    columns.ConstantColumn(72);
                    columns.ConstantColumn(78);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Fonte");
                    HeaderCell(header.Cell(), "Peso nos ganhos");
                    HeaderCell(header.Cell(), "Reg.", alignRight: true);
                    HeaderCell(header.Cell(), "Ganhos", alignRight: true);
                    HeaderCell(header.Cell(), "Perdas", alignRight: true);
                    HeaderCell(header.Cell(), "Resultado", alignRight: true);
                });

                int index = 0;

                foreach (LedgerSource source in sources.OrderByDescending(s => bySource.GetValueOrDefault(s.Key, LedgerTotals.Empty).Net))
                {
                    LedgerTotals totals = bySource.GetValueOrDefault(source.Key, LedgerTotals.Empty);
                    double share = totalGains == 0 ? 0 : (double)(totals.Gains / totalGains * 100);
                    string background = index++ % 2 == 0 ? Colors.White : Zebra;

                    table.Cell().Element(cell => Cell(cell, background)).Row(row =>
                    {
                        row.ConstantItem(8).AlignMiddle().Height(8).Background(source.Color).CornerRadius(4);
                        row.RelativeItem().PaddingLeft(6).Text(source.Name).SemiBold();
                    });
                    table.Cell().Element(cell => Cell(cell, background)).AlignMiddle().Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Element(bar => ShareBar(bar, share, source.Color));
                        row.ConstantItem(30).AlignRight().Text($"{share.ToString("0", Culture)}%").FontSize(8).FontColor(Muted);
                    });
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(totals.Count.ToString(Culture)).FontColor(Muted);
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(Money(totals.Gains, signed: false)).FontColor(Gain);
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(totals.Losses == 0 ? "—" : Money(-totals.Losses)).FontColor(totals.Losses == 0 ? Muted : Loss);
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(Money(totals.Net)).Bold().FontColor(Tone(totals.Net));
                }

                LedgerTotals all = IncomeLedger.Totals(items);
                TotalCell(table.Cell().ColumnSpan(2), "Total");
                TotalCell(table.Cell(), all.Count.ToString(Culture), alignRight: true);
                TotalCell(table.Cell(), Money(all.Gains, signed: false), alignRight: true);
                TotalCell(table.Cell(), all.Losses == 0 ? "—" : Money(-all.Losses), alignRight: true);
                TotalCell(table.Cell(), Money(all.Net), alignRight: true);
            });
        });
    }

    private static void ComposeMonths(IContainer container, List<LedgerItem> items, IncomeReportInput input)
    {
        IReadOnlyList<DateOnly> months = IncomeLedger.Months(input.From, input.To);
        IReadOnlyList<LedgerSource> sources = input.Sources;
        decimal maxAbs = months.Select(m => Math.Abs(IncomeLedger.Totals(IncomeLedger.Between(items, m, IncomeLedger.MonthEnd(m))).Net)).DefaultIfEmpty(0).Max();

        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, "Evolução mensal"));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(78);

                    foreach (LedgerSource _ in sources)
                    {
                        columns.RelativeColumn(1.4f);
                    }

                    columns.RelativeColumn(2);
                    columns.ConstantColumn(76);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Mês");

                    foreach (LedgerSource source in sources)
                    {
                        HeaderCell(header.Cell(), source.Name, alignRight: true);
                    }

                    HeaderCell(header.Cell(), string.Empty);
                    HeaderCell(header.Cell(), "Total", alignRight: true);
                });

                for (int index = 0; index < months.Count; index++)
                {
                    DateOnly month = months[index];
                    List<LedgerItem> own = [.. IncomeLedger.Between(items, month, IncomeLedger.MonthEnd(month))];
                    decimal net = IncomeLedger.Totals(own).Net;
                    string background = index % 2 == 0 ? Colors.White : Zebra;

                    table.Cell().Element(cell => Cell(cell, background)).Text(Capitalize(month.ToString("MMM yyyy", Culture))).SemiBold();

                    foreach (LedgerSource source in sources)
                    {
                        decimal value = IncomeLedger.Totals(own.Where(i => i.SourceKey == source.Key)).Net;
                        table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(value == 0 ? "—" : Money(value)).FontColor(value == 0 ? Muted : Tone(value));
                    }

                    double share = maxAbs == 0 ? 0 : (double)(Math.Abs(net) / maxAbs * 100);
                    table.Cell().Element(cell => Cell(cell, background)).AlignMiddle().Element(bar => ShareBar(bar, share, net < 0 ? Loss : Brand));
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(Money(net)).Bold().FontColor(Tone(net));
                }
            });
        });
    }

    private static void ComposeWeeks(IContainer container, List<LedgerItem> items)
    {
        IReadOnlyList<(DateOnly Start, LedgerTotals Totals)> weeks = IncomeLedger.ByWeek(items);

        if (weeks.Count == 0)
        {
            return;
        }

        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, "Melhores e piores semanas"));
            column.Item().Row(row =>
            {
                row.Spacing(14);
                row.RelativeItem().Element(item => WeekList(item, "Melhores", weeks.OrderByDescending(w => w.Totals.Net).Take(TopWeeks)));
                row.RelativeItem().Element(item => WeekList(item, "Mais fracas", weeks.OrderBy(w => w.Totals.Net).Take(TopWeeks)));
            });
        });
    }

    private static void WeekList(IContainer container, string title, IEnumerable<(DateOnly Start, LedgerTotals Totals)> weeks) =>
        container.Border(1).BorderColor(Line).CornerRadius(6).Padding(10).Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(title.ToUpperInvariant()).FontSize(7).Bold().LetterSpacing(0.1f).FontColor(Muted);

            foreach ((DateOnly start, LedgerTotals totals) in weeks)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(WeekText(start));
                    row.ConstantItem(40).AlignRight().Text($"{totals.Count} reg.").FontSize(8).FontColor(Muted);
                    row.ConstantItem(76).AlignRight().Text(Money(totals.Net)).Bold().FontColor(Tone(totals.Net));
                });
            }
        });

    private static void ComposeManual(IContainer container, LedgerSource source, List<LedgerItem> items, PendingTotals pending, PendingTotals unconverted)
    {
        LedgerTotals totals = IncomeLedger.Totals(items);
        IReadOnlyList<(string Platform, LedgerTotals Totals)> platforms = IncomeLedger.ByPlatform(items);

        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, source.Name, source.Color));

            string pendingText = (pending.Count > 0 ? $" · {Money(pending.Amount, false, source.Currency)} pendente de aprovação ({pending.Count})" : string.Empty)
                + (unconverted.Count > 0 ? $" · {Money(unconverted.Amount, false, source.Currency)} por converter ({unconverted.Count})" : string.Empty);

            if (source.Currency != IncomeCurrency.Eur && items.Count > 0)
            {
                decimal original = items.Sum(i => i.OriginalAmount ?? 0);
                column.Item().Text($"Recebe em {IncomeCurrencies.Name(source.Currency)}: {Money(original, false, source.Currency)} convertidos em {Money(IncomeLedger.Totals(items).Net, false)}.").FontColor(Muted);
            }

            if (items.Count == 0)
            {
                column.Item().Text($"Sem ganhos confirmados neste período{pendingText}.").FontColor(Muted);
                return;
            }

            column.Item().PaddingBottom(8).Text($"{totals.Count} registos · resultado {Money(totals.Net)} · média por registo {Money(Math.Round(totals.Net / totals.Count, 2))}{pendingText}").FontColor(Muted);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(54);
                    columns.ConstantColumn(80);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Plataforma");
                    HeaderCell(header.Cell(), "Peso");
                    HeaderCell(header.Cell(), "Registos", alignRight: true);
                    HeaderCell(header.Cell(), "Resultado", alignRight: true);
                });

                int index = 0;

                foreach ((string platform, LedgerTotals platformTotals) in platforms)
                {
                    string background = index++ % 2 == 0 ? Colors.White : Zebra;
                    double share = totals.Gains == 0 ? 0 : (double)(platformTotals.Gains / totals.Gains * 100);

                    table.Cell().Element(cell => Cell(cell, background)).Text(platform).SemiBold();
                    table.Cell().Element(cell => Cell(cell, background)).AlignMiddle().Element(bar => ShareBar(bar, share, source.Color));
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(platformTotals.Count.ToString(Culture)).FontColor(Muted);
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(Money(platformTotals.Net)).Bold().FontColor(Tone(platformTotals.Net));
                }
            });
        });
    }

    private static void ComposeTrades(IContainer container, LedgerSource source, List<LedgerItem> items, List<TraderTradeDto> trades)
    {
        TraderPeriodStats stats = TraderStats.Summarize(trades);
        double? profitFactor = TraderStats.ProfitFactor(trades);
        TraderTradeDto? best = TraderStats.Best(trades);
        TraderTradeDto? worst = TraderStats.Worst(trades);
        double fees = trades.Where(t => t.IsClosed).Sum(t => t.Fees);

        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, source.Name, source.Color));

            if (stats.Closed == 0)
            {
                column.Item().Text("Sem trades fechadas neste período.").FontColor(Muted);
                return;
            }

            column.Item().PaddingBottom(10).Row(row =>
            {
                row.Spacing(10);
                Kpi(row.RelativeItem(), "Trades fechadas", stats.Closed.ToString(Culture), BrandDark, $"{stats.Wins} ganhos · {stats.Losses} perdas");
                Kpi(row.RelativeItem(), "Taxa de acerto", stats.WinRate.ToString("0%", Culture), BrandDark);
                Kpi(row.RelativeItem(), "Fator de lucro", profitFactor is { } factor ? factor.ToString("0.00", Culture) : "—", BrandDark);
                Kpi(row.RelativeItem(), "Investido", Money((decimal)stats.Invested, signed: false), BrandDark, $"Comissões {Money((decimal)fees, signed: false)}");
            });

            column.Item().PaddingBottom(10).Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(item => TradeHighlight(item, "Melhor trade", best));
                row.RelativeItem().Element(item => TradeHighlight(item, "Pior trade", worst));
            });

            IReadOnlyList<(string Platform, LedgerTotals Totals)> symbols = IncomeLedger.ByPlatform(items);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(54);
                    columns.ConstantColumn(54);
                    columns.ConstantColumn(80);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Símbolo");
                    HeaderCell(header.Cell(), "Trades", alignRight: true);
                    HeaderCell(header.Cell(), "Acerto", alignRight: true);
                    HeaderCell(header.Cell(), "Resultado", alignRight: true);
                });

                int index = 0;

                foreach ((string symbol, LedgerTotals totals) in symbols.Take(TopSymbols))
                {
                    string background = index++ % 2 == 0 ? Colors.White : Zebra;
                    TraderPeriodStats symbolStats = TraderStats.Summarize(trades.Where(t => string.Equals(t.Symbol, symbol, StringComparison.CurrentCultureIgnoreCase)));

                    table.Cell().Element(cell => Cell(cell, background)).Text(symbol).SemiBold();
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(totals.Count.ToString(Culture)).FontColor(Muted);
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(symbolStats.WinRate.ToString("0%", Culture)).FontColor(Muted);
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(Money(totals.Net)).Bold().FontColor(Tone(totals.Net));
                }
            });
        });
    }

    private static void TradeHighlight(IContainer container, string title, TraderTradeDto? trade) =>
        container.Border(1).BorderColor(Line).CornerRadius(6).Padding(10).Column(column =>
        {
            column.Item().Text(title.ToUpperInvariant()).FontSize(7).Bold().LetterSpacing(0.1f).FontColor(Muted);

            if (trade is null)
            {
                column.Item().PaddingTop(2).Text("—").FontColor(Muted);
                return;
            }

            decimal pnl = (decimal)(trade.NetPnl ?? 0);
            column.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text($"{trade.Symbol} · {(trade.IsBuy ? "Compra" : "Venda")}").Bold();
                row.ConstantItem(90).AlignRight().Text(Money(pnl)).Bold().FontColor(Tone(pnl));
            });
            column.Item().Text($"{trade.Name} · fechada a {trade.ExitDate?.ToString("dd/MM/yyyy", Culture)}").FontSize(8).FontColor(Muted);
        });

    private static void ComposeDetail(IContainer container, List<LedgerItem> items, Dictionary<string, LedgerSource> sources) =>
        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, "Detalhe"));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(72);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(72);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Data");
                    HeaderCell(header.Cell(), "Fonte");
                    HeaderCell(header.Cell(), "Plataforma / trade");
                    HeaderCell(header.Cell(), "Nota");
                    HeaderCell(header.Cell(), "Valor", alignRight: true);
                });

                for (int index = 0; index < items.Count; index++)
                {
                    LedgerItem item = items[index];
                    LedgerSource source = sources[item.SourceKey];
                    string background = index % 2 == 0 ? Colors.White : Zebra;

                    table.Cell().Element(cell => Cell(cell, background)).Text(item.Date.ToString("dd/MM/yyyy", Culture));
                    table.Cell().Element(cell => Cell(cell, background)).Row(row =>
                    {
                        row.ConstantItem(6).AlignMiddle().Height(6).Background(source.Color).CornerRadius(3);
                        row.RelativeItem().PaddingLeft(5).Text(source.Name);
                    });
                    table.Cell().Element(cell => Cell(cell, background)).Text(string.IsNullOrWhiteSpace(item.Label) ? "—" : item.Label).FontColor(string.IsNullOrWhiteSpace(item.Label) ? Muted : Ink);
                    string detail = string.Join(" · ", new[] { item.OriginalAmount is { } original ? $"{Money(original, false, item.OriginalCurrency)} convertidos" : null, item.Detail }.Where(d => !string.IsNullOrWhiteSpace(d)));
                    table.Cell().Element(cell => Cell(cell, background)).Text(detail.Length == 0 ? "—" : detail).FontSize(8.5f).FontColor(Muted);
                    table.Cell().Element(cell => Cell(cell, background)).AlignRight().Text(Money(item.Amount)).Bold().FontColor(Tone(item.Amount));
                }
            });
        });

    private static IContainer Cell(IContainer container, string background) =>
        container.Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(6);

    private static void TotalCell(IContainer container, string text, bool alignRight = false)
    {
        IContainer cell = container.Background(BrandSoft).BorderTop(1.5f).BorderColor(Brand).PaddingVertical(6).PaddingHorizontal(6);
        cell = alignRight ? cell.AlignRight() : cell;
        cell.Text(text).Bold().FontColor(BrandDark);
    }

    private static void ShareBar(IContainer container, double share, string color)
    {
        float filled = (float)Math.Clamp(share, 0, 100);

        container.Height(6).Background(Line).CornerRadius(3).Row(row =>
        {
            if (filled > 0)
            {
                row.RelativeItem(filled).Background(color).CornerRadius(3);
            }

            if (filled < 100)
            {
                row.RelativeItem(100 - filled);
            }
        });
    }

    private static void SectionTitle(IContainer container, string title, string color = Brand) =>
        container.PaddingBottom(6).Row(row =>
        {
            row.ConstantItem(4).Height(14).Background(color);
            row.RelativeItem().PaddingLeft(8).Text(title).FontSize(13).Bold().FontColor(BrandDark);
        });

    private static void HeaderCell(IContainer container, string text, bool alignRight = false)
    {
        IContainer cell = container.Background(BrandDark).PaddingVertical(6).PaddingHorizontal(6);
        cell = alignRight ? cell.AlignRight() : cell;
        cell.Text(text.ToUpperInvariant()).FontSize(7.5f).Bold().LetterSpacing(0.08f).FontColor(Colors.White);
    }

    private static void ComposeFooter(IContainer container, DateTime generatedAt) =>
        container.PaddingHorizontal(36).PaddingVertical(14).BorderTop(0.5f).BorderColor(Line).Row(row =>
        {
            row.RelativeItem().Text($"Gerado em {generatedAt.ToString("dd/MM/yyyy 'às' HH:mm", Culture)} · Agender").FontSize(8).FontColor(Muted);
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                text.Span("Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });

    public static string Money(decimal value, bool signed = true, IncomeCurrency currency = IncomeCurrency.Eur) =>
        (signed ? value.ToString("+#,##0.00;-#,##0.00;0.00", Culture) : value.ToString("#,##0.00", Culture)) + " " + IncomeCurrencies.Symbol(currency);

    private static string Tone(decimal value) => value > 0 ? Gain : value < 0 ? Loss : Muted;

    private static string WeekText(DateOnly start) =>
        $"{start.ToString("d MMM", Culture)} – {start.AddDays(6).ToString("d MMM yyyy", Culture)}";

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Culture) + text[1..];

    private static string PeriodText(DateOnly from, DateOnly to) =>
        from == to
            ? from.ToString("d 'de' MMMM 'de' yyyy", Culture)
            : $"{from.ToString("d MMM yyyy", Culture)}  →  {to.ToString("d MMM yyyy", Culture)}";
}
