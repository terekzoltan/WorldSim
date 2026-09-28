# E11-H Step 5c4 — bounded two-OFF-case measurement

Scope: a separate, locally closed evidence/tooling candidate, **not** E11-H acceptance. The once-only accepted-source `101/Goap/OFF` and `202/Simple/OFF` measurements stopped at their first post-tick herbivore zero, no later than tick 888. No case was rerun for closeout.

## Source, configuration, and private evidence

- Measurement checkout commit `8bf9e86d8f940e8abb71b24501737d4c002dfaa7`, tree `8061bfd5407ead80bf35f163ea5d38baca1f3557`; accepted Runtime base `db8d18a799f12fcf39dd593e7a42f6811663c21e` (tree `fc62cdd7885f6f6c3997486ba27213aee5771c03`), Runtime tree `54e2788ba61f2594c81d1314508916fe0c91b2af`, AI tree `08a3d9cd219574f1bc3925a8467f94b4d6274060`, Tech blob `237c90380d803259cad0997302f489c8107cfa1f`, diagnostic-test blob `dcc163272d77c950cca860b21c20b8022055caec`.
- `64×40`, 24 initial humans, `dtSeconds=0.25`, `global_planner`; combat ON, diplomacy OFF, siege ON, stone buildings OFF, predator-human attacks OFF for both keys; birth-rate and movement multipliers 1; emergency rescue disabled; no replenishment or regrowth overrides; cap 888 ticks. Keys in order: `101/goap/off`, `202/simple/off`.
- The raw `off-deficit-v1.json` and `manifest.json` remain **private and local-only** under `.artifacts/smr/wave11-e11-h-step5c4-off-deficit-measurement-001/`. The raw is **367457 bytes**, SHA-256 `3509a9c254768e3dba824f617fc635d7a69de84f39f9b5b83d237127fc054109`; readback matches the private manifest. The files are Git-ignored and are **not published or committed**. Retrieval requires access to that retained local directory; this README alone cannot reconstruct the per-tick series.

| Fixed OFF case | First one-tick H decline | First below initial 8 H | Persistent below 8 through first zero | At first H zero: H/P | H births / successful-capture proxy / H starvation / unclassified H mortality |
|---|---:|---:|---|---|---|
| `101/goap/off` | 2 | 511 | 639–780 | `0/13`, tick 780 | `116 / 120 / 3 / 1` |
| `202/simple/off` | 13 | 774 | 774–888 | `0/10`, tick 888 | `116 / 121 / 2 / 1` |

Samples include tick zero: 781 and 889 respectively. Earlier below-eight intervals in the first case recovered before the persistent interval. `MeatFromHunt` is a successful-capture **proxy**, not a spatial encounter or predator-attempt record; zero replenishment was observed. The previous [accepted diagnostic](../wave11-e11-h-step5c4-recruitment-mortality-diagnostic/README.md) ran the same fixed OFF identities to tick 1200: both ended with **2 predators** and zero predator-extinction ticks, despite recorded predator starvation events.

## Owner App comparison and decision boundary

On 2026-09-28 the Owner reported earlier graphical App worlds in which herbivores multiplied and clustered, predators spread toward them as prey thinned, and predators apparently disappeared later. Crowding, scarce local food, predation and eventual prey shortage are a **leading plausible explanation**, not an unexplained event. The fixed OFF series independently supports capture-dominated herbivore decline, but does **not** prove spatial arrival, local food causality, individual birth eligibility, or the App predators' causes of death. The measured predators had **not** disappeared at first herbivore zero or by tick 1200 in the separate diagnostic.

The App process's launch binary/HEAD, effective environment, seeds and ticks were not captured. Suggested App settings and the code's `128×128`, 25-human defaults do not establish comparability with these `64×40`, 24-human fixed runs. Historical executed-binary-to-source binding of the measurement is also unproven. The pre-existing untracked Owner observation note and chat screenshots are not part of this committed packet.

Meta's bounded classification: **no specific new technical defect is demonstrated by this comparison**; gameplay balance or a human influence remains an Owner decision. The focused lifecycle gate remains **RED/open**; this package does not authorize Runtime/AI repair, an expanded matrix, E11-H whole-epic closeout, or E11-I/J.

## Acceptance and verification

Frozen candidate `op-a5fe33b2-9924-4e4b-82ef-578960a9c0ff`; final Meta GREEN synthesis `op-3bd181d7-091c-40cf-b8c7-36d0329c3713`; accountable SMR Diagnostic Delivery ACK_ONLY `op-87999ecc-d0f3-4d48-af24-d9eba9752b53`. Owner App read-only comparison `op-06716b9b-5fdc-4d80-9b80-6707f1bc88b3` and Meta scoped closeout disposition `op-a155138b-3b92-49b9-a95c-9f37f37981cb` distinguish accepted measurements from qualitative observations. Owner revision `db550684af11b8302aad4df49228e51387542e7fe7a49d997db3d6cfcd235ec9` grants **local commit only**.

The reviewed delivery reported a warning-as-error Release build (0 warnings/errors), focused **13/13 non-simulation tests**, and default `NOT_RUN`. The standalone test project is not in the ordinary solution/cheap CI filter; local focused verification is the closeout proof, **not** remote CI. If publication/CI wiring is separately authorized, SMR Diagnostic Delivery and Meta must revisit that CI companion before treating a remote check as evidence. No simulation or paid/expanded run is required for this closeout.
