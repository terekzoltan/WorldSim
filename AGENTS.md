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
- `[2026-02-28][Track A/B] Build drift javitas: World.NavigationTopologyVersion + randomSeed ctor + territory/mobilization accessor kompatibilitas visszaallitva; RenderFrameContext TimeSeconds/FxIntensity es WorldRenderer overlay/postfx API sync megtortent - kovetkezo lepes: smoke keymap + HUD panel overlap ellenorzes minden session utan`.
