using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetOpenPredictionsAsync method.
/// </summary>
public class KicktippClient_GetOpenPredictions_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_open_predictions_returns_empty_list_on_404()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsEmpty();
    }

    [Test]
    public async Task Getting_open_predictions_returns_empty_list_when_table_is_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head><title>Kicktipp</title></head>
            <body>
                <div class="content">
                    <p>No predictions available</p>
                </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsEmpty();
    }

    [Test]
    public async Task Getting_open_predictions_parses_matches_with_date_inheritance()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).HasCount().EqualTo(3);
        
        // First match has explicit date
        await Assert.That(matches[0].HomeTeam).IsEqualTo("Team A");
        await Assert.That(matches[0].AwayTeam).IsEqualTo("Team B");
        await Assert.That(matches[0].Matchday).IsEqualTo(5);
        
        // Second match inherits date from first
        await Assert.That(matches[1].HomeTeam).IsEqualTo("Team C");
        await Assert.That(matches[1].AwayTeam).IsEqualTo("Team D");
        
        // Third match has new explicit date
        await Assert.That(matches[2].HomeTeam).IsEqualTo("Team E");
        await Assert.That(matches[2].AwayTeam).IsEqualTo("Team F");
    }

    [Test]
    public async Task Getting_open_predictions_extracts_matchday_from_title()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsNotEmpty();
        await Assert.That(matches[0].Matchday).IsEqualTo(5);
    }

    [Test]
    public async Task Getting_open_predictions_extracts_round_from_hidden_field_before_navigation_label()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <input type="hidden" name="spieltagIndex" value="37" />
            <div class="prevnextTitle"><a>Achtelfinale</a></div>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>20.06.2026 21:00</td>
                        <td>Germany</td>
                        <td>Brazil</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
                        </td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("test-community");

        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].Matchday).IsEqualTo(37);
    }

    [Test]
    public async Task Getting_open_predictions_parses_berlin_summer_time_dates()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>3</a></div>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>20.06.2026 21:00</td>
                        <td>Germany</td>
                        <td>Brazil</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
                        </td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("test-community");

        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].StartsAt.Zone.Id).IsEqualTo("Europe/Berlin");
        await Assert.That(matches[0].StartsAt.Offset.ToString()).IsEqualTo("+02");
        await Assert.That(matches[0].StartsAt.Hour).IsEqualTo(21);
    }

    [Test]
    public async Task Getting_open_predictions_handles_rows_without_betting_inputs()
    {
        // Arrange - HTML with rows that have too few cells or no betting inputs
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                    </tr>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team B</td>
                        <td>Team C</td>
                        <td><span>Already played</span></td>
                    </tr>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team D</td>
                        <td>Team E</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
                        </td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert - only the row with betting inputs should be included
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].HomeTeam).IsEqualTo("Team D");
        await Assert.That(matches[0].AwayTeam).IsEqualTo("Team E");
    }

    [Test]
    public async Task Getting_open_predictions_handles_exception_in_row_parsing()
    {
        // Arrange - HTML that might cause parsing issues but should be handled gracefully
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Valid Team A</td>
                        <td>Valid Team B</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
                        </td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Getting_open_predictions_extracts_matchday_from_hidden_input_as_fallback()
    {
        // Arrange - HTML with hidden input for matchday but no title
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <input name="spieltagIndex" value="7" />
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
                        </td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsNotEmpty();
        await Assert.That(matches[0].Matchday).IsEqualTo(7);
    }

    [Test]
    public async Task Getting_open_predictions_defaults_matchday_to_1_when_not_found()
    {
        // Arrange - HTML without matchday information
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
                        </td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsNotEmpty();
        await Assert.That(matches[0].Matchday).IsEqualTo(1);
    }

    [Test]
    public async Task Getting_open_predictions_with_real_fixture_returns_valid_matchday()
    {
        // Arrange - use encrypted real fixture for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        const string community = "ehonda-test-buli";
        StubWithRealFixture(community, "tippabgabe");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync(community);

        // Assert - Bundesliga matchday typically has 9 matches
        await Assert.That(matches).HasCount().GreaterThanOrEqualTo(9);
        
        // All matches should have valid data
        foreach (var match in matches)
        {
            await Assert.That(match.HomeTeam).IsNotEmpty();
            await Assert.That(match.AwayTeam).IsNotEmpty();
            await Assert.That(match.HomeTeam).IsNotEqualTo(match.AwayTeam);
            await Assert.That(match.Matchday).IsGreaterThan(0);
        }
        
        // All matches should be in the same matchday
        var matchdays = matches.Select(m => m.Matchday).Distinct().ToList();
        await Assert.That(matchdays).HasCount().EqualTo(1);
        
        // Matches should have valid times (hours in reasonable range)
        foreach (var match in matches)
        {
            await Assert.That(match.StartsAt.Hour).IsGreaterThanOrEqualTo(0);
            await Assert.That(match.StartsAt.Hour).IsLessThan(24);
        }
    }

    /// <summary>
    /// Verifies that cancelled matches ("Abgesagt") are detected and marked with IsCancelled = true.
    /// Cancelled matches should still be included in the results since Kicktipp allows placing predictions on them.
    /// See docs/features/cancelled-matches.md for design rationale.
    /// </summary>
    [Test]
    public async Task Getting_open_predictions_detects_cancelled_matches()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-cancelled");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert - all 4 matches should be returned (including cancelled ones)
        await Assert.That(matches).HasCount().EqualTo(4);
        
        // First match: normal
        var firstMatch = matches.First(m => m.HomeTeam == "Team A");
        await Assert.That(firstMatch.IsCancelled).IsFalse();
        
        // Second match: cancelled (should have IsCancelled = true)
        var cancelledMatch1 = matches.First(m => m.HomeTeam == "Team C");
        await Assert.That(cancelledMatch1.IsCancelled).IsTrue();
        
        // Third match: normal
        var thirdMatch = matches.First(m => m.HomeTeam == "Team E");
        await Assert.That(thirdMatch.IsCancelled).IsFalse();
        
        // Fourth match: also cancelled
        var cancelledMatch2 = matches.First(m => m.HomeTeam == "Team G");
        await Assert.That(cancelledMatch2.IsCancelled).IsTrue();
    }

    /// <summary>
    /// Verifies that cancelled matches inherit the time from the previous match in the table.
    /// This is critical for database key consistency since startsAt is part of the composite key.
    /// </summary>
    [Test]
    public async Task Getting_open_predictions_cancelled_matches_inherit_previous_time()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-cancelled");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        // First match: explicit time 15:30
        var firstMatch = matches.First(m => m.HomeTeam == "Team A");
        await Assert.That(firstMatch.StartsAt.Hour).IsEqualTo(15);
        await Assert.That(firstMatch.StartsAt.Minute).IsEqualTo(30);
        
        // Second match (cancelled): should inherit 15:30 from first match
        var cancelledMatch1 = matches.First(m => m.HomeTeam == "Team C");
        await Assert.That(cancelledMatch1.StartsAt.Hour).IsEqualTo(15);
        await Assert.That(cancelledMatch1.StartsAt.Minute).IsEqualTo(30);
        await Assert.That(cancelledMatch1.IsCancelled).IsTrue();
        
        // Third match: new explicit time 18:30
        var thirdMatch = matches.First(m => m.HomeTeam == "Team E");
        await Assert.That(thirdMatch.StartsAt.Hour).IsEqualTo(18);
        await Assert.That(thirdMatch.StartsAt.Minute).IsEqualTo(30);
        
        // Fourth match (cancelled): should inherit 18:30 from third match
        var cancelledMatch2 = matches.First(m => m.HomeTeam == "Team G");
        await Assert.That(cancelledMatch2.StartsAt.Hour).IsEqualTo(18);
        await Assert.That(cancelledMatch2.StartsAt.Minute).IsEqualTo(30);
        await Assert.That(cancelledMatch2.IsCancelled).IsTrue();
    }

    [Test]
    [Arguments("Sechzehntelfinale", FifaWorldCup2026KnockoutStage.RoundOf32)]
    [Arguments("Achtelfinale", FifaWorldCup2026KnockoutStage.RoundOf16)]
    [Arguments("Viertelfinale", FifaWorldCup2026KnockoutStage.Quarterfinal)]
    [Arguments("Halbfinale", FifaWorldCup2026KnockoutStage.Semifinal)]
    [Arguments("Spiel um Platz 3", FifaWorldCup2026KnockoutStage.ThirdPlacePlayoff)]
    [Arguments("Spiel um den 3. Platz", FifaWorldCup2026KnockoutStage.ThirdPlacePlayoff)]
    [Arguments("Finale", FifaWorldCup2026KnockoutStage.Final)]
    public async Task Getting_world_cup_open_predictions_maps_knockout_round(
        string kicktippRoundName,
        FifaWorldCup2026KnockoutStage expectedStage)
    {
        StubHtmlResponse("/test-community/tippabgabe", CreateKnockoutTippabgabe(kicktippRoundName, includeMarker: true));
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("test-community", CompetitionIds.FifaWorldCup2026);

        await Assert.That(matches).HasCount().EqualTo(1);
        var data = matches[0].CompetitionSpecificData as FifaWorldCup2026MatchData;
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.KicktippRoundName).IsEqualTo(kicktippRoundName);
        await Assert.That(data.Stage).IsEqualTo(expectedStage);
        await Assert.That(data.ResultBasis)
            .IsEqualTo(FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout);
    }

    [Test]
    public async Task Getting_non_world_cup_open_predictions_retains_source_round_and_penalty_basis_without_guessing_a_subcompetition()
    {
        StubHtmlResponse("/test-community/tippabgabe", CreateKnockoutTippabgabe("DFB-Pokal 2026/27", includeMarker: true));
        var client = CreateClient();

        var match = (await client.GetOpenPredictionsAsync("test-community", CompetitionIds.Bundesliga2026_27)).Single();

        await Assert.That(match.CompetitionSpecificData).IsNull();
        await Assert.That(match.KicktippFixtureId).IsNull();
        await Assert.That(match.KicktippRoundName).IsEqualTo("DFB-Pokal 2026/27");
        await Assert.That(match.ResultBasis).IsEqualTo(ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout);
        await Assert.That(match.BundesligaSeasonSubcompetition).IsNull();
    }

    [Test]
    public async Task Getting_world_cup_open_predictions_allows_unknown_round_with_penalty_marker()
    {
        StubHtmlResponse("/test-community/tippabgabe", CreateKnockoutTippabgabe("Neue K.-o.-Runde", includeMarker: true));
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("test-community", CompetitionIds.FifaWorldCup2026);

        var data = matches.Single().CompetitionSpecificData as FifaWorldCup2026MatchData;
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.KicktippRoundName).IsEqualTo("Neue K.-o.-Runde");
        await Assert.That(data.Stage).IsEqualTo(FifaWorldCup2026KnockoutStage.Unknown);
    }

    [Test]
    public async Task Getting_world_cup_open_predictions_uses_known_round_without_penalty_marker()
    {
        StubHtmlResponse("/test-community/tippabgabe", CreateKnockoutTippabgabe("Sechzehntelfinale", includeMarker: false));
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("test-community", CompetitionIds.FifaWorldCup2026);

        await Assert.That(matches).HasCount().EqualTo(1);
        var data = matches.Single().CompetitionSpecificData as FifaWorldCup2026MatchData;
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Stage).IsEqualTo(FifaWorldCup2026KnockoutStage.RoundOf32);
        await Assert.That(data.ResultBasis)
            .IsEqualTo(FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout);
    }

    [Test]
    public async Task Getting_world_cup_open_predictions_distinguishes_matches_in_shared_finale_round()
    {
        var html = """
            <!DOCTYPE html><html><body>
            <input type="hidden" name="spieltagIndex" value="15" />
            <div class="spieltagsauswahl"><div class="prevnextTitle"><a>Finale</a></div></div>
            <table id="tippabgabeSpiele"><tbody>
                <tr><td>18.07.26 23:00</td><td>France</td><td>England</td><td>
                    <span class="kicktipp-spielabschnitt-markierung">n.E.</span>
                    <input type="text" /><input type="text" />
                </td></tr>
                <tr><td>19.07.26 21:00</td><td>Spain</td><td>Argentina</td><td>
                    <span class="kicktipp-spielabschnitt-markierung">n.E.</span>
                    <input type="text" /><input type="text" />
                </td></tr>
            </tbody></table>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("test-community", CompetitionIds.FifaWorldCup2026);

        var thirdPlaceData = matches.Single(match => match.HomeTeam == "France").CompetitionSpecificData
            as FifaWorldCup2026MatchData;
        var finalData = matches.Single(match => match.HomeTeam == "Spain").CompetitionSpecificData
            as FifaWorldCup2026MatchData;
        await Assert.That(thirdPlaceData!.Stage).IsEqualTo(FifaWorldCup2026KnockoutStage.ThirdPlacePlayoff);
        await Assert.That(finalData!.Stage).IsEqualTo(FifaWorldCup2026KnockoutStage.Final);
    }

    [Test]
    public async Task Getting_generic_open_predictions_does_not_add_world_cup_data()
    {
        StubHtmlResponse("/test-community/tippabgabe", CreateKnockoutTippabgabe("Sechzehntelfinale", includeMarker: true));
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("test-community");

        await Assert.That(matches.Single().CompetitionSpecificData).IsNull();
    }

    [Test]
    public async Task Getting_schadensfresse_Bundesliga_open_predictions_joins_both_canonical_fixture_IDs_from_sanitized_outcome_details()
    {
        StubSchadensfresseFixtureIdentitySurfaces();
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("schadensfresse", CompetitionIds.Bundesliga2026_27);

        await Assert.That(matches.Select(match => match.KicktippFixtureId)).IsEquivalentTo(["1662323362", "1662323366"]);
        foreach (var match in matches)
        {
            await Assert.That(match.KicktippRoundName).IsEqualTo("1. Spieltag");
            await Assert.That(match.BundesligaSeasonSubcompetition).IsEqualTo(BundesligaSeasonSubcompetition.Bundesliga);
            await Assert.That(match.ResultBasis).IsEqualTo(ResultBasis.RegularTime90Minutes);
        }
        await Assert.That(GetRequestsForPath("/schadensfresse/tippabgabe")).Count().IsEqualTo(1);
        await Assert.That(GetRequestsForPath("/schadensfresse/tippuebersicht")).Count().IsEqualTo(1);
        await Assert.That(GetRequestsForPath("/schadensfresse/tippuebersicht/spiel")).Count().IsEqualTo(2);
    }

    [Test]
    [Arguments("missing-id")]
    [Arguments("duplicate-id")]
    [Arguments("missing-label")]
    [Arguments("duplicate-label")]
    [Arguments("wrong-label")]
    [Arguments("wrong-competition")]
    [Arguments("wrong-round")]
    [Arguments("detail-id-mismatch")]
    [Arguments("status")]
    [Arguments("login")]
    [Arguments("missing-table")]
    [Arguments("extra-source-query")]
    [Arguments("duplicate-source-query")]
    [Arguments("missing-termin")]
    [Arguments("duplicate-termin")]
    [Arguments("unknown-structured-field")]
    [Arguments("outcome-open-ambiguity")]
    [Arguments("form-conflict")]
    [Arguments("unknown-seed")]
    [Arguments("inference-only")]
    public async Task Getting_schadensfresse_Bundesliga_open_predictions_fails_visibly_before_returning_a_partial_typed_set(string mutation)
    {
        StubSchadensfresseFixtureIdentitySurfaces(mutation);
        var client = CreateClient();

        await Assert.That(async () => await client.GetOpenPredictionsAsync("schadensfresse", CompetitionIds.Bundesliga2026_27))
            .Throws<KicktippFixtureIdentityException>();
    }

    [Test]
    public async Task Getting_schadensfresse_Bundesliga_open_predictions_returns_empty_only_for_a_present_authenticated_table_without_betting_controls()
    {
        StubSchadensfresseFixtureIdentitySurfaces("true-empty");
        var client = CreateClient();

        var matches = await client.GetOpenPredictionsAsync("schadensfresse", CompetitionIds.Bundesliga2026_27);

        await Assert.That(matches).IsEmpty();
        await Assert.That(GetRequestsForPath("/schadensfresse/tippuebersicht")).IsEmpty();
    }

    [Test]
    public async Task Getting_schadensfresse_Bundesliga_open_predictions_propagates_a_cancelled_request()
    {
        StubSchadensfresseFixtureIdentitySurfaces();
        var client = CreateClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(async () => await client.GetOpenPredictionsAsync(
                "schadensfresse", CompetitionIds.Bundesliga2026_27, cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [Arguments("https://www.kicktipp.de/schadensfresse/tippabgabe", true)]
    [Arguments("http://www.kicktipp.de/schadensfresse/tippabgabe", false)]
    [Arguments("https://kicktipp.de/schadensfresse/tippabgabe", false)]
    [Arguments("https://user@www.kicktipp.de/schadensfresse/tippabgabe", false)]
    public async Task Schadensfresse_fixture_authority_requires_the_canonical_production_origin(string value, bool expected)
    {
        await Assert.That(KicktippClient.IsCanonicalKicktippAuthority(new Uri(value))).IsEqualTo(expected);
    }

    private void StubSchadensfresseFixtureIdentitySurfaces(string? mutation = null)
    {
        const string firstId = "1662323362";
        const string secondId = "1662323366";
        if (mutation == "status")
        {
            StubStatusCode("/schadensfresse/tippabgabe", 500);
            return;
        }
        if (mutation == "login")
        {
            StubHtmlResponse("/schadensfresse/tippabgabe", "<html><head><title>Login</title></head><body><form id=\"loginFormular\"></form></body></html>");
            return;
        }
        if (mutation == "missing-table")
        {
            StubHtmlResponse("/schadensfresse/tippabgabe", "<html><head><title>Kicktipp</title></head><body></body></html>");
            return;
        }

        var firstOutcomeId = mutation is "missing-id" or "unknown-seed" ? "unknown-id" : firstId;
        var secondOutcomeId = mutation == "duplicate-id" ? firstOutcomeId : secondId;
        var firstFormId = mutation is "detail-id-mismatch" or "unknown-seed" ? firstOutcomeId : firstId;
        var secondFormId = mutation == "form-conflict" ? firstId : secondId;
        var outcomeRows = OutcomeRow(firstOutcomeId, "30.08.26 15:30", "SC Freiburg", "Werder Bremen") +
            OutcomeRow(secondOutcomeId, "30.08.26 17:30", "FC Augsburg", "FC Schalke 04");
        if (mutation == "missing-id") outcomeRows = outcomeRows.Replace("data-url=\"/schadensfresse/tippuebersicht/spiel?tippsaisonId=5746822&amp;spieltagIndex=1&amp;tippspielId=unknown-id\"", "", StringComparison.Ordinal);
        if (mutation == "outcome-open-ambiguity") outcomeRows += OutcomeRow(firstOutcomeId, "30.08.26 15:30", "SC Freiburg", "Werder Bremen");
        if (mutation == "inference-only") outcomeRows = string.Empty;
        if (mutation == "extra-source-query") outcomeRows = outcomeRows.Replace($"tippspielId={firstId}", $"tippspielId={firstId}&amp;unexpected=1", StringComparison.Ordinal);
        if (mutation == "duplicate-source-query") outcomeRows = outcomeRows.Replace($"tippspielId={firstId}", $"tippspielId={firstId}&amp;tippspielId={firstId}", StringComparison.Ordinal);

        var tippabgabe = $$"""
            <html><head><title>Kicktipp</title></head><body><input name="spieltagIndex" value="1" />
            <table id="tippabgabeSpiele"><tbody>
              <tr><td>30.08.26 15:30</td><td>SC Freiburg</td><td>Werder Bremen</td><td><input type="text" name="spieltippForms[{{firstFormId}}][heim]" /><input type="text" name="spieltippForms[{{firstFormId}}][gast]" /></td></tr>
              <tr><td>30.08.26 17:30</td><td>FC Augsburg</td><td>FC Schalke 04</td><td><input type="text" name="spieltippForms[{{secondFormId}}][heim]" /><input type="text" name="spieltippForms[{{secondFormId}}][gast]" /></td></tr>
            </tbody></table></body></html>
            """;
        if (mutation == "true-empty")
        {
            tippabgabe = tippabgabe.Replace("<input type=\"text\"", "<input type=\"hidden\"", StringComparison.Ordinal);
        }
        var outcomes = $"<html><head><title>Kicktipp</title></head><body><table id=\"spielplanSpiele\"><tbody>{outcomeRows}</tbody></table></body></html>";
        StubHtmlResponse("/schadensfresse/tippabgabe", tippabgabe);
        StubHtmlResponseWithParams("/schadensfresse/tippuebersicht", outcomes, ("spieltagIndex", "1"));
        if (mutation is "true-empty" or "inference-only" or "unknown-seed" or "missing-id" or "duplicate-id" or "outcome-open-ambiguity" or "form-conflict" or "extra-source-query" or "duplicate-source-query") return;

        var firstDetail = Detail("1. Bundesliga 2026/27", "1. Spieltag", "30.08.26 15:30");
        if (mutation == "missing-label") firstDetail = firstDetail.Replace("<span class=\"spieldaten-infos-label\">Wettbewerb</span><span class=\"spieldaten-infos-value\">1. Bundesliga 2026/27</span>", "", StringComparison.Ordinal);
        if (mutation == "duplicate-label") firstDetail = firstDetail.Replace("</body>", "<span class=\"spieldaten-infos-label\">Wettbewerb</span><span class=\"spieldaten-infos-value\">1. Bundesliga 2026/27</span></body>", StringComparison.Ordinal);
        if (mutation == "wrong-label") firstDetail = firstDetail.Replace("Spieltag</span>", "Spielrunde</span>", StringComparison.Ordinal);
        if (mutation == "wrong-competition") firstDetail = Detail("Bundesliga", "1. Spieltag", "30.08.26 15:30");
        if (mutation == "wrong-round") firstDetail = Detail("1. Bundesliga 2026/27", "1. Runde", "30.08.26 15:30");
        if (mutation == "missing-termin") firstDetail = firstDetail.Replace("<div><span class=\"spieldaten-infos-label\">Termin</span><span class=\"spieldaten-infos-value\">30.08.26 15:30</span></div>", "", StringComparison.Ordinal);
        if (mutation == "duplicate-termin") firstDetail = firstDetail.Replace("</body>", "<div><span class=\"spieldaten-infos-label\">Termin</span><span class=\"spieldaten-infos-value\">30.08.26 15:30</span></div></body>", StringComparison.Ordinal);
        if (mutation == "unknown-structured-field") firstDetail = firstDetail.Replace("</body>", "<div><span class=\"spieldaten-infos-label\">Unbekannt</span><span class=\"spieldaten-infos-value\">x</span></div></body>", StringComparison.Ordinal);
        if (mutation == "detail-id-mismatch")
        {
            Server
                .Given(WireMock.RequestBuilders.Request.Create()
                    .WithPath("/schadensfresse/tippuebersicht/spiel")
                    .WithParam("tippsaisonId", new WireMock.Matchers.ExactMatcher("5746822"))
                    .WithParam("spieltagIndex", new WireMock.Matchers.ExactMatcher("1"))
                    .WithParam("tippspielId", new WireMock.Matchers.ExactMatcher(firstId))
                    .UsingGet())
                .RespondWith(WireMock.ResponseBuilders.Response.Create()
                    .WithStatusCode(302)
                    .WithHeader("Location", $"{ServerUrl}/schadensfresse/tippuebersicht/spiel?tippsaisonId=5746822&spieltagIndex=1&tippspielId={secondId}"));
        }
        else
        {
            StubHtmlResponseWithParams("/schadensfresse/tippuebersicht/spiel", firstDetail, ("tippsaisonId", "5746822"), ("spieltagIndex", "1"), ("tippspielId", firstId));
        }
        StubHtmlResponseWithParams("/schadensfresse/tippuebersicht/spiel", Detail("1. Bundesliga 2026/27", "1. Spieltag", "30.08.26 17:30"), ("tippsaisonId", "5746822"), ("spieltagIndex", "1"), ("tippspielId", secondId));
    }

    private static string OutcomeRow(string id, string time, string home, string away) =>
        $"<tr class=\"clickable\" data-url=\"/schadensfresse/tippuebersicht/spiel?tippsaisonId=5746822&amp;spieltagIndex=1&amp;tippspielId={id}\"><td>{time}</td><td>{home}</td><td>{away}</td><td>-</td></tr>";

    private static string Detail(string competition, string round, string time) =>
        $"<html><head><title>Kicktipp</title></head><body><div><span class=\"spieldaten-infos-label\">Wettbewerb</span><span class=\"spieldaten-infos-value\">{competition}</span></div><div><span class=\"spieldaten-infos-label\">Spieltag</span><span class=\"spieldaten-infos-value\">{round}</span></div><div><span class=\"spieldaten-infos-label\">Termin</span><span class=\"spieldaten-infos-value\">{time}</span></div><div><span class=\"spieldaten-infos-label\">Tipptermin</span><span class=\"spieldaten-infos-value\">{time}</span></div></body></html>";

    private static string CreateKnockoutTippabgabe(string roundName, bool includeMarker)
    {
        var marker = includeMarker
            ? "<span class=\"kicktipp-spielabschnitt-markierung\">n.E.</span>"
            : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html><body>
            <input type="hidden" name="spieltagIndex" value="37" />
            <div class="spieltagsauswahl"><div class="prevnextTitle"><a>{{roundName}}</a></div></div>
            <table id="tippabgabeSpiele"><tbody><tr>
                <td>28.06.26 21:00</td><td>South Africa</td><td>Canada</td>
                <td>{{marker}}<input type="text" name="heim" /><input type="text" name="gast" /></td>
            </tr></tbody></table>
            </body></html>
            """;
    }
}
