using System.Text.Json;
using MediaLibraryNormalizer.Models;
using Spectre.Console;

namespace MediaLibraryNormalizer.Reporting;

/// <summary>
/// Generates console summaries and JSON reports.
/// </summary>
public class ReportGenerator : IReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void WriteConsoleSummary(ScanResult result, List<MergeOperation> operations, bool dryRun)
    {
        AnsiConsole.WriteLine();
        var mode = dryRun ? "[yellow]DRY RUN[/]" : "[green]LIVE[/]";
        AnsiConsole.MarkupLine($"[bold]Media Library Normalizer — {mode} Report[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Total folders scanned", result.TotalFolders.ToString());
        table.AddRow("Total video files", result.TotalFiles.ToString());
        table.AddRow("Duplicate series groups", result.DuplicateGroups.Count.ToString());
        table.AddRow("UNPACK folders", result.UnpackFolders.Count.ToString());
        table.AddRow("Empty folders", result.EmptyFolders.Count.ToString());
        table.AddRow("Operations planned/executed", operations.Count.ToString());
        table.AddRow("Uncertain matches", result.UncertainMatches.Count.ToString());
        table.AddRow("Errors", result.Errors.Count.ToString());

        AnsiConsole.Write(table);

        // Duplicate groups detail
        if (result.DuplicateGroups.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold underline]Duplicate Series Groups[/]");

            foreach (var group in result.DuplicateGroups)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"  [bold]{Markup.Escape(group.CanonicalName)}[/] " +
                    $"(matched by {group.MatchMethod}" +
                    (group.FuzzyScore.HasValue ? $", score={group.FuzzyScore}" : "") + ")");

                AnsiConsole.MarkupLine($"    [green]Canonical:[/] {Markup.Escape(group.CanonicalFolder?.OriginalName ?? "?")}");

                foreach (var dupe in group.DuplicateFolders)
                {
                    AnsiConsole.MarkupLine($"    [red]Duplicate:[/] {Markup.Escape(dupe.OriginalName)} ({dupe.FileCount} files)");
                }
            }
        }

        // Uncertain matches
        if (result.UncertainMatches.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold underline yellow]Uncertain Matches — Manual Review Required[/]");

            foreach (var um in result.UncertainMatches)
            {
                AnsiConsole.MarkupLine($"  '{Markup.Escape(um.TitleA)}' ↔ '{Markup.Escape(um.TitleB)}' " +
                    $"(fuzzy={um.FuzzyScore}, AI={um.AiResult?.ToString() ?? "N/A"})");
            }
        }

        // Errors
        if (result.Errors.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold underline red]Errors[/]");
            foreach (var err in result.Errors)
            {
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape(err)}[/]");
            }
        }

        AnsiConsole.WriteLine();
    }

    public async Task WriteJsonReportAsync(
        string filePath, ScanResult result, List<MergeOperation> operations, bool dryRun)
    {
        var report = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            dryRun,
            summary = new
            {
                totalFolders = result.TotalFolders,
                totalFiles = result.TotalFiles,
                duplicateGroups = result.DuplicateGroups.Count,
                unpackFolders = result.UnpackFolders.Count,
                emptyFolders = result.EmptyFolders.Count,
                operationsCount = operations.Count,
                uncertainMatches = result.UncertainMatches.Count,
                errors = result.Errors.Count
            },
            duplicateGroups = result.DuplicateGroups.Select(g => new
            {
                seriesKey = g.SeriesKey,
                canonicalName = g.CanonicalName,
                canonicalFolder = g.CanonicalFolder?.OriginalName,
                matchMethod = g.MatchMethod.ToString(),
                fuzzyScore = g.FuzzyScore,
                folders = g.AllFolders.Select(f => new
                {
                    name = f.OriginalName,
                    fileCount = f.FileCount,
                    isCanonical = f == g.CanonicalFolder
                })
            }),
            operations = operations.Select(o => new
            {
                type = o.Type.ToString(),
                source = o.Source,
                destination = o.Destination,
                dryRun = o.DryRun
            }),
            uncertainMatches = result.UncertainMatches.Select(um => new
            {
                titleA = um.TitleA,
                titleB = um.TitleB,
                fuzzyScore = um.FuzzyScore,
                fileCountA = um.FileCountA,
                fileCountB = um.FileCountB,
                aiResult = um.AiResult?.ToString()
            }),
            errors = result.Errors
        };

        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
}
