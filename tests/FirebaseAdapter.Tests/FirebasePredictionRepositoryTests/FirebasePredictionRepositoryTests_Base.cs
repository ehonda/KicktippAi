using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using FirebaseAdapter.Tests.Fixtures;
using Microsoft.Extensions.Logging.Testing;
using NodaTime;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Base class for FirebasePredictionRepository integration tests.
/// Uses a Firestore emulator container shared per test class.
/// Tests run sequentially to ensure data isolation via ClearDataAsync.
/// </summary>
[ClassDataSource<FirestoreFixture>(Shared = SharedType.PerClass)]
[NotInParallel("FirestoreEmulator")]
public abstract class FirebasePredictionRepositoryTests_Base(FirestoreFixture fixture)
{
    protected FirestoreFixture Fixture { get; } = fixture;

    /// <summary>
    /// Clears all data from the emulator before each test to ensure isolation.
    /// </summary>
    [Before(Test)]
    public async Task ClearTestData()
    {
        await Fixture.ClearDataAsync();
    }

    /// <summary>
    /// Creates a FirebasePredictionRepository instance with optional dependency overrides.
    /// </summary>
    /// <param name="logger">Optional logger. Defaults to a new FakeLogger.</param>
    /// <returns>A configured FirebasePredictionRepository instance.</returns>
    protected FirebasePredictionRepository CreateRepository(
        Option<FakeLogger<FirebasePredictionRepository>> logger = default)
    {
        var actualLogger = logger.Or(() => new FakeLogger<FirebasePredictionRepository>());
        return new FirebasePredictionRepository(Fixture.Db, actualLogger);
    }

    /// <summary>
    /// Creates a test match with default values.
    /// </summary>
    protected static Match CreateTestMatch(
        Option<string> homeTeam = default,
        Option<string> awayTeam = default,
        Option<ZonedDateTime> startsAt = default,
        Option<int> matchday = default)
    {
        return new Match(
            homeTeam.Or("Bayern München"),
            awayTeam.Or("Borussia Dortmund"),
            startsAt.Or(() => Instant.FromUtc(2025, 3, 15, 15, 30).InUtc()),
            matchday.Or(25));
    }

    /// <summary>
    /// Creates a test prediction with default values.
    /// </summary>
    protected static Prediction CreateTestPrediction(
        Option<int> homeGoals = default,
        Option<int> awayGoals = default,
        NullableOption<PredictionJustification> justification = default)
    {
        return new Prediction(
            homeGoals.Or(2),
            awayGoals.Or(1),
            justification.Or((PredictionJustification?)null));
    }

    /// <summary>
    /// Creates a test bonus question with default values.
    /// </summary>
    protected static BonusQuestion CreateTestBonusQuestion(
        Option<string> text = default,
        Option<List<BonusQuestionOption>> options = default)
    {
        var actualOptions = options.Or(() =>
        [
            new("opt-1", "Option 1"),
            new("opt-2", "Option 2"),
            new("opt-3", "Option 3")
        ]);
        
        return new BonusQuestion(
            text.Or("Who will win the league?"),
            Instant.FromUtc(2025, 5, 15, 18, 0).InUtc(),
            actualOptions,
            MaxSelections: 1);
    }

    /// <summary>
    /// Creates a test bonus prediction with default values.
    /// </summary>
    /// <param name="selectedOptionIds">Optional list of selected option IDs. Defaults to ["opt-1"].</param>
    protected static BonusPrediction CreateTestBonusPrediction(List<string>? selectedOptionIds = null)
    {
        return new BonusPrediction(selectedOptionIds ?? ["opt-1"]);
    }
}
