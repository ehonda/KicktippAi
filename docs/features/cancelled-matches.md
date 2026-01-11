# Cancelled Matches Handling

This document describes how the prediction workflow handles cancelled matches ("Abgesagt" in German) on Kicktipp.

## Problem Statement

When a Bundesliga match is cancelled, Kicktipp displays "Abgesagt" in the time/date column instead of the scheduled time. This creates several challenges:

1. **Parsing Issue**: The HTML parser expects a date/time string (e.g., "10.01.26 15:30") but receives "Abgesagt"
2. **Database Key Consistency**: Predictions are stored with a composite key that includes `startsAt` - changing this value would break lookups and create duplicates
3. **Workflow Failures**: Without proper handling, the verify workflow fails because the match times don't match between Kicktipp and the database

## Key Observations

### Cancelled Matches Can Still Receive Predictions

**Important:** Kicktipp allows users to place predictions on cancelled matches. The betting input fields remain active even when the match shows "Abgesagt". This was verified manually on 2026-01-10.

### HTML Structure

Normal match:
```html
<tr>
    <td class="nw kicktipp-time">10.01.26 15:30</td>
    <td class="nw">FC St. Pauli</td>
    <td class="nw">RB Leipzig</td>
    <td class="kicktipp-tippabgabe">
        <input type="text" name="spieltippForms[...].heimTipp" />
        <input type="text" name="spieltippForms[...].gastTipp" />
    </td>
</tr>
```

Cancelled match:
```html
<tr>
    <td class="nw kicktipp-time">Abgesagt</td>
    <td class="nw">FC St. Pauli</td>
    <td class="nw">RB Leipzig</td>
    <td class="kicktipp-tippabgabe">
        <!-- Input fields still present! -->
        <input type="text" name="spieltippForms[...].heimTipp" value="1" />
        <input type="text" name="spieltippForms[...].gastTipp" value="2" />
    </td>
</tr>
```

## Design Decision: Process Cancelled Matches

We chose to **continue processing cancelled matches** rather than skipping them. Here's the rationale:

### Why Not Skip?

1. **Predictions are still accepted**: Users can place bets on Kicktipp, so we should too
2. **Rescheduling uncertainty**: When matches are rescheduled, we don't want to risk missing the new slot
3. **Existing predictions carry over**: Based on our understanding, when Kicktipp reschedules a match, existing predictions should remain in place

### Time Inheritance Strategy

To maintain database key consistency, cancelled matches **inherit the time from the previous match** in the table (same as empty time cells):

```
Row 1: "10.01.26 15:30" - Team A vs Team B  → startsAt = 15:30
Row 2: ""               - Team C vs Team D  → startsAt = 15:30 (inherited)
Row 3: "Abgesagt"       - Team E vs Team F  → startsAt = 15:30 (inherited), IsCancelled = true
Row 4: "10.01.26 18:30" - Team G vs Team H  → startsAt = 18:30
```

This ensures:
- Database lookups continue working for cancelled matches
- No duplicate predictions are created
- The `IsCancelled` flag provides explicit state tracking

#### Known Imprecision

The time inheritance approach has a known limitation: **it doesn't account for day boundaries or time slot changes**.

Example of incorrect assignment:
```
Saturday:
  Row 1: "10.01.26 15:30" - Match A (normal)
  Row 2: "10.01.26 18:30" - Match B (normal)

Sunday:
  Row 3: "Abgesagt"       - Match C → inherits 18:30 from Row 2 (WRONG - should be Sunday's time)
  Row 4: "11.01.26 15:30" - Match D (normal)
```

In this scenario, the cancelled match on Sunday incorrectly gets Saturday's 18:30 time slot, resulting in a non-existent configuration (two matches at 18:30 on Saturday).

**Why we accept this imprecision:**

1. **Schedule variability**: Bundesliga schedules are not consistent - some Sundays have 2 matches, some have 3. The last matchdays of the season often have unique schedules. Building correct logic for all cases would be complex.
2. **Low impact**: The worst case is an orphaned prediction when the match is rescheduled. Since we generate a new prediction with the correct time when that happens, there's no functional harm.
3. **Reprediction handles it**: When the match is rescheduled, the new time creates a "new" match in our system, triggering a fresh prediction with updated context.
4. **Rare occurrence**: Cancelled matches are uncommon events, and this specific edge case (first match of a day cancelled) is even rarer.

### Edge Case: First Match Cancelled

If the first match on a matchday is cancelled (no previous time to inherit), `ParseMatchDateTime` uses `DateTimeOffset.MinValue` as the fallback. This ensures:

1. **Database key consistency**: The same MinValue timestamp is always used, preventing orphaned predictions
2. **Reprediction cap integrity**: Using a varying timestamp (like `Now`) would create new predictions each run, bypassing reprediction limits
3. **Predictable behavior**: Tests and debugging are easier with a deterministic fallback

This is logged as a warning. In practice, this edge case is rare.

## Implementation Details

### Match Record

The `Match` record includes an `IsCancelled` property:

```csharp
public record Match(
    string HomeTeam,
    string AwayTeam,
    ZonedDateTime StartsAt,
    int Matchday,
    bool IsCancelled = false);
```

### Detection Logic

The `IsCancelledTimeText` method in `KicktippClient` performs case-insensitive detection:

```csharp
private static bool IsCancelledTimeText(string timeText)
{
    return string.Equals(timeText, "Abgesagt", StringComparison.OrdinalIgnoreCase);
}
```

### Affected Methods

The following methods handle cancelled matches:

| Method | File | Behavior |
|--------|------|----------|
| `GetOpenPredictionsAsync` | KicktippClient.cs | Inherits time, sets `IsCancelled = true` |
| `GetPlacedPredictionsAsync` | KicktippClient.cs | Inherits time, sets `IsCancelled = true` |
| `ExtractMatchWithHistoryFromSpielinfoPage` | KicktippClient.cs | Sets `IsCancelled = true`, uses `DateTime.Now` fallback |
| `ExecuteVerificationWorkflow` | VerifyMatchdayCommand.cs | Logs warning for cancelled matches |
| `ExecuteMatchdayWorkflow` | MatchdayCommand.cs | Logs warning for cancelled matches |

## Assumptions

The following assumptions underpin this design:

1. **Prediction persistence**: When Kicktipp reschedules a cancelled match to a new time, existing predictions remain attached to that match
2. **Unique team pairing per matchday**: A home-away team pairing is unique within a matchday (cannot have Team A vs Team B twice on matchday 16)
3. **Time inheritance is safe**: The inherited time doesn't affect Kicktipp's form submission - only team names and form field IDs matter
4. **Rescheduling creates new slot**: When rescheduled, the match will appear with the new time, triggering a fresh prediction workflow

## When Matches Are Rescheduled

When a cancelled match is rescheduled:

1. The match will appear on Kicktipp with the **new scheduled time**
2. Our workflow will treat this as a "new" match (different `startsAt` key)
3. A fresh prediction will be generated with updated context
4. The old prediction (with inherited time) becomes orphaned in the database

This is **acceptable behavior** because:
- We get to re-evaluate with current context (form changes, injuries, etc.)
- The old prediction served its purpose (having something in place just in case)
- Database storage is inexpensive

## Future Considerations

### Alternative: Match ID Lookup

If Kicktipp provides stable match IDs in the HTML (e.g., form field names like `spieltippForms[1384231932]`), we could potentially:
1. Extract and store the Kicktipp match ID
2. Use it as an alternative lookup key for cancelled/rescheduled matches
3. Enable prediction continuity across time changes

This would require:
- Schema changes to store Kicktipp match IDs
- Migration of existing predictions
- Changes to all repository query methods

Currently not implemented due to complexity, but worth revisiting if cancelled matches become more common.

### Monitoring

Consider adding metrics/alerts for:
- Number of cancelled matches encountered per workflow run
- Prediction mismatches specifically for cancelled matches
- Orphaned predictions (predictions for matches that were rescheduled)

## Testing

The following test scenarios cover cancelled match handling:

1. **Parsing**: Verify "Abgesagt" is detected and `IsCancelled = true` is set
2. **Time inheritance**: Verify cancelled matches use previous match's time
3. **Firebase compatibility**: Verify `DateTimeOffset.MinValue` can be stored (edge case)
4. **Workflow integration**: Verify predictions can be placed for cancelled matches

See test files:
- `KicktippClient_GetOpenPredictions_Tests.cs`
- `KicktippClient_GetPlacedPredictions_Tests.cs`
- `FirebasePredictionRepository_Match_Tests.cs`
