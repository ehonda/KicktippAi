using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.Dev;
using Orchestrator.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

public sealed class CollectContextProfileCommand : AsyncCommand<CollectContextProfileSettings>
{
    private readonly IAnsiConsole _console;
    private readonly ICompetitionCollectionProfileResolver _profileResolver;
    private readonly ICompetitionProfileCollectorExecutor _collectorExecutor;
    private readonly ICommunityKicktippCredentialLoader _credentialLoader;
    private readonly IServiceProvider? _services;

    public CollectContextProfileCommand(
        IAnsiConsole console,
        ICompetitionCollectionProfileResolver profileResolver,
        ICompetitionProfileCollectorExecutor collectorExecutor,
        ICommunityKicktippCredentialLoader credentialLoader,
        IServiceProvider? services = null)
    {
        _console = console;
        _profileResolver = profileResolver;
        _collectorExecutor = collectorExecutor;
        _credentialLoader = credentialLoader;
        _services = services;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CollectContextProfileSettings settings,
        CancellationToken cancellationToken)
    {
        CompetitionCollectionProfile profile;
        string communityContext;
        CompetitionContextSourceCycleInvocation? sourceCycleInvocation;
        try
        {
            communityContext = settings.CommunityContext.Trim();
            var targetCompetition = CompetitionResolver.ResolveTargetCompetition(
                settings.Competition,
                communityContext);
            profile = _profileResolver.ResolveCompetition(targetCompetition);
            profile = profile with
            {
                ContextSourceFeatures = new CompetitionContextSourceFeatures(
                    settings.EnableClubEloSource,
                    settings.EnableRosterSource)
            };
            if (settings.ContextSourceOnly)
                CompetitionProfileCollectionRunner.ValidateSourceOnly(profile, settings.Matchdays, settings.FullSeason, settings.KicktippCredentialProfile);
            sourceCycleInvocation = ResolveContextSourceCycle(settings, profile, communityContext);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }

        try
        {
            if (settings.ContextSourceOnly) { }
            else if (string.IsNullOrWhiteSpace(settings.KicktippCredentialProfile))
            {
                _credentialLoader.Load(communityContext);
            }
            else
            {
                _credentialLoader.Load(communityContext, settings.KicktippCredentialProfile);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or IOException
                                           or UnauthorizedAccessException)
        {
            _console.MarkupLine(
                $"[red]Error:[/] Unable to load Kicktipp credentials for community context " +
                $"'[yellow]{Markup.Escape(communityContext)}[/]'. Check the matching sibling credential file " +
                "and injected environment variables.");
            return 1;
        }

        var request = new CompetitionProfileCollectionRequest(
            profile,
            communityContext,
            communityContext,
            settings.Matchdays,
            settings.FullSeason,
            settings.RecentHistoryDateMap,
            settings.DryRun,
            settings.Verbose,
            settings.MarkdownSummaryOutput,
            sourceCycleInvocation,
            settings.ContextSourceOnly);
        return await CompetitionProfileCollectionRunner.ExecuteAsync(
            _console,
            _collectorExecutor,
            request,
            cancellationToken,
            _services);
    }

    private static CompetitionContextSourceCycleInvocation? ResolveContextSourceCycle(
        CollectContextProfileSettings settings,
        CompetitionCollectionProfile profile,
        string communityContext)
    {
        // Preserve the zero-interaction legacy path: source-cycle arguments have no
        // effect until at least one independently gated source flag is enabled.
        if (!profile.ContextSourceFeatures.AnyEnabled) return null;

        var scope = settings.ContextSourceScope?.Trim();
        if (string.IsNullOrWhiteSpace(scope))
        {
            if (communityContext != BundesligaContextSourceContract.DevelopmentCommunity)
                throw new InvalidOperationException("Source-enabled non-development communities require explicit production-live cycle and lane authority.");
            scope = BundesligaContextSourceContract.DevelopmentScope;
        }
        if (scope == BundesligaContextSourceContract.DevelopmentScope)
        {
            BundesligaContextSourceContract.ValidateConsumerAuthority(BundesligaContextSourceScope.Development, BundesligaContextSourceContract.DevelopmentLane, communityContext);
            if (new[] { settings.ContextSourceCycleId, settings.ContextSourceCurrentLane, settings.ContextSourceProducerLane, settings.ContextSourceConsumers }
                .Any(value => !string.IsNullOrWhiteSpace(value)))
                throw new InvalidOperationException("Development source cycles allocate their UUIDv7 and fixed lane contract at profile entry.");
            return null;
        }
        if (scope != BundesligaContextSourceContract.ProductionScope)
            throw new InvalidOperationException("--context-source-scope must be 'development' or 'production-live'.");

        var cycleId = Require(settings.ContextSourceCycleId, "--context-source-cycle-id");
        var currentLane = Require(settings.ContextSourceCurrentLane, "--context-source-current-lane");
        var producerLane = Require(settings.ContextSourceProducerLane, "--context-source-producer-lane");
        var consumersValue = Require(settings.ContextSourceConsumers, "--context-source-consumers");
        var consumers = consumersValue.Split(',', StringSplitOptions.None);
        if (consumers.Any(value => value.Length == 0 || value != value.Trim()))
            throw new InvalidOperationException("--context-source-consumers must be a comma-separated canonical lane list without whitespace.");

        var separator = cycleId.LastIndexOf(':');
        if (separator < 0 || !long.TryParse(cycleId.AsSpan(separator + 1), out var sequence))
            throw new InvalidOperationException("--context-source-cycle-id must be gha:<github.repository_id>:<github.run_id>.");
        var identity = BundesligaContextSourceCycleIdentity.Create(profile.Competition, BundesligaContextSourceScope.ProductionLive, cycleId, sequence);
        var invocation = new CompetitionContextSourceCycleInvocation(identity, currentLane, producerLane, consumers);
        invocation.Validate();
        BundesligaContextSourceContract.ValidateConsumerAuthority(BundesligaContextSourceScope.ProductionLive, currentLane, communityContext);
        {
            var repositoryId = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_ID");
            var runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
            if (Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") != "ehonda/KicktippAi"
                || !CanonicalId(repositoryId) || !CanonicalId(runId)
                || cycleId != $"gha:{repositoryId}:{runId}"
                || producerLane != "pes-squad-context"
                || !consumers.SequenceEqual(BundesligaContextSourceContract.ProductionConsumers, StringComparer.Ordinal))
                throw new InvalidOperationException("Production source collection requires the exact Actions repository/run and full ordered consumer authority.");
        }
        return invocation;

        static bool CanonicalId(string? value) => value is { Length: > 0 and <= 19 } && value[0] is >= '1' and <= '9'
            && value.All(character => character is >= '0' and <= '9') && long.TryParse(value, out var id) && id > 0;

        static string Require(string? value, string option)
            => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{option} is required for production-live source cycles.") : value.Trim();
    }
}
