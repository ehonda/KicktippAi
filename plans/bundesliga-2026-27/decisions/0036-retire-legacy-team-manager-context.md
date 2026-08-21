# ADR-0036: Retire legacy team and manager context from the live partition

- Status: Accepted
- Date: 2026-08-21

## Context

The historical `team-data` and `manager-data` KPI documents were manually uploaded, broadly selected by name, and lacked the source identity and freshness contract required by ADR-0007. Their apparently useful fields now overlap the accepted Bundesliga 2026/27 Club Elo and roster publications. Keeping parallel copies would let stale or subjective facts contradict the atomic publication heads.

The repository inventory found no remaining launch-required field that justifies a replacement collector. Squad size, average age, total and median market value are authoritative in `team-squad-summary`; an exact mean player value is derivable from its total value and valued-player count. Team identity and primary coach are authoritative in the per-club roster documents and roster coach rows. Subjective team assessment and coach age, country, and tenure have no accepted current source/freshness contract and are not launch inputs.

## Decision

The Bundesliga 2026/27 live context contract retires `team-data` and `manager-data` without replacement. It does not add another collector or prompt document. Match and bonus consumers use only their explicit profile-owned allowlists; the complete storage hygiene policy accounts for current match documents, targeted roster documents, the Club Elo aggregate, `team-squad-summary`, and publication support artifacts while classifying legacy, transfer, WM26, historical-season, invalid profile-owned, and unexpected names.

Profile-owned current-season documents cannot be changed through generic upload or copy utilities. The generic KPI upload command and its PowerShell document generator are removed. The remaining generic context upload and cross-community copy commands fail before writes when a Bundesliga 2026/27 target would mutate a profile-owned, retired, transfer, WM26, historical-season, or otherwise invalid owned name. Historical competition partitions retain their existing generic behavior.

A read-only inventory command reports the current partition's expected and observed document identities, versions, SHA-256 hashes, publication snapshot identities, source dates when a canonical publication provides them, an explicit Europe/Berlin freshness-evaluation date, and hygiene classifications. It never prints document content and has no delete, apply, or write mode. Remote deletion is not part of P0 launch and requires a separately reviewed inventory and decision.

Explicit competition is authoritative for catalog selection. If an exact known community maps to a different competition, resolution fails closed rather than allowing a community-name override to mix contracts. Valid match catalog ordering remains unchanged.

## Alternatives considered

- **Refresh the two manual documents:** Rejected because every launch-required objective fact is already authoritative elsewhere and the subjective fields lack an accepted source contract.
- **Add a smaller manager document:** Rejected because roster coach rows already provide the required coach identity.
- **Delete current or historical remote documents now:** Rejected because ADR-0007 requires inventory first and historical partitions are preserved.
- **Permit generic writes in dry-run mode:** Rejected because dry-run must validate the same mutation plan that a real execution would attempt.

## Consequences

- Live Bundesliga prompts have one authoritative source for squad, valuation, Elo, and coach facts.
- Operators can audit stale storage without exposing payloads or obtaining a mutation path.
- Existing remote legacy artifacts may remain stored, but explicit catalogs prevent their selection.
- The plan decision index and P0-15 task link are added by the integration owner because those files are shared across parallel lanes.

## Affected tasks

- P0-15 (integration owner adds the task link)

## Supersedes

None. This makes ADR-0007's hygiene requirement concrete and narrows older generic upload behavior only for the Bundesliga 2026/27 partition.
