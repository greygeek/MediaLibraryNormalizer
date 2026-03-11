using System.CommandLine;
using MediaLibraryNormalizer;
using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Reporting;
using MediaLibraryNormalizer.Runner;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var pathArg = new Argument<string>("path") { Description = "Path to the media library root directory" };

var dryRunOption = new Option<bool>("--dry-run") { Description = "Preview mode — no filesystem changes (default)", DefaultValueFactory = _ => true };
var mergeOption = new Option<bool>("--merge") { Description = "Apply merge and cleanup operations" };
var exactWithFilesOnlyOption = new Option<bool>("--exact-with-files-only") { Description = "Limit merge to exact-match groups with real files in duplicate folders; skips fuzzy groups and global empty-folder cleanup" };
var discardInferiorDuplicatesOption = new Option<bool>("--discard-inferior-duplicates") { Description = "Delete inferior duplicate episode files when duplicate comparison chooses the better version" };
var aiOption = new Option<bool>("--ai") { Description = "Enable AI-assisted title resolution" };
var hashOption = new Option<bool>("--hash") { Description = "Enable file hashing for duplicate detection" };
var verboseOption = new Option<bool>("--verbose") { Description = "Enable verbose output" };
var deleteSamplesOption = new Option<bool>("--delete-samples") { Description = "Delete sample files" };
var undoOption = new Option<string?>("--undo") { Description = "Undo operations from a transaction log file" };

var rootCommand = new RootCommand("Media Library Normalizer — TV series library cleanup tool");
rootCommand.Arguments.Add(pathArg);
rootCommand.Options.Add(dryRunOption);
rootCommand.Options.Add(mergeOption);
rootCommand.Options.Add(exactWithFilesOnlyOption);
rootCommand.Options.Add(discardInferiorDuplicatesOption);
rootCommand.Options.Add(aiOption);
rootCommand.Options.Add(hashOption);
rootCommand.Options.Add(verboseOption);
rootCommand.Options.Add(deleteSamplesOption);
rootCommand.Options.Add(undoOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(pathArg)!;
    var dryRun = parseResult.GetValue(dryRunOption);
    var merge = parseResult.GetValue(mergeOption);
    var exactWithFilesOnly = parseResult.GetValue(exactWithFilesOnlyOption);
    var discardInferiorDuplicates = parseResult.GetValue(discardInferiorDuplicatesOption);
    var ai = parseResult.GetValue(aiOption);
    var hash = parseResult.GetValue(hashOption);
    var verbose = parseResult.GetValue(verboseOption);
    var deleteSamples = parseResult.GetValue(deleteSamplesOption);
    var undoFile = parseResult.GetValue(undoOption);

    var config = ConfigLoader.Load();
    config.LibraryPath = path;
    config.Verbose = verbose;
    config.ExactMatchesWithFilesOnly = exactWithFilesOnly;
    config.DiscardInferiorDuplicates = discardInferiorDuplicates;
    config.UseAi = ai;
    config.UseHash = hash;
    config.DeleteSamples = deleteSamples;
    config.UndoFile = undoFile;

    if (merge)
    {
        config.DryRun = false;
        config.Merge = true;
    }
    else
    {
        config.DryRun = dryRun;
    }

    var progress = new Progress<string>(_ => { });

    try
    {
        var runner = new NormalizerRunner();

        if (undoFile is not null)
        {
            await runner.UndoAsync(config, progress, cancellationToken);
            return;
        }

        var result = await runner.RunAsync(config, null, progress, cancellationToken);

        using var reportServices = ServiceRegistration.BuildServiceProvider(config);
        var reporter = reportServices.GetRequiredService<IReportGenerator>();
        reporter.WriteConsoleSummary(result.ScanResult, result.Operations, result.Config.DryRun);
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteException(ex);
        Environment.ExitCode = 1;
    }
});

return rootCommand.Parse(args).Invoke();
