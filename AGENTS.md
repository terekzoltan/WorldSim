# WorldSim AGENTS Guide

Ez a dokumentum a projekt tudatos szetvalasztasi terve: kulon fejlesztheto komponensek, tiszta hatarok, es parhuzamos munkaszervezes (Track A-D).

## Cel

- A jelenlegi `WorldSim` monolit felelossegeinek szetvalasztasa kulon projektekre.
- Stabil boundary-k letrehozasa a simulation runtime, a grafika/UI, es a Java refinery service kozott.
- PArhuzamos fejlesztes tamogatasa ugy, hogy a csapatok minimalisan zavarjak egymast.

## Tervezett celstruktura

Root szinten (solution alatt):

```text
WorldSim.sln
AGENTS.md

WorldSim.App/                 # MonoGame host (exe), minimalis wiring
WorldSim.Graphics/            # Render + camera + HUD + content mapping
WorldSim.Runtime/             # Sim update loop + domain orchestracio
WorldSim.AI/                  # Utility/GOAP/HTN es NPC decision modul
WorldSim.Contracts/           # KozoS DTO/Schema C# oldalon (Java parityhoz)
WorldSim.RefineryAdapter/     # Java patch -> runtime command mapping
WorldSim.RefineryClient/      # HTTP client + patch parser/applier (mar megvan)
WorldSim.RefineryClient.Tests/
refinery-service-java/        # Kulon Java service (mar megvan)

Tech/                         # Data (technologies.json)
```

## Modulleiras es felelossegek

### 1) WorldSim.App

- MonoGame `Game` host (`Program`, `GameHost`).
- Input routing commandokra.
- Osszekoti a `Runtime` + `Graphics` + `RefineryAdapter` retegeket.
- Nem tartalmaz domain logikat.

### 2) WorldSim.Graphics

- Kamera (`Camera2D`), render pipeline (`WorldRenderer`, `HudRenderer`, stb.).
- Asset katalogus (`TextureCatalog`) + faction/resource ikon mapping.
- Csak read-only snapshotot fogyaszt (`WorldRenderSnapshot`), nem nyul domain modellekhez.

### 3) WorldSim.Runtime

- Vilagmodell, tick update, ecology/economy/tech alkalmazasa.
- Domain command endpoint: pl. `TriggerRefineryPatch`, `UnlockTech`, `ToggleOverlay`.
- Kiad egy immutable/read-only snapshotot a graphics szamara.
- Nincs MonoGame fuggoseg.

### 4) WorldSim.AI

- NPC goal selection/planning (Utility, GOAP, kesobb HTN).
- Cserelheto planner/brain interfeszek (`INpcBrain`, `IPlanner`, stb.).
- Runtime csak interfeszen at hivja.

### 5) WorldSim.Contracts

- KozoS contract tipusok + verziokezeles (`v1`, kesobb `v2`).
- Java/C# kozt a schema egyetlen forrasa.
- Contract validation policy (strict/lenient) deklaracioja.

### 6) WorldSim.RefineryAdapter

- Anti-corruption layer a Java patch contract es a Runtime commandok kozott.
- Felelos a tech ID mappingert es op-level validaciokert.
- Runtime oldalon ne legyen Java-specifikus branching.

### 7) refinery-service-java

- Planner pipeline (`Mock/Llm/Refinery`) HTTP API-n keresztul.
- A C# oldallal csak contracton keresztul beszel.

## Dependency graph (engedelyezett iranyok)

```text
WorldSim.App
  -> WorldSim.Graphics
  -> WorldSim.Runtime
  -> WorldSim.RefineryAdapter

WorldSim.Graphics
  -> WorldSim.Runtime (csak snapshot/read model namespace)

WorldSim.Runtime
  -> WorldSim.AI
  -> WorldSim.Contracts

WorldSim.RefineryAdapter
  -> WorldSim.Contracts
  -> WorldSim.Runtime
  -> WorldSim.RefineryClient

WorldSim.RefineryClient
  -> WorldSim.Contracts
```

Tiltott: `Runtime -> Graphics`, `Runtime -> App`, `AI -> Graphics`, kozvetlen `Graphics -> Simulation mutable state`.

## Atmeneti szabalyok (migration policy)

- Amig a Track C nincs teljesen atvezetve, az AI kod ideiglenesen maradhat a `WorldSim.Runtime` alatt,
  de csak interfesz mogott (`INpcBrain`, `IPlanner` stb.), hogy kesobb vesztesegmentesen atmozgathato legyen.
- Uj AI logika lehetoseg szerint mar most interfesz-alapu wiringgal keszuljon; Track C celja az implementaciok
  fizikai atkoltoztetese `WorldSim.AI` modulba.
- A fenti atmeneti szabaly kizarolag migration idoszakra szol; vegallapotban `WorldSim.Runtime -> WorldSim.AI` fuggoseg maradjon.

## Trackok (A-D)

### Track A - Graphics/UI

Scope:
- `Game1` draw/update vizualis reszeinek kiszervezese `WorldSim.Graphics` ala.
- Kamera, render pass-ok, HUD, tech menu overlay.

Detailed execution plan:
- `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md`

Deliverable:
- `GameHost` csak host legyen; render logika kulon osztalyokban.

Definition of Done:
- F1/F6 UI flow nem torik.
- Zoom/pan mukodik.
- Ugyanaz a gameplay vizualisan korrekt marad.

### Track B - Runtime Core

Scope:
- `Simulation`, `TechTree`, season/ecology/economy update logika `WorldSim.Runtime` ala.
- Command API + snapshot builder.

Deliverable:
- MonoGame-tol fuggetlen runtime assembly.

Definition of Done:
- Runtime buildel standalone.
- Snapshot alapjan ugyanazok a jatekallapotok kirajzolhatok.
- `WorldSim.Runtime` nem fugg `WorldSim.RefineryClient`-tol.
- `WorldSim.App` host marad: nincs kozvetlen mutable domain allapot-manipulacio.
- `WorldSim.Graphics` csak read-model/snapshot tipusokra tamaszkodik (nincs `WorldSim.Simulation` enum leak).

### Track C - AI Module

Scope:
- Utility/GOAP planner kod kivitele `WorldSim.AI` modulba.
- Runtime only interface-level wiring.

Deliverable:
- Planner implementaciok cserelhetok konfiguracioval.

Definition of Done:
- NPC-k dontesi ciklusa regresszio nelkul fut.
- AI modul kulon tesztelheto.

### Track D - Refinery Boundary

Scope:
- `Contracts` + `RefineryAdapter` kiepitese.
- Java patch opok runtime commandokka forditasa.
- Tech ID mapping kulon konfiguracios retegben.

Deliverable:
- Stabil Java-C# boundary, minimalis runtime coupling.

Definition of Done:
- Fixture es live mod parity mukodik.
- Unknown op/tech kezelese determinisztikus hibaval tortenik.

Current Track D focus (Season Director program):
- Director checkpoint alapu beavatkozas (nem per-frame AI): seasononkent / X tick alapjan.
- Egy snapshotbol ket kimenet: `story beat` + `planner nudge`.
- Beat/nudge kulon-kulon kapcsolhato legyen (mindketto, csak egyik, csak masik, egyik sem).
- LLM kreativitas kihasznalasa, de formalis Refinery gate tartja kontroll alatt a hibakat/hallucinaciot.

Track D implementation plan doc:
- `WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md`

Track D design principles (OnlabRefinery parity):
- Felelosseg szetvalasztas: LLM javasol, Refinery validal/repair-el, runtime csak alkalmaz.
- Layering: Design/Model/Runtime jellegu szeparacio a director oldali formalis modellben.
- Iterativ feedback loop: invalid candidate -> feedback -> retry -> deterministic fallback.
- Determinisztikus output policy debugginghoz: ugyanarra a checkpoint inputra reprodukalhato eredmeny.



## Kockazatok es mitigacio

- Nagy `Game1` refaktor regressziot okozhat -> kis, lepesenkenti PR-ek.
- Content path torhet projektmozgasnal -> smoke test minden PR utan.
- Tulsagosan gyors encapsulation lassithat -> snapshot adapterrel fokozatos atallas.

## "Nem kell kulon Track E"

A telemetry/persistence/scenario-runner feladatok nem kulon trackkent kezeltek, hanem:

- Telemetry: Track A (HUD/debug overlay) + Track B (runtime counters)
- Persistence: Track B (runtime allapot) + Track D (patch dedupe state)
- Scenario runner/headless: Track B incremental toolkent, ha CI vagy balansz igenyli

## Wave turn-gate protocol (all track agents)

Cel:
- A wave/sprint sorrend es dependency-k kotelezo betartasa, hogy ne induljon el blokkolo elofeltetel nelkul implementacio.

Szabaly:
- Minden implementacio elejen az adott agent ellenorzi a `Docs/Plans/Combined-Execution-Sequencing-Plan.md` statuszait es dependency sorrendjet.
- Ha elofeltetel hianyzik: kotelezo `NOT READY` jelzes a usernek/koordinatornak, es nincs kodolas az adott epicen.
- Ha minden elofeltetel kesz: `READY` jelzes, majd az adott epic statusza `⬜ -> 🔄`.
- Lezaraskor (build/test/smoke zold): `🔄 -> ✅`.
- Masik track epicjet `✅`-re allitani csak owner visszajelzes vagy explicit koordinator jovahagyas alapjan lehet.

## Kozos uzenofal (cross-track notes)

Cel:
- Olyan rovid, gyakorlati megjegyzesek gyujtohelye, amik tobb tracket is erintenek.

Formatum:
- `[YYYY-MM-DD][Track] rovid cim - hatas - kovetkezo lepes`.

Entries:
- `[2026-02-21][Track D] Season Director roadmap elinditva - Runtime/Graphics/AI feladatokat is erint - reszletes terv: WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md`.
- `[2026-02-21][Track D] A Refinery hasznos mukodesehez runtime oldali director state + idozitett hatas + colony directive kell - Track B-vel osszehangolt command endpointok szuksegesek`.
- `[2026-02-21][Track D] Graphics oldalon story beat es nudge allapot vizualizalasa szukseges (HUD/event feed), kulonben nehezen verifikalhato a checkpoint hatas`.
- `[2026-02-21][Track D] LLM stage csak gated modban induljon (default OFF), Refinery gate maradjon kotelezo minosegkapu`.
- `[2026-02-21][Track C] AI debug panel tracked NPC valtas bekerult (PgUp/PgDn, Home) - HUD/input szinkront erint - kovetkezo lepes: tracked replay/export opcio`.
- `[2026-02-21][Track C] GOAP/HTN trace bovitve (plan cost, replan reason, method) - Track A debug olvashatosaghoz plusz mezok kellenek - kovetkezo lepes: compact+page UX finomitas`.
- `[2026-02-21][Track C] Policy mix aktiv (Global/FactionMix/HtnPilot) es Aetheri HTN pilot - Track B balanszparametereket erint - kovetkezo lepes: config tablaba emeles hardcode switch helyett`.
- `[2026-02-21][Track C] GOAP invalidation+backoff es HTN method scoring bekotve, faction policy table env-bol konfiguralhato - Track B runtime balansz/ops finomhangolast erint - kovetkezo lepes: policy tabla JSON-ra emelese`.
- `[2026-03-02][Track B] P0-D(B)/P0-E implementalva - snapshot person combat mezok (Health/IsInCombat/LastCombatTick) es 1000 tick headless smoke gate bovitve, teljes build+test zold - kovetkezo lepes: Track A P0-D vizualis HP/combat marker render befejezese`.
- `[2026-03-02][Track C] P0-C implementalas elinditva (Fight/Flee threat response) - AI contract/planner/runtime mapping erintett - kovetkezo lepes: teszt gate + statuszaras`.
- `[2026-03-02][Track C] P0-C status zarva (Fight/Flee threat response) - AI contract+planner+runtime mapping bekotve, build+test zold - kovetkezo lepes: P0-D/P0-E cross-track verifikacio`.
- `[2026-03-02][Track D] Wave1 D1 S1-A/B/C/D status zarva (✅) - v1 wire megtartva, additiv C# contracts/v2 namespace, Java director goal+ops+output-mode mock, C# parser/applier+adapter director op tamogatas kesz - kovetkezo lepes: Wave2 S2-A/B/C cross-track handshake Track B/C-vel`.
- `[2026-03-02][Track A] Wave1 P0-D status zarva (✅) - HP bar + in-combat marker render es event feed category color mapping bekotve, build+arch+runtime test zold - kovetkezo lepes: Wave2 Track A P1-E varakozas Track B snapshot stance/territory readiness jelzesig`.
- `[2026-03-03][Track A] Host/input regresszio javitas ujraalkalmazva - Program GameHost inditasra visszaallitva, Game1 kompatibilitasi shim, F1 tech menu HUD overlap megszunt (tech menu exkluziv HUD mod) - kovetkezo lepes: Ctrl+F* + F1 smoke minden futtatas utan`.
- `[2026-03-03][Track A] Map meret noveles (4x terulet) alkalmazva - GameHost runtime default 128x128 -> 256x256 allitva (CreateRuntime + AI options CreateRuntime) - kovetkezo lepes: kamerafit/perf smoke 1080p + windowed`.
- `[2026-03-03][Track A] P1-E status zarva (✅) - Diplomacy panel valodi 4x4 stance matrix render snapshotbol, Territory overlay owner+contested tile rajz es WorldRenderer overlay pass wiring kesz, build+arch test zold - kovetkezo lepes: Wave3 S3-B/P1-I sequencing check es smoke`.
- `[2026-03-03][Meta] Wave1 teljes teszt gate zarva (65/65 zold) - uj tesztek: ThreatResponse_AdjacentEnemies (CombatPrimitives), HeadlessSmoke_WithCombat_IsDeterministic (Harness), DirectorEndToEndTests x2 (Adapter) - fontos lelet: Person._rng es Animal._rng nem seeded (new Random()), predator/combat szamlalok nem determinisztikusak - kovetkezo lepes: Wave2 kickoff (S2-A/B/C + P1-A)`.
- `[2026-03-03][Meta] Wave1 manual QA checklist elkeszult - Docs/Wave1-Manual-QA-Checklist.md - 25 ellenorzesi pont (P0-A/B/C/D, S1-A/C/D, UI regression, AI debug panel) - kovetkezo lepes: QA futtatasa + Wave2 agent promptok keszitese`.
- `[2026-03-03][Track C] S2-B status zarva (Goal Bias AI wiring) - utility score bias bekotve (goal-category alapon), RuntimeNpcBrain context bias mezoket tolti, profession rebalance priority IsGoalPriorityActive alapjan finomitva, build+teljes test gate zold - kovetkezo lepes: S2-C/P1-A Track B allapotjelzes utan P1-D kickoff elokeszites`.
- `[2026-03-03][Track C] P1-D implementalas elinditva (enemy sensing + role behavior) - stance+territory+local threat context es warrior/civilian reakciok wiring alatt - kovetkezo lepes: acceptance tesztek + teljes test gate + statuszaras`.
- `[2026-03-03][Track C] P1-D status zarva (enemy sensing + role behavior) - AI context stance/contested/threat mezokkel bovitve, warrior/civilian engage-retreat logika bekotve, build+teljes test gate zold - kovetkezo lepes: P1-E Track A handoff + Wave2 cross-track verifikacio`.
- `[2026-03-03][Track B] RNG determinisztikusitas javitva - Person/Animal RNG world seedbol szarmazik, Animal.Spawn nem hasznal kulon new Random()-ot, headless 1000 tick smoke azonos seeddel stabil - kovetkezo lepes: S2-A status zaras es S2-B/S2-C folytatas`.
- `[2026-03-03][Track B] S2-A zarva, S2-C elinditva - DomainModifierEngine runtime wire+teszt kesz (✅), GoalBiasEngine Track B API kesz (🔄), DirectorState/tick+snapshot endpointek initial wire kesz (🔄) - kovetkezo lepes: S2-B Track C integracio handoff + S2-C statuszaras`.
- `[2026-03-03][Track B] S2-C status zarva (✅) - DirectorState cooldown+active state tickelve, ApplyStoryBeat/ApplyColonyDirective endpoint DirectorState-re kotve, refinery snapshot director blokk kiegeszitve, runtime/full test gate zold - kovetkezo lepes: S2-B Track C integracio visszajelzes utan statuszaras, majd P1-A inditas`.
- `[2026-03-03][Track B] P1-A status zarva (✅) - faction stance matrix runtime single-source-of-truth (default Neutral), szimmetrikus set/get es snapshot export (FactionStances) bekotve, build+teljes test gate zold - kovetkezo lepes: P1-B relation dynamics triggerek inditasa`.
- `[2026-03-03][Track B] P1-B status zarva (✅) - RelationManager bekotve (border pressure + skirmish trigger + stance cooldown), stance-alapu mobilizacio finomitva, relation dinamika tesztek zold - kovetkezo lepes: P1-C territory contested tiles+counter modell inditasa`.
- `[2026-03-03][Track B] P1-C status zarva (✅) - territory influence score-alapu tile owner+contested szamitas, faction-pair contested counterek es snapshot tile mezok (OwnerFactionId/IsContested) bekotve, runtime/full test gate zold - kovetkezo lepes: P1-D Track C handoff (enemy sensing+role viselkedes)`.
- `[2026-03-03][Track B] P1-D cross-track verifikacio kesz - Track C enemy sensing+role behavior status zarva (✅), teljes gate zold, P1-E Track A HUD/panel/overlay implementacio READY - kovetkezo lepes: Track A P1-E inditas es vizualis verifikacio`.
