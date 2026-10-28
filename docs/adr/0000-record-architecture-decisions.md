# 0000. Record architecture decisions

- Status: accepted
- Date: 2026-10-28

## Context

subgate's design rests on measurements that go stale: Anthropic's terms, the shape of the usage data its clients receive, the plan's meter. Later readers need the reason and the evidence, not just the outcome.

## Decision

Record each decision as a numbered Markdown file in `docs/adr/`, from `template.md`. Each ADR states the decision, the options, the evidence (a spike result, a measurement, or an explicit assumption) and the condition under which to reopen it. Superseded ADRs stay, with a pointer to their successor.

## Consequences

Evidence from the author's private environment is summarised in generic terms; raw data, hostnames, key names and usage numbers stay outside this repository.

## Reopen if

—
