using MediaLibraryNormalizer.AI;
using MediaLibraryNormalizer.Audit;
using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Hashing;
using MediaLibraryNormalizer.Matching;
using MediaLibraryNormalizer.Merging;
using MediaLibraryNormalizer.Normalization;
using MediaLibraryNormalizer.Parser;
using MediaLibraryNormalizer.Reporting;
using MediaLibraryNormalizer.Runner;
using MediaLibraryNormalizer.Scanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer;

/// <summary>
/// Registers all services in the DI container.
/// </summary>
public static class ServiceRegistration
{
    public static ServiceProvider BuildServiceProvider(NormalizerConfig config)
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(config);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(config.Verbose ? LogLevel.Debug : LogLevel.Information);
        });

        // Scanner
        services.AddSingleton<IMediaFileDetector, MediaFileDetector>();
        services.AddSingleton<ILibraryScanner, LibraryScanner>();

        // Normalization
        services.AddSingleton<INameNormalizer, NameNormalizer>();

        // Parser
        services.AddSingleton<IEpisodeParser, EpisodeParser>();

        // Matching
        services.AddSingleton<ISeriesMatcher, SeriesMatcher>();
        services.AddSingleton<IDuplicateDetector, DuplicateDetector>();

        // AI
        if (config.UseAi)
        {
            services.AddHttpClient<IAiResolver, OpenAiResolver>();
        }
        else
        {
            services.AddSingleton<IAiResolver, NoOpAiResolver>();
        }

        if (config.UseAiOrganizer)
        {
            services.AddHttpClient<IAiOrganizer, OpenAiOrganizer>();
        }
        else
        {
            services.AddSingleton<IAiOrganizer, NoOpAiOrganizer>();
        }

        // Hashing
        services.AddSingleton<IMediaHasher, MediaHasher>();

        // Merging
        services.AddSingleton<ITransactionLog, TransactionLog>();
        services.AddSingleton<IFileMover, FileMover>();
        services.AddSingleton<ISeriesMerger, SeriesMerger>();
        services.AddSingleton<IEmptyFolderCleaner, EmptyFolderCleaner>();
        services.AddSingleton<UndoService>();

        // Reporting
        services.AddSingleton<IReportGenerator, ReportGenerator>();

        // Orchestration
        services.AddSingleton<INormalizerRunner, NormalizerRunner>();
        services.AddSingleton<ISeriesAuditRunner, SeriesAuditRunner>();

        return services.BuildServiceProvider();
    }
}
