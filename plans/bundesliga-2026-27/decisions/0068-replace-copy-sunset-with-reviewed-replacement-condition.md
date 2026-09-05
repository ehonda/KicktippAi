# ADR-0068: Replace the copy calendar sunset with a reviewed replacement condition

- Status: Accepted
- Date: 2026-09-05

## Context

ADR-0062 used an absolute calendar sunset for the temporary Schadensfresse copy route. The recovery remains the current reviewed operational topology, while the target-primary replacement is still an independently reviewed P1-10 milestone. A calendar expiry would force a quarantine even when no accepted replacement is available.

## Decision

The temporary copy route remains in place until a reviewed successor explicitly replaces or terminates it. The successor must name the replacement topology, rollback and recovery owner, exact integrated revision, and required green validation before retirement. This ADR changes no workflow, cron, community, credential, model, prompt, or posting behavior.

## Alternatives considered

- **Keep the calendar sunset:** Rejected because time alone does not prove a safe replacement exists.
- **Silently extend the copy route:** Rejected because an operational continuation needs a durable reviewed condition.

## Consequences

- P1-10 remains the final replacement owner, but it is no longer deadline-critical solely because of the former calendar date.
- Existing ADR-0062 rollback, no-bonus, manual-only, and recovery-owner boundaries remain in force until a reviewed successor changes them.

## Affected tasks

- [P1-10](../tasks/p1-10-schadensfresse-primary-community.md)
- [P1 recovery execution packet](../p1-execution-packet.md)
- [Bundesliga execution strategy](../execution-strategy.md)

## Supersedes

[ADR-0062](0062-temporarily-restore-schadensfresse-copy.md) only for its `2026-09-08T12:00:00Z` calendar-sunset and missed-sunset re-quarantine condition. Its current temporary runtime topology and all other authority boundaries remain accepted.
