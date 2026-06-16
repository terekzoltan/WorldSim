# Swarm Assistant Runbook

> Ez a dokumentum a WorldSim projekt Swarm Assistant review / gate / evidence futtatasi szabalyait rogziti.
> Nem Meta Coordinator runbook: a Meta tovabbra is a sequencing es routing tulajdonosa, mig ez a runbook a Swarm Assistant gate-dispatch es review-output viselkedeset standardizalja.

---

## Scope

- Swarm Assistant review csomagok.
- Step-review / deep-review kulso gate futtatasok.
- Evidence, test, lint, security, drift, hallucination, SME vagy council jellegu gate-ek dispatch sorrendje.
- Review outputok, ahol egy gate futott, nem futott, vagy nem volt ertelmesen alkalmazhato.

Nem scope:
- Meta Coordinator sequencing ownership.
- Track B/C/A/D implementation planning.
- Raw `.artifacts` baseline promotion.
- `ops/PROJECT_STATE.md` ownership.

---

## Dispatch Policy

- Run independent gates in parallel when that reduces turnaround without losing clarity.
- Run gates sequentially when one gate's output materially affects the next gate's scope or interpretation.
- If a gate is enabled but not meaningfully applicable to the target, say so explicitly in the review output instead of silently skipping it.

## Practical Interpretation

Parallelize when:
- lint, focused tests, static checks, and docs/evidence reads do not depend on each other;
- independent reviewer/SME/test-engineer lanes can inspect the same frozen artifact set;
- the output of one gate is unlikely to change the file scope or evidence question of another gate.

Run sequentially when:
- a failed preflight changes what should be reviewed;
- a gate determines whether later expensive SMR packages are necessary;
- a security or drift finding may invalidate the evidence package;
- a diagnostic gate is needed before behavior or acceptance can be interpreted;
- an early-stop policy exists, such as Step10B.5 hostile/manual decision-core before pure/stress expansion.

Explicitly mark not-applicable gates when:
- frontend/manual visual gates are requested for runtime-only changes;
- mutation/security/schema gates are enabled but no relevant executable/testable surface changed;
- council/SME review is configured but the current target has no material domain-specific question;
- a full SMR matrix is intentionally skipped by staged/early-stop policy;
- a gate would only duplicate a stronger already-run gate without adding evidence.

## Review Output Requirements

Every Swarm Assistant review output should include:
- which gates ran,
- which gates were intentionally skipped,
- why skipped gates were not meaningfully applicable,
- whether gates ran in parallel or sequence when this affects interpretation,
- any early-stop decision and the evidence that justified stopping,
- residual risks from skipped or narrowed gates.

Do not silently omit enabled gates. If a gate is not useful for the target, say that directly and explain the reasoning in one concise line.

## Relationship To Meta Coordinator

- Meta Coordinator owns whether a step is green-lit, deferred, split, or routed to another Track.
- Swarm Assistant owns clear gate execution/reporting within the scope Meta handed off.
- If gate applicability is ambiguous, Swarm Assistant should report the ambiguity instead of inventing a new workflow rule.
- If a gate failure changes ownership or sequencing, Swarm Assistant should recommend routing; Meta decides.
