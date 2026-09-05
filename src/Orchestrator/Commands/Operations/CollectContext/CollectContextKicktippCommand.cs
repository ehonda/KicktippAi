using System.Globalization;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.Logging;
using NodaTime;
using Orchestrator.Commands.Operations.BundesligaHistory;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>
/// Command for collecting Kicktipp context documents and storing them in the database.
/// </summary>
public class CollectContextKicktippCommand : AsyncCommand<CollectContextKicktippSettings>
{
    private const int BundesligaOrdinaryOutcomeMatchdayCount = 34;

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly IContextProviderFactory _contextProviderFactory;
    private readonly MatchOutcomeCollectionService _matchOutcomeCollectionService;
    private readonly IBundesligaHistoryPlayedDateCollector _historyPlayedDateCollector;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CollectContextKicktippCommand> _logger;

    public CollectContextKicktippCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IContextProviderFactory contextProviderFactory,
        MatchOutcomeCollectionService matchOutcomeCollectionService,
        IBundesligaHistoryPlayedDateCollector historyPlayedDateCollector,
        TimeProvider timeProvider,
        ILogger<CollectContextKicktippCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _contextProviderFactory = contextProviderFactory;
        _matchOutcomeCollectionService = matchOutcomeCollectionService;
        _historyPlayedDateCollector = historyPlayedDateCollector;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CollectContextKicktippSettings settings,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithSettingsAsync(settings, cancellationToken);
    }

    internal async Task<int> ExecuteWithSettingsAsync(
        CollectContextKicktippSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.CommunityContext))
            {
                _console.MarkupLine("[red]Error: Community context is required[/]");
                return 1;
            }

            _console.MarkupLine("[green]Collect-context kicktipp command initialized[/]");
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }

            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");
            }

            if (settings.MatchOutcomesOnly)
            {
                _console.MarkupLine("[blue]Match outcomes only mode enabled - context documents will not be updated[/]");
            }

            if (settings.FullSeason)
            {
                _console.MarkupLine("[blue]Full-season Bundesliga context mode enabled[/]");
            }

            await ExecuteKicktippContextCollection(settings, cancellationToken);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing collect-context kicktipp command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private async Task ExecuteKicktippContextCollection(
        CollectContextKicktippSettings settings,
        CancellationToken cancellationToken)
    {
        var competition = CompetitionResolver.ResolveCompetition(
            settings.Competition,
            settings.CommunityContext,
            settings.CommunityContext);
        var isBundesliga2026 = string.Equals(
            competition,
            CompetitionIds.Bundesliga2026_27,
            StringComparison.Ordinal);
        ValidateProfileCounts(settings);

        // Validate all caller-controlled input before match-outcome collection, which may write.
        var requestedMatchdays = ParseMatchdays(settings.Matchdays);
        var fullSeasonMatchdayCount = ResolveFullSeasonMatchdayCount(
            settings,
            competition,
            isBundesliga2026,
            requestedMatchdays);

        if (settings.FullSeason)
        {
            await ExecuteFullSeasonCollectionAsync(
                settings,
                competition,
                fullSeasonMatchdayCount!.Value,
                cancellationToken);
            return;
        }

        var outcomeCollectionResult = await _matchOutcomeCollectionService.CollectAsync(
            settings.CommunityContext,
            settings.DryRun,
            competition,
            cancellationToken);
        PrintOutcomeCollectionSummary(outcomeCollectionResult, settings);

        if (settings.MatchOutcomesOnly)
        {
            var completionMessage = settings.DryRun
                ? "[magenta]✓ Match outcome dry run completed[/]"
                : "[green]✓ Match outcome collection completed![/]";
            _console.MarkupLine(completionMessage);
            return;
        }

        var kicktippClient = _kicktippClientFactory.CreateClient();
        var contextRepository = _firebaseServiceFactory.CreateContextRepository(competition);
        PrintTarget(settings, competition);

        var targetMatchdays = requestedMatchdays.Count > 0
            ? requestedMatchdays.Select<int, int?>(matchday => matchday).ToArray()
            : new int?[] { null };
        var pages = new List<TargetMatchdayCollection>(targetMatchdays.Length);
        foreach (var targetMatchday in targetMatchdays)
        {
            var page = await FetchMatchdayAsync(
                kicktippClient,
                settings,
                competition,
                targetMatchday,
                outcomeCollectionResult,
                cancellationToken);
            if (page is not null)
            {
                pages.Add(page);
            }
        }

        var collection = await CollectContextDocumentsAsync(
            kicktippClient,
            settings,
            competition,
            isBundesliga2026,
            pages,
            failOnConflictingContent: false,
            cancellationToken);
        if (isBundesliga2026 && collection.ExpectedSelectedHistoryDocumentNames.Count == 0)
        {
            throw new InvalidDataException(
                "Bundesliga context collection returned no fixtures and derived no expected selected-history documents.");
        }

        if (collection.Documents.Count == 0)
        {
            return;
        }

        var documents = isBundesliga2026
            ? await ApplyBundesligaHistoryGateAsync(
                settings,
                competition,
                collection.Documents,
                collection.ExpectedSelectedHistoryDocumentNames,
                BundesligaOrdinaryOutcomeMatchdayCount,
                requireCompleteFrozenMap: false,
                cancellationToken,
                contextRepository)
            : collection.Documents;

        _console.MarkupLine($"[green]Collected {documents.Count} unique context documents[/]");
        await PublishOrdinaryAsync(
            settings,
            contextRepository,
            isBundesliga2026,
            documents,
            cancellationToken);
    }

    private async Task ExecuteFullSeasonCollectionAsync(
        CollectContextKicktippSettings settings,
        string competition,
        int matchdayCount,
        CancellationToken cancellationToken)
    {
        var kicktippClient = _kicktippClientFactory.CreateClient();
        var contextRepository = _firebaseServiceFactory.CreateContextRepository(competition);
        PrintTarget(settings, competition);

        // Fetch and validate every profile-owned page before constructing a context provider. This keeps an
        // 8/9 page, a duplicate fixture, or a later page failure from reaching any context publication seam.
        var pages = new List<TargetMatchdayCollection>(matchdayCount);
        for (var matchday = 1; matchday <= matchdayCount; matchday++)
        {
            var page = await FetchRequiredFullSeasonMatchdayAsync(
                kicktippClient,
                settings,
                competition,
                matchday,
                cancellationToken);
            pages.Add(page);
        }

        var expectedDocumentNames = ValidateFullSeasonFixtures(settings, pages, matchdayCount);
        var collection = await CollectFullSeasonContextDocumentsAsync(
            kicktippClient,
            settings,
            competition,
            pages,
            cancellationToken);
        ValidateExactDocumentSet(
            "raw full-season Kicktipp context",
            collection.Documents.Keys,
            expectedDocumentNames);
        var expectedSelectedHistoryDocumentNames = BundesligaHistoryPlayedDateMap.ExpectedDocumentNames
            .ToHashSet(StringComparer.Ordinal);
        if (!collection.ExpectedSelectedHistoryDocumentNames.SetEquals(expectedSelectedHistoryDocumentNames))
        {
            throw SetMismatch(
                "derived full-season selected-history",
                collection.ExpectedSelectedHistoryDocumentNames,
                expectedSelectedHistoryDocumentNames);
        }

        // Only after the complete remote candidate set passes do we refresh outcomes. Context publication still
        // remains behind the history gate and a single atomic repository call.
        var outcomeCollectionResult = await _matchOutcomeCollectionService.CollectAsync(
            settings.CommunityContext,
            settings.DryRun,
            competition,
            cancellationToken);
        PrintOutcomeCollectionSummary(outcomeCollectionResult, settings);
        var documents = await ApplyBundesligaHistoryGateAsync(
            settings,
            competition,
            collection.Documents,
            expectedSelectedHistoryDocumentNames,
            matchdayCount,
            requireCompleteFrozenMap: true,
            cancellationToken);
        ValidateExactDocumentSet(
            "dated full-season Kicktipp context",
            documents.Keys,
            expectedDocumentNames);

        _console.MarkupLine($"[green]Collected and validated {documents.Count} exact full-season context documents[/]");
        await PublishFullSeasonAtomicallyAsync(settings, contextRepository, documents, cancellationToken);
    }

    private async Task<CollectedContextDocuments> CollectFullSeasonContextDocumentsAsync(
        IKicktippClient kicktippClient,
        CollectContextKicktippSettings settings,
        string competition,
        IReadOnlyList<TargetMatchdayCollection> pages,
        CancellationToken cancellationToken)
    {
        var matchdays = pages.ToDictionary(
            page => page.Matchday!.Value,
            page => page.Matches,
            EqualityComparer<int>.Default);
        var historyRequests = BundesligaFullSeasonHistorySelector.Select(matchdays);
        var providers = pages.ToDictionary(
            page => page.Matchday!.Value,
            page => _contextProviderFactory.CreateKicktippContextProvider(
                kicktippClient,
                settings.CommunityContext,
                competition,
                settings.CommunityContext,
                page.Matchday),
            EqualityComparer<int>.Default);
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);

        var firstProvider = providers[1];
        AddUniqueFullSeasonDocument(
            documents,
            await firstProvider.CurrentStandings(),
            "bundesliga-standings.csv",
            "shared standings");
        AddUniqueFullSeasonDocument(
            documents,
            await firstProvider.CommunityScoringRules(),
            $"community-rules-{settings.CommunityContext}.md",
            "shared rules");

        foreach (var request in historyRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = providers[request.Matchday];
            var document = request.Kind switch
            {
                BundesligaCanonicalHistoryKind.Recent => await provider.RecentHistory(request.TeamName),
                BundesligaCanonicalHistoryKind.Home => await provider.HomeHistory(
                    request.Match.HomeTeam,
                    request.Match.AwayTeam),
                BundesligaCanonicalHistoryKind.Away => await provider.AwayHistory(
                    request.Match.HomeTeam,
                    request.Match.AwayTeam),
                _ => throw new ArgumentOutOfRangeException(nameof(request.Kind), request.Kind, null)
            };
            AddUniqueFullSeasonDocument(
                documents,
                document,
                request.DocumentName,
                $"canonical {request.Kind.ToString().ToLowerInvariant()} history matchday {request.Matchday}");
            if (settings.Verbose)
            {
                _console.MarkupLine(
                    $"[dim]  Collected canonical history document: {Markup.Escape(document.Name)} " +
                    $"(matchday {request.Matchday})[/]");
            }
        }

        foreach (var page in pages.OrderBy(page => page.Matchday))
        {
            var provider = providers[page.Matchday!.Value];
            foreach (var matchWithHistory in page.Matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var match = matchWithHistory.Match;
                var identity = GetFixtureIdentity(match);
                var expectedName = $"head-to-head-{identity.HomeSlug}-vs-{identity.AwaySlug}.csv";
                _console.MarkupLine(
                    $"[cyan]Collecting matchday {page.Matchday} H2H for:[/] " +
                    $"{Markup.Escape(match.HomeTeam)} vs {Markup.Escape(match.AwayTeam)}");
                var document = await provider.HeadToHeadHistory(match.HomeTeam, match.AwayTeam);
                AddUniqueFullSeasonDocument(
                    documents,
                    document,
                    expectedName,
                    $"matchday {page.Matchday} ordered fixture");
                if (settings.Verbose)
                {
                    _console.MarkupLine($"[dim]  Collected H2H document: {Markup.Escape(document.Name)}[/]");
                }
            }
        }

        return new CollectedContextDocuments(
            documents,
            historyRequests.Select(request => request.DocumentName).ToHashSet(StringComparer.Ordinal));
    }

    private static void AddUniqueFullSeasonDocument(
        IDictionary<string, string> documents,
        DocumentContext document,
        string expectedName,
        string phase)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (documents.TryGetValue(document.Name, out var existing))
        {
            var existingHash = DocumentPublicationContract.ComputeContentSha256(existing);
            var incomingHash = DocumentPublicationContract.ComputeContentSha256(document.Content);
            throw new InvalidDataException(
                $"Full-season {phase} returned duplicate exact identity '{document.Name}'; " +
                $"existingBytes={System.Text.Encoding.UTF8.GetByteCount(existing)}, " +
                $"incomingBytes={System.Text.Encoding.UTF8.GetByteCount(document.Content)}, " +
                $"existingSha256={existingHash}, incomingSha256={incomingHash}.");
        }

        if (!string.Equals(document.Name, expectedName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Full-season {phase} returned document '{document.Name}' instead of exact identity '{expectedName}'.");
        }

        documents.Add(document.Name, document.Content);
    }

    private async Task<TargetMatchdayCollection?> FetchMatchdayAsync(
        IKicktippClient kicktippClient,
        CollectContextKicktippSettings settings,
        string competition,
        int? targetMatchday,
        MatchOutcomeCollectionResult? outcomeCollectionResult,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var matchdayLabel = targetMatchday.HasValue ? $"matchday {targetMatchday.Value}" : "current matchday";
        _console.MarkupLine($"[blue]Getting {matchdayLabel} matches...[/]");
        var matches = targetMatchday.HasValue
            ? await kicktippClient.GetMatchesWithHistoryAsync(
                settings.CommunityContext,
                targetMatchday.Value,
                competition)
            : await kicktippClient.GetMatchesWithHistoryAsync(settings.CommunityContext, competition);

        if (matches.Count == 0)
        {
            if (settings.ExpectedMatchesPerMatchday is int expectedMatches)
            {
                throw new InvalidDataException(
                    $"The {competition} profile expected exactly {expectedMatches} matches for {matchdayLabel}, but found 0.");
            }

            _console.MarkupLine($"[yellow]No matches found for {matchdayLabel}[/]");
            return null;
        }

        if (settings.ExpectedMatchesPerMatchday is int expectedMatchCount
            && matches.Count != expectedMatchCount
            && !IsExplainedCurrentOpenMatchdayCount(
                matches,
                expectedMatchCount,
                targetMatchday,
                outcomeCollectionResult))
        {
            throw new InvalidDataException(
                $"The {competition} profile expected exactly {expectedMatchCount} matches for {matchdayLabel}, " +
                $"but found {matches.Count}.");
        }

        _console.MarkupLine($"[green]Found {matches.Count} matches for {matchdayLabel}[/]");
        return new TargetMatchdayCollection(targetMatchday, matches);
    }

    private static bool IsExplainedCurrentOpenMatchdayCount(
        IReadOnlyList<MatchWithHistory> matches,
        int expectedMatchCount,
        int? targetMatchday,
        MatchOutcomeCollectionResult? outcomeCollectionResult)
    {
        if (targetMatchday.HasValue
            || outcomeCollectionResult is null
            || matches.Count >= expectedMatchCount
            || matches.Any(match => match.Match.Matchday != outcomeCollectionResult.CurrentMatchday))
        {
            return false;
        }

        var currentMatchdaySummaries = outcomeCollectionResult.MatchdaySummaries
            .Where(summary => summary.Matchday == outcomeCollectionResult.CurrentMatchday)
            .ToArray();
        if (currentMatchdaySummaries.Length != 1)
        {
            return false;
        }

        var summary = currentMatchdaySummaries[0];
        if (summary.FetchedMatches != expectedMatchCount
            || summary.Outcomes.Count != expectedMatchCount
            || summary.CompletedMatches != expectedMatchCount - matches.Count
            || summary.PendingMatches != matches.Count
            || summary.Outcomes.Any(outcome => outcome.Matchday != outcomeCollectionResult.CurrentMatchday)
            || summary.Outcomes.Any(outcome => string.IsNullOrWhiteSpace(outcome.TippSpielId))
            || summary.Outcomes
                .Select(outcome => outcome.TippSpielId!)
                .Distinct(StringComparer.Ordinal)
                .Count() != expectedMatchCount)
        {
            return false;
        }

        var outcomeFixtureIdentities = summary.Outcomes
            .Select(outcome => new LeagueFixtureIdentity(outcome.HomeTeam, outcome.AwayTeam))
            .ToHashSet(LeagueFixtureIdentityComparer.Instance);
        if (outcomeFixtureIdentities.Count != expectedMatchCount)
        {
            return false;
        }

        var openFixtureIdentities = matches
            .Select(match => new LeagueFixtureIdentity(match.Match.HomeTeam, match.Match.AwayTeam))
            .ToHashSet(LeagueFixtureIdentityComparer.Instance);
        if (openFixtureIdentities.Count != matches.Count)
        {
            return false;
        }

        var pendingOutcomeFixtureIdentities = summary.Outcomes
            .Where(outcome => !outcome.HasOutcome)
            .Select(outcome => new LeagueFixtureIdentity(outcome.HomeTeam, outcome.AwayTeam))
            .ToHashSet(LeagueFixtureIdentityComparer.Instance);
        return pendingOutcomeFixtureIdentities.Count == matches.Count
            && pendingOutcomeFixtureIdentities.SetEquals(openFixtureIdentities);
    }

    private async Task<TargetMatchdayCollection> FetchRequiredFullSeasonMatchdayAsync(
        IKicktippClient kicktippClient,
        CollectContextKicktippSettings settings,
        string competition,
        int matchday,
        CancellationToken cancellationToken)
    {
        var page = await FetchMatchdayAsync(
            kicktippClient,
            settings,
            competition,
            matchday,
            outcomeCollectionResult: null,
            cancellationToken)
            ?? throw new InvalidDataException($"Full-season matchday {matchday} returned no fixtures.");
        var identities = page.Matches.Select(match => GetFixtureIdentity(match.Match)).ToArray();
        if (page.Matches.Any(match => match.Match.Matchday != matchday))
        {
            throw new InvalidDataException(
                $"Full-season matchday {matchday} returned a fixture whose matchday identity does not equal {matchday}.");
        }

        var duplicate = identities.GroupBy(identity => identity).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Full-season matchday {matchday} contains duplicate ordered fixture identity " +
                $"'{duplicate.Key.HomeSlug}>{duplicate.Key.AwaySlug}'.");
        }

        var participantSlugs = identities
            .SelectMany(identity => new[] { identity.HomeSlug, identity.AwaySlug })
            .ToArray();
        if (participantSlugs.Distinct(StringComparer.Ordinal).Count() != participantSlugs.Length)
        {
            throw new InvalidDataException(
                $"Full-season matchday {matchday} must contain each participating club exactly once.");
        }

        return page;
    }

    private static IReadOnlySet<string> ValidateFullSeasonFixtures(
        CollectContextKicktippSettings settings,
        IReadOnlyList<TargetMatchdayCollection> pages,
        int matchdayCount)
    {
        var expectedMatchCount = settings.ExpectedMatchCount!.Value;
        if (pages.Count != matchdayCount)
        {
            throw new InvalidDataException(
                $"Full-season collection expected {matchdayCount} matchday pages but received {pages.Count}.");
        }

        var actualFixtures = pages.SelectMany(page => page.Matches)
            .Select(match => GetFixtureIdentity(match.Match))
            .ToArray();
        if (actualFixtures.Length != expectedMatchCount)
        {
            throw new InvalidDataException(
                $"Full-season collection expected {expectedMatchCount} fixtures but received {actualFixtures.Length}.");
        }

        var duplicate = actualFixtures.GroupBy(identity => identity).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Full-season collection contains duplicate ordered fixture identity " +
                $"'{duplicate.Key.HomeSlug}>{duplicate.Key.AwaySlug}'.");
        }

        var teams = BundesligaTeamManifest.Default.Entries;
        var expectedFixtures = teams
            .SelectMany(home => teams
                .Where(away => !string.Equals(home.TeamSlug, away.TeamSlug, StringComparison.Ordinal))
                .Select(away => (HomeSlug: home.TeamSlug, AwaySlug: away.TeamSlug)))
            .ToHashSet();
        var actualFixtureSet = actualFixtures.ToHashSet();
        if (!actualFixtureSet.SetEquals(expectedFixtures))
        {
            var missing = expectedFixtures.Except(actualFixtureSet)
                .OrderBy(identity => identity.HomeSlug, StringComparer.Ordinal)
                .ThenBy(identity => identity.AwaySlug, StringComparer.Ordinal)
                .Select(identity => $"{identity.HomeSlug}>{identity.AwaySlug}");
            var unexpected = actualFixtureSet.Except(expectedFixtures)
                .OrderBy(identity => identity.HomeSlug, StringComparer.Ordinal)
                .ThenBy(identity => identity.AwaySlug, StringComparer.Ordinal)
                .Select(identity => $"{identity.HomeSlug}>{identity.AwaySlug}");
            throw new InvalidDataException(
                $"Full-season ordered fixture set is incomplete; missing=[{string.Join(',', missing)}], " +
                $"unexpected=[{string.Join(',', unexpected)}].");
        }

        var expectedDocumentNames = GetExpectedFullSeasonKicktippDocumentNames(settings.CommunityContext);
        var expectedHeadToHeadNames = expectedDocumentNames
            .Where(name => name.StartsWith("head-to-head-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var actualHeadToHeadNames = actualFixtures
            .Select(identity => $"head-to-head-{identity.HomeSlug}-vs-{identity.AwaySlug}.csv")
            .ToHashSet(StringComparer.Ordinal);
        if (expectedHeadToHeadNames.Count != expectedMatchCount
            || !actualHeadToHeadNames.SetEquals(expectedHeadToHeadNames))
        {
            throw SetMismatch(
                "full-season fixture H2H vs strict catalog",
                actualHeadToHeadNames,
                expectedHeadToHeadNames);
        }

        return expectedDocumentNames;
    }

    private async Task<CollectedContextDocuments> CollectContextDocumentsAsync(
        IKicktippClient kicktippClient,
        CollectContextKicktippSettings settings,
        string competition,
        bool isBundesliga2026,
        IReadOnlyList<TargetMatchdayCollection> pages,
        bool failOnConflictingContent,
        CancellationToken cancellationToken)
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);
        var selectedHistoryNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = _contextProviderFactory.CreateKicktippContextProvider(
                kicktippClient,
                settings.CommunityContext,
                competition,
                settings.CommunityContext,
                page.Matchday);

            if (isBundesliga2026)
            {
                foreach (var requiredDocumentName in page.Matches
                    .SelectMany(match => MatchContextDocumentCatalog.ForMatch(
                        match.Match,
                        settings.CommunityContext,
                        competition).RequiredDocumentNames)
                    .Where(BundesligaHistoryPlayedDateCollector.IsSelectedDocumentName))
                {
                    selectedHistoryNames.Add(requiredDocumentName);
                }
            }

            foreach (var matchWithHistory in page.Matches)
            {
                var match = matchWithHistory.Match;
                _console.MarkupLine($"[cyan]Collecting context for:[/] {Markup.Escape(match.HomeTeam)} vs {Markup.Escape(match.AwayTeam)}");
                try
                {
                    var matchContext = match.CompetitionSpecificData is FifaWorldCup2026MatchData
                        ? provider.GetMatchContextAsync(match, cancellationToken)
                        : provider.GetMatchContextAsync(match.HomeTeam, match.AwayTeam, cancellationToken);
                    await foreach (var contextDocument in matchContext.WithCancellation(cancellationToken))
                    {
                        if (documents.TryGetValue(contextDocument.Name, out var existingContent))
                        {
                            if (failOnConflictingContent
                                && !string.Equals(existingContent, contextDocument.Content, StringComparison.Ordinal))
                            {
                                throw new InvalidDataException(
                                    $"Full-season provider returned conflicting content for '{contextDocument.Name}'.");
                            }

                            continue;
                        }

                        documents.Add(contextDocument.Name, contextDocument.Content);
                        if (settings.Verbose)
                        {
                            _console.MarkupLine($"[dim]  Collected context document: {Markup.Escape(contextDocument.Name)}[/]");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to collect context for match {HomeTeam} vs {AwayTeam}",
                        match.HomeTeam,
                        match.AwayTeam);
                    _console.MarkupLine($"[red]  ✗ Failed to collect context: {Markup.Escape(ex.Message)}[/]");
                    if (isBundesliga2026)
                    {
                        throw new InvalidDataException(
                            $"Bundesliga context collection failed for {match.HomeTeam} vs {match.AwayTeam}; " +
                            "the complete selected-history set was not published.",
                            ex);
                    }
                }
            }
        }

        return new CollectedContextDocuments(documents, selectedHistoryNames);
    }

    private async Task<Dictionary<string, string>> ApplyBundesligaHistoryGateAsync(
        CollectContextKicktippSettings settings,
        string competition,
        IReadOnlyDictionary<string, string> documents,
        IReadOnlySet<string> expectedSelectedHistoryDocumentNames,
        int outcomeMatchdayCount,
        bool requireCompleteFrozenMap,
        CancellationToken cancellationToken,
        IContextRepository? contextRepository = null)
    {
        BundesligaHistoryPlayedDateCollectionOptions? proxyOptions = null;
        if (!requireCompleteFrozenMap)
        {
            if (contextRepository is null)
            {
                throw new InvalidOperationException("Ordinary Bundesliga history collection requires a context repository for strict prior reads.");
            }
            var collectionInstant = Instant.FromDateTimeOffset(_timeProvider.GetUtcNow());
            var priorContents = await LoadPriorSelectedHistoryDocumentContentsAsync(
                contextRepository,
                settings.CommunityContext,
                expectedSelectedHistoryDocumentNames,
                cancellationToken);
            proxyOptions = new BundesligaHistoryPlayedDateCollectionOptions(priorContents, collectionInstant);
        }
        var matchOutcomes = await LoadBundesligaMatchOutcomesAsync(
            settings.CommunityContext,
            competition,
            outcomeMatchdayCount,
            cancellationToken);
        var dateMap = BundesligaHistoryPlayedDateMap.Default.Entries;
        var incomingDocuments = documents.Select(pair => new BundesligaHistoryDocument(pair.Key, pair.Value)).ToArray();
        var collection = proxyOptions is null
            ? _historyPlayedDateCollector.Collect(
                competition,
                incomingDocuments,
                dateMap,
                matchOutcomes,
                expectedSelectedHistoryDocumentNames)
            : _historyPlayedDateCollector.Collect(
                competition,
                incomingDocuments,
                dateMap,
                matchOutcomes,
                expectedSelectedHistoryDocumentNames,
                proxyOptions);
        if (!collection.Succeeded)
        {
            var details = string.Join(Environment.NewLine, collection.Diagnostics.Take(20)
                .Select(value => $"{value.DocumentName}#{value.RowOrdinal?.ToString() ?? "-"}: {value.Message}"));
            throw new InvalidDataException(
                $"Bundesliga history played-date gate failed; no context documents were saved.{Environment.NewLine}{details}");
        }

        _console.MarkupLine(
            $"[green]Bundesliga history played-date gate passed:[/] {collection.Resolutions.Count} completed occurrence(s), " +
            $"tuple-groups={collection.DistinctTupleGroupCount}; " +
            $"existing={collection.PreservedCount}, Kicktipp={collection.KicktippCount}, fixed-map={collection.FixedMapCount}, " +
            $"collection-date-proxy={collection.CollectionDateProxyCount}, excluded-incomplete={collection.ExcludedIncompleteRowCount}; " +
            $"fixed-map sources: {Markup.Escape(BundesligaHistoryCommandSupport.FormatFixedSourceCounts(collection))}");
        return collection.Documents.ToDictionary(
            document => document.Name,
            document => document.Content,
            StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyDictionary<string, string?>> LoadPriorSelectedHistoryDocumentContentsAsync(
        IContextRepository contextRepository,
        string communityContext,
        IReadOnlySet<string> expectedSelectedHistoryDocumentNames,
        CancellationToken cancellationToken)
    {
        var contents = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var documentName in expectedSelectedHistoryDocumentNames.Order(StringComparer.Ordinal))
        {
            var prior = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext, cancellationToken);
            if (prior is not null && !string.Equals(prior.DocumentName, documentName, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Latest selected-history document read returned '{prior.DocumentName}' for requested '{documentName}'.");
            }
            contents.Add(documentName, prior?.Content);
        }
        return contents;
    }

    private async Task PublishFullSeasonAtomicallyAsync(
        CollectContextKicktippSettings settings,
        IContextRepository contextRepository,
        IReadOnlyDictionary<string, string> documents,
        CancellationToken cancellationToken)
    {
        var writes = documents
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ContextDocumentWrite(pair.Key, pair.Value))
            .ToArray();
        if (settings.DryRun)
        {
            _console.MarkupLine(
                $"[magenta]✓ Full-season dry run completed - would atomically publish {writes.Length} documents[/]");
            return;
        }

        var results = await contextRepository.SaveContextDocumentsAtomicallyAsync(
            writes,
            settings.CommunityContext,
            cancellationToken);
        if (results.Count != writes.Length)
        {
            throw new InvalidDataException(
                $"Atomic repository returned {results.Count} results for {writes.Length} full-season documents.");
        }

        _console.MarkupLine("[green]✓ Full-season context collection completed atomically![/]");
        _console.MarkupLine($"[green]  Saved: {results.Count(result => result.Version.HasValue)} documents[/]");
        _console.MarkupLine($"[dim]  Skipped: {results.Count(result => !result.Version.HasValue)} documents (unchanged)[/]");
    }

    private async Task PublishOrdinaryAsync(
        CollectContextKicktippSettings settings,
        IContextRepository contextRepository,
        bool isBundesliga2026,
        IReadOnlyDictionary<string, string> documents,
        CancellationToken cancellationToken)
    {
        var savedCount = 0;
        var skippedCount = 0;
        var currentDate = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var selectedHistoryDocuments = isBundesliga2026
            ? documents
                .Where(pair => BundesligaHistoryPlayedDateCollector.IsSelectedDocumentName(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ContextDocumentWrite(pair.Key, pair.Value))
                .ToArray()
            : [];

        if (selectedHistoryDocuments.Length > 0)
        {
            if (settings.DryRun)
            {
                foreach (var document in selectedHistoryDocuments)
                {
                    _console.MarkupLine($"[magenta]  Dry run - would atomically save:[/] {Markup.Escape(document.DocumentName)}");
                }
            }
            else
            {
                var batchResults = await contextRepository.SaveContextDocumentsAtomicallyAsync(
                    selectedHistoryDocuments,
                    settings.CommunityContext,
                    cancellationToken);
                savedCount += batchResults.Count(result => result.Version.HasValue);
                skippedCount += batchResults.Count(result => !result.Version.HasValue);
                if (settings.Verbose)
                {
                    _console.MarkupLine(
                        $"[green]  ✓ Atomically published {selectedHistoryDocuments.Length} selected Bundesliga history document(s)[/]");
                }
            }
        }

        foreach (var (documentName, content) in documents)
        {
            if (isBundesliga2026 && BundesligaHistoryPlayedDateCollector.IsSelectedDocumentName(documentName))
            {
                continue;
            }

            try
            {
                if (settings.DryRun)
                {
                    _console.MarkupLine($"[magenta]  Dry run - would save:[/] {Markup.Escape(documentName)}");
                    continue;
                }

                var finalContent = content;
                if (IsHistoryDocument(documentName))
                {
                    var previousDocument = await contextRepository.GetLatestContextDocumentAsync(
                        documentName,
                        settings.CommunityContext,
                        cancellationToken);
                    finalContent = HistoryCsvUtility.AddDataCollectedAtColumn(
                        content,
                        previousDocument?.Content,
                        currentDate);
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  Added Data_Collected_At column to {Markup.Escape(documentName)}[/]");
                    }
                }

                var savedVersion = await contextRepository.SaveContextDocumentAsync(
                    documentName,
                    finalContent,
                    settings.CommunityContext,
                    cancellationToken);
                if (savedVersion.HasValue)
                {
                    savedCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[green]  ✓ Saved {Markup.Escape(documentName)} as version {savedVersion.Value}[/]");
                    }
                }
                else
                {
                    skippedCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  - Skipped {Markup.Escape(documentName)} (content unchanged)[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save context document {DocumentName}", documentName);
                _console.MarkupLine($"[red]  ✗ Failed to save {Markup.Escape(documentName)}: {Markup.Escape(ex.Message)}[/]");
            }
        }

        if (settings.DryRun)
        {
            _console.MarkupLine($"[magenta]✓ Dry run completed - would have processed {documents.Count} documents[/]");
        }
        else
        {
            _console.MarkupLine("[green]✓ Context collection completed![/]");
            _console.MarkupLine($"[green]  Saved: {savedCount} documents[/]");
            _console.MarkupLine($"[dim]  Skipped: {skippedCount} documents (unchanged)[/]");
        }
    }

    private static int? ResolveFullSeasonMatchdayCount(
        CollectContextKicktippSettings settings,
        string competition,
        bool isBundesliga2026,
        IReadOnlyList<int> requestedMatchdays)
    {
        if (!settings.FullSeason)
        {
            return null;
        }

        if (!isBundesliga2026)
        {
            throw new NotSupportedException(
                $"Full-season context collection is supported only for '{CompetitionIds.Bundesliga2026_27}', not '{competition}'.");
        }

        if (settings.MatchOutcomesOnly)
        {
            throw new ArgumentException("--full-season cannot be combined with --match-outcomes-only.");
        }

        if (requestedMatchdays.Count > 0)
        {
            throw new ArgumentException("--full-season cannot be combined with --matchdays.");
        }

        if (settings.ExpectedMatchCount is not int expectedMatchCount
            || settings.ExpectedMatchesPerMatchday is not int expectedMatchesPerMatchday)
        {
            throw new InvalidOperationException(
                "--full-season requires the typed competition profile's expected season and matchday fixture counts; " +
                "use collect-context-dev or collect-context-profile.");
        }

        if (expectedMatchCount % expectedMatchesPerMatchday != 0)
        {
            throw new InvalidDataException(
                $"The {competition} profile fixture count {expectedMatchCount} is not divisible by " +
                $"its {expectedMatchesPerMatchday} fixtures per matchday.");
        }

        return expectedMatchCount / expectedMatchesPerMatchday;
    }

    private static void ValidateProfileCounts(CollectContextKicktippSettings settings)
    {
        if (settings.ExpectedMatchCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings.ExpectedMatchCount),
                settings.ExpectedMatchCount,
                "Expected season match count must be a positive integer when supplied by a competition profile.");
        }

        if (settings.ExpectedMatchesPerMatchday is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings.ExpectedMatchesPerMatchday),
                settings.ExpectedMatchesPerMatchday,
                "Expected matches per matchday must be a positive integer when supplied by a competition profile.");
        }
    }

    private static IReadOnlySet<string> GetExpectedFullSeasonKicktippDocumentNames(string communityContext)
    {
        var expectedHistory = BundesligaHistoryPlayedDateMap.ExpectedDocumentNames.ToHashSet(StringComparer.Ordinal);
        return BundesligaContextHygienePolicy.GetExpectedDocuments(communityContext)
            .Where(document => document.Key.Kind == DocumentPublicationKind.Context)
            .Select(document => document.Key.Name)
            .Where(name => string.Equals(name, "bundesliga-standings.csv", StringComparison.Ordinal)
                           || string.Equals(name, $"community-rules-{communityContext}.md", StringComparison.Ordinal)
                           || expectedHistory.Contains(name)
                           || name.StartsWith("head-to-head-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static (string HomeSlug, string AwaySlug) GetFixtureIdentity(Match match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var manifest = BundesligaTeamManifest.Default;
        var home = manifest.GetByKicktippName(match.HomeTeam).TeamSlug;
        var away = manifest.GetByKicktippName(match.AwayTeam).TeamSlug;
        if (string.Equals(home, away, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Fixture '{home}>{away}' cannot use the same team twice.");
        }

        return (home, away);
    }

    private static void ValidateExactDocumentSet(
        string label,
        IEnumerable<string> actualNames,
        IReadOnlySet<string> expectedNames)
    {
        var actual = actualNames.ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expectedNames))
        {
            throw SetMismatch(label, actual, expectedNames);
        }
    }

    private static InvalidDataException SetMismatch(
        string label,
        IReadOnlySet<string> actual,
        IReadOnlySet<string> expected)
    {
        var missing = expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var unexpected = actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal);
        return new InvalidDataException(
            $"{label} set mismatch; missing=[{string.Join(',', missing)}], " +
            $"unexpected=[{string.Join(',', unexpected)}].");
    }

    private static bool IsHistoryDocument(string documentName)
    {
        return documentName.StartsWith("recent-history-", StringComparison.OrdinalIgnoreCase)
               || documentName.StartsWith("home-history-", StringComparison.OrdinalIgnoreCase)
               || documentName.StartsWith("away-history-", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<int> ParseMatchdays(string? matchdays)
    {
        if (string.IsNullOrWhiteSpace(matchdays))
        {
            return [];
        }

        var result = new List<int>();
        foreach (var token in matchdays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(token, out var matchday) || matchday <= 0)
            {
                throw new ArgumentException($"Invalid matchday '{token}'. Use positive integers separated by commas.");
            }

            if (!result.Contains(matchday))
            {
                result.Add(matchday);
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<PersistedMatchOutcome>> LoadBundesligaMatchOutcomesAsync(
        string communityContext,
        string competition,
        int matchdayCount,
        CancellationToken cancellationToken)
    {
        var repository = _firebaseServiceFactory.CreateMatchOutcomeRepository(competition);
        var outcomes = new List<PersistedMatchOutcome>();
        for (var matchday = 1; matchday <= matchdayCount; matchday++)
        {
            outcomes.AddRange(await repository.GetMatchdayOutcomesAsync(
                matchday,
                communityContext,
                cancellationToken));
        }

        return outcomes.AsReadOnly();
    }

    private void PrintTarget(CollectContextKicktippSettings settings, string competition)
    {
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{Markup.Escape(settings.CommunityContext)}[/]");
        _console.MarkupLine($"[blue]Using competition:[/] [yellow]{Markup.Escape(competition)}[/]");
    }

    private void PrintOutcomeCollectionSummary(
        MatchOutcomeCollectionResult result,
        CollectContextKicktippSettings settings)
    {
        _console.MarkupLine($"[blue]Current tippuebersicht matchday:[/] [yellow]{result.CurrentMatchday}[/]");
        if (!result.IncompleteMatchdays.Any())
        {
            _console.MarkupLine("[green]All persisted matchdays up to the current matchday are already complete[/]");
            return;
        }

        _console.MarkupLine($"[blue]Incomplete matchdays to check:[/] [yellow]{string.Join(", ", result.IncompleteMatchdays)}[/]");
        foreach (var summary in result.MatchdaySummaries)
        {
            if (settings.DryRun)
            {
                _console.MarkupLine(
                    $"[magenta]  Dry run - would evaluate matchday {summary.Matchday}[/] " +
                    $"({summary.FetchedMatches} matches, {summary.CompletedMatches} completed, {summary.PendingMatches} pending)");
                continue;
            }

            _console.MarkupLine(
                $"[green]  Matchday {summary.Matchday}:[/] {summary.FetchedMatches} matches, " +
                $"created {summary.CreatedCount}, updated {summary.UpdatedCount}, unchanged {summary.UnchangedCount}, " +
                $"pending {summary.PendingMatches}");
        }
    }

    private sealed record TargetMatchdayCollection(
        int? Matchday,
        IReadOnlyList<MatchWithHistory> Matches);

    private readonly record struct LeagueFixtureIdentity(string HomeTeam, string AwayTeam);

    private sealed class LeagueFixtureIdentityComparer : IEqualityComparer<LeagueFixtureIdentity>
    {
        public static LeagueFixtureIdentityComparer Instance { get; } = new();

        public bool Equals(LeagueFixtureIdentity x, LeagueFixtureIdentity y)
        {
            return string.Equals(x.HomeTeam, y.HomeTeam, StringComparison.Ordinal)
                && string.Equals(x.AwayTeam, y.AwayTeam, StringComparison.Ordinal);
        }

        public int GetHashCode(LeagueFixtureIdentity obj)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.HomeTeam),
                StringComparer.Ordinal.GetHashCode(obj.AwayTeam));
        }
    }

    private sealed record CollectedContextDocuments(
        Dictionary<string, string> Documents,
        HashSet<string> ExpectedSelectedHistoryDocumentNames);
}
