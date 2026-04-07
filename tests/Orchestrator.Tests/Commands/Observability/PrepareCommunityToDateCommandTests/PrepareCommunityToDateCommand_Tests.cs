using System.Text.Json;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;
using Orchestrator.Commands.Observability.PrepareCommunityToDate;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.PrepareCommunityToDateCommandTests;

public class PrepareCommunityToDateCommand_Tests
{
    [Test]
    public async Task Running_command_writes_manifest_with_cutoff_and_participant_predictions()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var matchdayOneStart = Instant.FromUtc(2025, 8, 22, 18, 30).InUtc();
            var matchdayTwoStart = Instant.FromUtc(2025, 8, 29, 18, 30).InUtc();
            var kicktippClient = new Mock<IKicktippClient>(MockBehavior.Strict);
            kicktippClient
                .Setup(client => client.GetCommunityMatchdaySnapshotAsync("test-community", 1))
                .ReturnsAsync(new KicktippCommunityMatchdaySnapshot(
                    1,
                    [new CollectedMatchOutcome("Team A", "Team B", matchdayOneStart, 1, 2, 1, MatchOutcomeAvailability.Completed, "101")],
                    [
                        new KicktippCommunityParticipantSnapshot(
                            "p1",
                            "Alice",
                            [new KicktippCommunityMatchPrediction(0, "101", "101", KicktippCommunityPredictionStatus.Placed, new BetPrediction(2, 1), 4)],
                            4,
                            4),
                        new KicktippCommunityParticipantSnapshot(
                            "p2",
                            "Bob",
                            [new KicktippCommunityMatchPrediction(0, "101", "101", KicktippCommunityPredictionStatus.Missed, null, 0)],
                            0,
                            0)
                    ]));
            kicktippClient
                .Setup(client => client.GetCommunityMatchdaySnapshotAsync("test-community", 2))
                .ReturnsAsync(new KicktippCommunityMatchdaySnapshot(
                    2,
                    [new CollectedMatchOutcome("Team C", "Team D", matchdayTwoStart, 2, 0, 0, MatchOutcomeAvailability.Completed, "102")],
                    [
                        new KicktippCommunityParticipantSnapshot(
                            "p1",
                            "Alice",
                            [new KicktippCommunityMatchPrediction(0, "102", "102", KicktippCommunityPredictionStatus.Missed, null, 0)],
                            0,
                            4),
                        new KicktippCommunityParticipantSnapshot(
                            "p2",
                            "Bob",
                            [new KicktippCommunityMatchPrediction(0, "102", "102", KicktippCommunityPredictionStatus.Placed, new BetPrediction(0, 1), 0)],
                            0,
                            0)
                    ]));

            var kicktippFactory = CreateMockKicktippClientFactory(kicktippClient);
            var commandContext = CreateCommandApp<PrepareCommunityToDateCommand>(
                "prepare-community-to-date",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(kicktippFactory.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                commandContext.App,
                commandContext.Console,
                "prepare-community-to-date",
                "--community-context",
                "test-community",
                "--cutoff-matchday",
                "2",
                "--output-directory",
                tempDirectory.FullName);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"mode\": \"community-to-date\"");

            var manifestJson = await File.ReadAllTextAsync(Path.Combine(tempDirectory.FullName, "slice-manifest.json"));
            using var manifestDocument = JsonDocument.Parse(manifestJson);
            var manifest = manifestDocument.RootElement;

            await Assert.That(manifest.GetProperty("task").GetString()).IsEqualTo("community-to-date");
            await Assert.That(manifest.GetProperty("cutoffMatchday").GetInt32()).IsEqualTo(2);
            await Assert.That(manifest.GetProperty("items").GetArrayLength()).IsEqualTo(2);
            await Assert.That(manifest.GetProperty("participants").GetArrayLength()).IsEqualTo(2);

            var firstItem = manifest.GetProperty("items")[0];
            await Assert.That(firstItem.GetProperty("sourceDatasetItemId").GetString()).IsEqualTo("bundesliga-2025-26__test-community__ts101");
            await Assert.That(firstItem.GetProperty("sliceDatasetItemId").GetString()).IsEqualTo("bundesliga-2025-26__test-community__ts101__slice__community-to-date-md02");

            var alice = manifest.GetProperty("participants").EnumerateArray()
                .Single(participant => participant.GetProperty("participantId").GetString() == "p1");
            await Assert.That(alice.GetProperty("predictions").GetArrayLength()).IsEqualTo(2);
            await Assert.That(alice.GetProperty("predictions")[0].GetProperty("status").GetString()).IsEqualTo("placed");
            await Assert.That(alice.GetProperty("predictions")[1].GetProperty("status").GetString()).IsEqualTo("missed");
            await Assert.That(alice.GetProperty("predictions")[0].GetProperty("sourceDatasetItemId").GetString()).IsEqualTo("bundesliga-2025-26__test-community__ts101");
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
