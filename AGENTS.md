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
- `GameHost` draw/update vizualis reszeinek kiszervezese `WorldSim.Graphics` ala.
- Kamera, render pass-ok, HUD, tech menu overlay.

Detailed execution plan:
- `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md`
- `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Sprint3-Execution-Plan.md`

Deliverable:
- `GameHost` csak host legyen; render logika kulon osztalyokban.

Definition of Done:
- F1/F6 UI flow nem torik.
- Zoom/pan mukodik.
- Ugyanaz a gameplay vizualisan korrekt marad.

Current Track A focus (Phase 1, Sprint 2 closeout):
- Snapshot interpolation pipeline stabilizalasa (`previous/current` + `tickAlpha`).
- Atmosphere layer: `FogHazeRenderPass` + finom terrain/resource animaciok (water/grass modulation, food pulse, ore glint).
- Camera feel polish: smooth travel tracked NPC fokuszra (`F2`), nagy map clamp/fit stabilitas.
- 16:9 map preset workflow: `384x216` default, `F5` preset cycle, windowed 16:9 fallback.
- HUD compact telemetry: hosszabb sorok blokkositasa (colony/ecology/events/status), jobb olvashatosag.

Track A integration guardrails (build-break megelozes):
- Ha valtozik `WorldRenderSnapshot`, ugyanabban a PR-ben frissiteni kell:
  - `WorldSnapshotInterpolator`
  - erintett render passok (`ActorRenderPass`, stb.)
- Ha valtozik `RenderFrameContext`, ugyanabban a PR-ben frissiteni kell:
  - minden pass, ami uj mezot hasznal (`FogHazeRenderPass`, `TerrainRenderPass`, `ResourceRenderPass`, stb.)
  - `WorldRenderer` context konstrukcio
- Snapshot contract valtozasnal kotelezo smoke:
  - `dotnet build WorldSim.sln`
  - `dotnet test WorldSim.ArchTests/WorldSim.ArchTests.csproj`

Megjegyzes: `Game1` -> `GameHost` atnevezes megtortent; uj valtoztatasok mar `WorldSim.App/GameHost.cs`-ra hivatkozzanak.

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

- Nagy `GameHost` refaktor regressziot okozhat -> kis, lepesenkenti PR-ek.
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
- `[2026-02-21][Track D] A Refinery hasznos mukodesehez runtime oldali director state + idozitett hatas + colony directive kell - Track B-vel osszehangolt command endpointok szuksegesek`.
- `[2026-02-21][Track D] Graphics oldalon story beat es nudge allapot vizualizalasa szukseges (HUD/event feed), kulonben nehezen verifikalhato a checkpoint hatas`.
- `[2026-02-21][Track D] LLM stage csak gated modban induljon (default OFF), Refinery gate maradjon kotelezo minosegkapu`.
- `[2026-02-21][Track C] AI debug panel tracked NPC valtas bekerult (PgUp/PgDn, Home) - HUD/input szinkront erint - kovetkezo lepes: tracked replay/export opcio`.
- `[2026-02-21][Track C] GOAP/HTN trace bovitve (plan cost, replan reason, method) - Track A debug olvashatosaghoz plusz mezok kellenek - kovetkezo lepes: compact+page UX finomitas`.
- `[2026-02-21][Track C] Policy mix aktiv (Global/FactionMix/HtnPilot) es Aetheri HTN pilot - Track B balanszparametereket erint - kovetkezo lepes: config tablaba emeles hardcode switch helyett`.
- `[2026-02-21][Track C] GOAP invalidation+backoff es HTN method scoring bekotve, faction policy table env-bol konfiguralhato - Track B runtime balansz/ops finomhangolast erint - kovetkezo lepes: policy tabla JSON-ra emelese`.
- `[2026-02-26][Track B] AI Context Contract v1 visszaigazolva Track C fele (P0/P1 mezolista + cadence + determinism policy) - dokumentum: Docs/Plans/Track-C-AI-Context-Contract-v1.md - kovetkezo lepes: fallback mezok kivaltasa valodi diplomacy/territory/role allapottal`.
- `[2026-02-26][Track B] B-Prep 2/3/4 baseline leszallitva (AI context cadence cache + snapshot combat fields + navigation topology scaffold) - dokumentum: Docs/Plans/Track-B-Prep-Roadmap.md - kovetkezo lepes: territory ownership valodi modell es role mobilization runtime policy`.
- `[2026-02-26][Track A] Sprint 2 closeout aktiv: interpolation + haze + compact HUD + 16:9 large map preset - Track B snapshot shape/perf erzekeny - kovetkezo lepes: snapshot/entity id stabilitas es viewport culling policy`.
- `[2026-02-26][Track A] Render context/schema drift build breaket okozott (snapshot/context mismatch) - cross-track tanulsag: contract valtozas egy-PR-ben frissuljon minden downstream renderer komponensben - kovetkezo lepes: integration guardrails formalizalasa`.
- `[2026-02-26][Track C] AI compile blocker elharitva (DecisionTrace signature + planner/brain callsite sync) es Runtime test harness stabilizalva - teljes solution ujra zold - kovetkezo lepes: Sprint 3 LLM stage strict gate mellett, fallback policy megtartasaval`.
- `[2026-02-26][Track C] Track B contract alapjan context cadence cache bekerult (War/Territory 10 tick, WarriorCount 5 tick), fallback mezok runtime forrasra allitva (diplomacy+hostile overlap+role flag) - kovetkezo lepes: valodi territory/diplomacy state kivaltja a proxy logikat`.
