# README editorial brief

This document records the editorial decisions behind the August 2026 rewrite of the project README. It was distilled from an interview with the project owner and a repository-wide implementation review. Future README updates should begin here, then verify every time-sensitive claim against the current repository state.

The brief is internal guidance. It should not be linked from the public README or copied into it wholesale.

## What the README is for

The primary reader is a technically curious visitor encountering this personal hobby project, not a prospective contributor looking for setup instructions. The README should help that reader understand:

- what KicktippAi does;
- why the project exists;
- how autonomous prediction operations work at a high level;
- how experiments provide a separate way to investigate models and make evidence-informed decisions;
- how the project has evolved alongside LLMs, supporting tools, and coding agents; and
- where to go for deeper technical, experimental, or historical material.

The desired reaction is: “This is an interesting real-world laboratory for autonomous AI systems,” followed by enough architectural understanding to explore further.

## Narrative hierarchy

Lead with the concrete hook: an LLM participates autonomously in real Kicktipp football prediction communities. Follow with the motivation: KicktippAi is a hobby project for learning what can be built with AI-powered systems and for observing the technology's evolution through a system that genuinely runs over time.

The two main capabilities should then be explained without forcing them into one tightly coupled loop:

1. **Autonomous operations** collect context, generate or reuse predictions, persist them, submit them to Kicktipp, and verify the result. This live path has run for multiple competitions.
2. **Experiments on demand** replay historical fixtures under controlled configurations. They are used when a production choice needs evidence or when a question is interesting independently. Experiments can inform future work, but they are not part of every live prediction cycle and need not produce an immediate operational change.

After those foundations, show the current competition chapter, the project's progression across competitions, and the increasing role of coding agents in building it.

## Motivation and positioning

The project began, and remains, a personal hobby project. Its value is not a claim to superior football forecasting or a reusable commercial product. It offers a concrete way to:

- learn how to design and operate AI-powered systems;
- compare changing model capabilities on a persistent real-world task;
- adopt and understand tooling such as Langfuse through actual use;
- investigate questions such as model choice, knowledge cutoffs, reasoning effort, and the value of different context inputs; and
- observe how coding agents change software development itself.

KicktippAi is tailored to its own communities and research questions. Do not present it as a turnkey prediction service, a setup tutorial, or proof that one model is universally “best.”

## Factual guardrails

Preserve these distinctions in future wording:

- Prediction operations were autonomous from the outset. The owner did not manually trigger routine production runs or post predictions. Do not describe the project's evolution as manual prediction operations becoming automated.
- What became substantially more autonomous was **development**: coding agents progressed from assisting with implementation to carrying planned work from requirements and specifications through research, implementation, review, validation, and onboarding.
- That development autonomy remains bounded. The owner supplies direction and ideas, makes consequential product and model decisions, authorizes spending, controls credentials, and handles external gates.
- `ehonda-ai-arena` existed in the first Bundesliga chapter. Do not imply that it was introduced for the World Cup or a later season.
- Live operations and statistical experimentation are related capabilities, not a mandatory closed feedback loop.
- Experimental results should be described with their scope and caveats. Distinguish descriptive rankings from statistically supported differences, and link to full evidence where possible.
- Use **prediction** consistently. Avoid “tip” as an English shorthand and avoid betting language.

## Competition story

The competition history is useful because it makes the system's evolution tangible:

- **Bundesliga 2025/26** was the first operational chapter, including autonomous communities and the AI arena.
- **FIFA World Cup 2026** required tournament-specific adaptation, including ranking and lineup context.
- **Bundesliga 2026/27** brought lessons back into a domestic competition with richer context, explicit provenance, reproducible model selection, and a requirements-led onboarding program implemented with substantial coding-agent autonomy.

Treat every chapter as a learning checkpoint. Current context inputs—such as club Elo ratings, rosters, FIFA rankings, lineups, standings, recent results, or head-to-head history—are examples of the system at a point in time, not a final recipe. The README should say that the system is continually adjusted as new needs and evidence emerge.

## Tone, authorship, and transparency

Use an accessible, technically literate, neutral voice with some personality. Avoid first-person statements attributed to the owner when the prose was drafted by an agent. At the beginning of the README, disclose that its motivation and priorities came from an owner interview and that the final text was drafted with Codex. The disclosure belongs at the start so readers never form a false impression of authorship.

Prefer concrete descriptions over marketing language. It is fine for the title and opening hook to feel playful; the body should stay precise about evidence and autonomy boundaries.

## Architecture and level of detail

Use small Mermaid diagrams because GitHub renders them natively and the relationships are easier to understand visually. Two diagrams are preferable to one crowded diagram:

- a left-to-right operational view centered on KicktippAi and its interactions with GitHub Actions, Kicktipp, OpenAI, Firestore, and Langfuse; and
- a vertical experiment flow from historical outcomes and stored context through replayed variants and Langfuse runs to statistical reports.

Keep technical explanations at system level. Useful details include competition-specific context, structured predictions, caching and selective reprediction, immutable provenance, submission verification, tracing, and comparable experiment runs. Link to deeper documents instead of embedding setup commands, environment variables, schedules, workflow matrices, project trees, test instructions, contribution guidance, or detailed cost-optimization procedures.

As a rough editorial target, keep the README around 900–1,200 words, with no badges and only a short license section.

## Time-sensitive snapshot

As of the August 2026 interview, the active chapter was Bundesliga 2026/27 and its selected production configuration was `gpt-5.6-sol` with `xhigh` reasoning. That decision was informed by a reproducible production-candidate experiment, but the result did not justify an unqualified claim of statistical superiority. The README therefore linked the full report and described the choice as evidence-informed.

This paragraph is a historical snapshot, not standing permission to repeat those facts. Before every README update, verify the active competitions, production configuration, context sources, report links, operational behavior, and coding-agent workflow from current repository evidence.

## Update checklist

Before changing the README:

1. Read this brief and the current README.
2. Scan current application code, workflows, configuration, accepted decisions, and published reports for factual changes.
3. Separate durable project motivation from the current operational snapshot.
4. Preserve the autonomy distinctions and experiment caveats above.
5. Keep the public narrative visitor-focused; move implementation procedures to dedicated docs.
6. Render both Mermaid diagrams at desktop and narrow widths and simplify them if labels or edges become crowded.
7. Verify every local and external link.
8. Check that the opening authorship note remains accurate if the drafting process changes.
