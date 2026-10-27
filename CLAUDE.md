# Writing code

These rules target known weaknesses of LLM-written code. IDs and grades refer to the dev-process skill references. Where this project has its own benchmarks, defect data or requirements, those take precedence over the rules below.

## Efficiency (correct LLM code is often several times slower than needed; PERF-C1, moderate, perishable)
- Before writing a loop over data that can grow, work out how its cost scales, and pick the data structure for the access pattern: hash/set for membership, sorted/indexed for ranges, contiguous array for scans (PERF-A1, moderate).
- Inside loops, look for re-scans of the same collection, linear lookups, repeated identical computations or queries, and multiple passes that could be one. Hoist, precompute or build a lookup once (PERF-A2, moderate).
- Don't compute, fetch, copy or render what won't be used (PERF-A3, moderate).
- Know the cost of the library/ORM/built-in calls in hot paths; prefer built-in or vectorised operations to hand-rolled loops (PERF-A4, moderate).
- State the expected input size and growth before choosing an approach. If unknown, assume it grows: paginate growable lists, index filtered/joined columns. For a one-off script on known small input, say so and keep it simple (PERF-A5, moderate).
- One call over many items, not many calls over one item: batch queries, network and disk I/O; no N+1 (PERF-A6, moderate).
- Keep blocking I/O and heavy work off UI, event-loop and request threads (PERF-A9, moderate in domain); avoid needless allocation and copies in hot paths (PERF-A10, moderate).
- Prefer structural fixes over engine-specific micro-tricks, which are unreliable across runtime versions (PERF-A8, weak on magnitudes).

## Size and complexity (AI-assisted repos show a persistent rise in warnings and complexity; AI-4, moderate–suggestive, perishable)
- Make the smallest change that does the job; don't rewrite or add abstractions the task doesn't need (MAINT §4, inference; PERF-A14, moderate).
- Don't introduce new linter or compiler warnings. Fix any you cause before finishing.

## Verification (QUAL-C2, inference)
- Your own reading of code you wrote is not evidence that it's correct (QUAL-A1, moderate).
- Before calling a change done, run the relevant tests and the configured linters or type checkers (QUAL-A6, weak, not causal). If none exist or they can't run, say so explicitly; never imply the change is verified.
- When code handles untrusted input or permissions, check input validation and access control explicitly (QUAL-A2, moderate; evidence on AI-code security is contested, QUAL-C1).
- Write tests that would fail on plausible faults and edge cases; don't write tests to raise coverage (QUAL-A3, moderate).

## When asked to optimise (agents optimise the wrong place and stop early; PERF-C2, moderate)
- Reproduce a realistic workload, get a line-level profile, look past the first hotspot, prefer structural fixes, re-run the tests, and confirm the speedup with repeated interleaved runs (PERF-C4, inference; PERF-A11–A13, moderate). Never optimise by self-review alone, without running the code.

## Don't assert
TDD or coverage targets as quality guarantees (QUAL-A3, QUAL-A4); "language X has fewer bugs" or static typing reduces bugs, unqualified (CODE-3, CODE-2); "premature optimisation" as a reason to accept a bad algorithm (PERF-A1).

# In-game testing

- Only one build can be in the game at a time. Deploy with `scripts/dev-plugin deploy <task-ref>`, and only when the user wants to test in-game; never copy builds into the dev folder by other means.
- If the deploy is refused, another checkout is being tested: tell the user who holds it (`scripts/dev-plugin status`) and wait. Never release a claim you don't hold.
- Run `scripts/dev-plugin release` only once the user says testing is finished. Clearing someone else's or a stale claim is the user's job; a hook blocks agents from `--force` and from touching the claim directly.
