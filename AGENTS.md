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
- A showcase-orientalt visual polish elnyomhatja az olcso/stabil baseline-t -> profile-aware strategia kell, ahol a default fejlesztoi mod nem a legdragabb latvanyreteg.

## "Nem kell kulon Track E"

A telemetry/persistence/scenario-runner feladatok nem kulon trackkent kezeltek, hanem:

- Telemetry: Track A (HUD/debug overlay) + Track B (runtime counters)
- Persistence: Track B (runtime allapot) + Track D (patch dedupe state)
- Scenario runner/headless: Track B incremental toolkent, ha CI vagy balansz igenyli

## Low-Cost 2D guiding constraint

Referencia:
- `Docs/Plans/Master/world_sim_low_cost_2_d_docs.md`

Projekt-szintu alapelv:
- A WorldSim vizualis es teljesitmeny-strategiaja low-cost, state-driven es profile-aware: a `Runtime` szamolja a visual-driving state-et, a snapshot exportalja, a `Graphics` ezt olcson es determinisztikusan jeleniti meg, a quality/profile layer pedig skalazza.
- A default fejlesztoi baseline nem a showcase polish, hanem az olcso es stabil futas.
- A latvanyosabb post-fx / capture / cinematic utak csak raepulhetnek erre az alapra, nem cserelhetik le.
- A low-cost strategy nem irhatja felul a snapshot boundary-t: a `Graphics` nem szamolhat gameplay/allapot logikat sajat maga.

## Wave turn-gate protocol (all track agents)

Cel:
- A wave/sprint sorrend es dependency-k kotelezo betartasa, hogy ne induljon el blokkolo elofeltetel nelkul implementacio.

Szabaly:
- Minden implementacio elejen az adott agent ellenorzi a `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` statuszait es dependency sorrendjet.
- Ha elofeltetel hianyzik: kotelezo `NOT READY` jelzes a usernek/koordinatornak, es nincs kodolas az adott epicen.
- Ha minden elofeltetel kesz: `READY` jelzes, majd az adott epic statusza `⬜ -> 🔄`.
- Lezaraskor (build/test/smoke zold): `🔄 -> ✅`.
- Masik track epicjet `✅`-re allitani csak owner visszajelzes vagy explicit koordinator jovahagyas alapjan lehet.

## Kozos uzenofal (cross-track notes)

Cel:
- Olyan rovid, gyakorlati megjegyzesek gyujtohelye, amik tobb tracket is erintenek.

- Mindig próbálj aljára írni.

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
- `[2026-03-06][Track D] Wave3 S3-C status zarva (✅) - output mode matrix (both/story_only/nudge_only/off) Java oldali tesztekkel es C# adapter oldali env override policyval bekotve, adapter test gate zold - kovetkezo lepes: S3-A runtime fel Track B handshake utan S3-D parity+smoke`.
- `[2026-03-03][Track A] Host/input regresszio javitas ujraalkalmazva - Program GameHost inditasra visszaallitva, Game1 kompatibilitasi shim, F1 tech menu HUD overlap megszunt (tech menu exkluziv HUD mod) - kovetkezo lepes: Ctrl+F* + F1 smoke minden futtatas utan`.
- `[2026-03-04][Track A] Map meret csokkentve (2x terulet) - GameHost runtime default vissza 128x128 (CreateRuntime + AI options CreateRuntime) - kovetkezo lepes: smoke kamerafit + territory overlay viszontosag ellenorzes`.
- `[2026-03-03][Track A] P1-E status zarva (✅) - Diplomacy panel valodi 4x4 stance matrix render snapshotbol, Territory overlay owner+contested tile rajz es WorldRenderer overlay pass wiring kesz, build+arch test zold - kovetkezo lepes: Wave3 S3-B/P1-I sequencing check es smoke`.
- `[2026-03-03][Meta] Wave1 teljes teszt gate zarva (65/65 zold) - uj tesztek: ThreatResponse_AdjacentEnemies (CombatPrimitives), HeadlessSmoke_WithCombat_IsDeterministic (Harness), DirectorEndToEndTests x2 (Adapter) - fontos lelet: Person._rng es Animal._rng nem seeded (new Random()), predator/combat szamlalok nem determinisztikusak - kovetkezo lepes: Wave2 kickoff (S2-A/B/C + P1-A)`.
- `[2026-03-03][Meta] Wave1 manual QA checklist elkeszult - Docs/Wave1-Manual-QA-Checklist.md - 25 ellenorzesi pont (P0-A/B/C/D, S1-A/C/D, UI regression, AI debug panel) - kovetkezo lepes: QA futtatasa + Wave2 agent promptok keszitese`.
- `[2026-03-03][Track C] S2-B status zarva (Goal Bias AI wiring) - utility score bias bekotve (goal-category alapon), RuntimeNpcBrain context bias mezoket tolti, profession rebalance priority IsGoalPriorityActive alapjan finomitva, build+teljes test gate zold - kovetkezo lepes: S2-C/P1-A Track B allapotjelzes utan P1-D kickoff elokeszites`.
- `[2026-03-03][Track C] P1-D implementalas elinditva (enemy sensing + role behavior) - stance+territory+local threat context es warrior/civilian reakciok wiring alatt - kovetkezo lepes: acceptance tesztek + teljes test gate + statuszaras`.
- `[2026-03-03][Track C] P1-D status zarva (enemy sensing + role behavior) - AI context stance/contested/threat mezokkel bovitve, warrior/civilian engage-retreat logika bekotve, build+teljes test gate zold - kovetkezo lepes: P1-E Track A handoff + Wave2 cross-track verifikacio`.
- `[2026-03-03][Track C] P1-H implementalas elinditva (AI defense building + raid skeleton) - hostile/war defense goalok, raid border mozgas es structure attack workflow wiring alatt - kovetkezo lepes: acceptance tesztek + teljes gate + statuszaras`.
- `[2026-03-03][Track C] P1-H status zarva (AI defense building + raid skeleton) - hostile/war defense goalok, raid border mozgas es structure attack workflow bekotve, raid success loot+stance impact aktiv, build+teljes test gate zold - kovetkezo lepes: Wave3 cross-track verifikacio`.
- `[2026-03-03][Track B] RNG determinisztikusitas javitva - Person/Animal RNG world seedbol szarmazik, Animal.Spawn nem hasznal kulon new Random()-ot, headless 1000 tick smoke azonos seeddel stabil - kovetkezo lepes: S2-A status zaras es S2-B/S2-C folytatas`.
- `[2026-03-03][Track B] S2-A zarva, S2-C elinditva - DomainModifierEngine runtime wire+teszt kesz (✅), GoalBiasEngine Track B API kesz (🔄), DirectorState/tick+snapshot endpointek initial wire kesz (🔄) - kovetkezo lepes: S2-B Track C integracio handoff + S2-C statuszaras`.
- `[2026-03-03][Track B] S2-C status zarva (✅) - DirectorState cooldown+active state tickelve, ApplyStoryBeat/ApplyColonyDirective endpoint DirectorState-re kotve, refinery snapshot director blokk kiegeszitve, runtime/full test gate zold - kovetkezo lepes: S2-B Track C integracio visszajelzes utan statuszaras, majd P1-A inditas`.
- `[2026-03-03][Track B] P1-A status zarva (✅) - faction stance matrix runtime single-source-of-truth (default Neutral), szimmetrikus set/get es snapshot export (FactionStances) bekotve, build+teljes test gate zold - kovetkezo lepes: P1-B relation dynamics triggerek inditasa`.
- `[2026-03-03][Track B] P1-B status zarva (✅) - RelationManager bekotve (border pressure + skirmish trigger + stance cooldown), stance-alapu mobilizacio finomitva, relation dinamika tesztek zold - kovetkezo lepes: P1-C territory contested tiles+counter modell inditasa`.
- `[2026-03-03][Track B] P1-C status zarva (✅) - territory influence score-alapu tile owner+contested szamitas, faction-pair contested counterek es snapshot tile mezok (OwnerFactionId/IsContested) bekotve, runtime/full test gate zold - kovetkezo lepes: P1-D Track C handoff (enemy sensing+role viselkedes)`.
- `[2026-03-03][Track B] P1-D cross-track verifikacio kesz - Track C enemy sensing+role behavior status zarva (✅), teljes gate zold, P1-E Track A HUD/panel/overlay implementacio READY - kovetkezo lepes: Track A P1-E inditas es vizualis verifikacio`.
- `[2026-03-03][Track B] P1-G status zarva (✅) - Person MoveTowards BFS cache-t hasznal (12 step horizon), topology mismatch/blocked-step invalidation es 4096 expansion guard+unstick fallback aktivalva, navigation tesztek (topology/blocked-step/ring-gap) zold - kovetkezo lepes: S3-A runtime severity tier implementacio`.
- `[2026-03-03][Track B] P1-F status zarva (✅) - defense scaffold bekotve (WoodWall/Watchtower/DefenseManager), hostiles szamara fal-blokkolas es watchtower auto-fire aktiv, defensive snapshot mezok (DefensiveStructures + HP) exportalva, build+runtime/full test gate zold - kovetkezo lepes: P1-G BFS cache+topology invalidation`.
- `[2026-03-06][Track B] S3-A runtime fel kesz (🔄) - beat severity effect-count alapjan inferalt (minor=0, major<=2, epic<=3), minor beat gameplay-modifier nelkul fut, major/epic cooldown policy DirectorState-re kotve, severity-tagelt director event feed bekotve, runtime+full gate zold - kovetkezo lepes: S3-A teljes statuszaras Track D contract/adapter visszajelzessel`.
- `[2026-03-06][Track B] S3-A status zarva (✅) - Track D contract/adapter oldali severity enforce + S3-C/S3-D kesz allapotaval egyutt cross-track lezaras megtortent, Step3 (Track A S3-B) feloldva - kovetkezo lepes: Track A S3-B HUD/event feed integracio`.
- `[2026-03-08][Track B] Wave3.1 W3.1-B1 status zarva (✅) - runtime director render state mar adapter altal szamitott effective output mode/source/stage alapjan frissul (nem env default), refinery snapshot director blokk effective mode/source mezokkel bovult, adapter+runtime+full gate zold - kovetkezo lepes: W3.1-A1 Track A HUD consume + W3.1-B2 territory periodic cache`.
- `[2026-03-08][Track B] Wave3.1 W3.1-B2 status zarva (✅) - territory ownership/contested recompute periodikus cache modellre valtott (5 tick interval + dirty trigger), per-tick full scan megszunt, territory determinisztikus/perf tesztek zold - kovetkezo lepes: Wave3.1 cross-track smoke + Wave4 kickoff readiness`.
- `[2026-03-08][Track B] Wave3.2 W3.2-B1 status zarva (✅) - actor end-position deconfliction bekotve (peaceful mode), tick-vegi stack feloldas nearby free tile fallbackkal aktiv, overlap regresszio teszt zold - kovetkezo lepes: W3.2-B3 no-progress detection+backoff`.
- `[2026-03-06][Track A] P1-I status zarva (✅) - wall/watchtower render + damaged structure HP bar + watchtower beam visualization (recent event trigger) bekotve, build+arch test zold - kovetkezo lepes: S3-B varakozas S3-A teljes (B+D) statuszarasig`.
- `[2026-03-06][Track D] Wave3 S3-D status zarva (✅) - director fixture replay+live parity hash teszt (opt-in), Java/C# teszt gate zold, manual smoke checklist dokumentalva (Docs/Wave3-Director-Smoke-Checklist.md) - kovetkezo lepes: S3-A cross-track statuszaras Track B runtime fel kesz jelzes utan`.
- `[2026-03-06][Track A] S3-B status zarva (✅) - director render state snapshotba emelve, event feed severity color (minor/major/epic), HUD directive+timer/stage/cooldown es debug sorok (modifiers+biases) bekotve, build+arch test zold - kovetkezo lepes: Wave3 teljes cross-track smoke`.
- `[2026-03-08][Track D] Wave3.1 W3.1-D1 status zarva (✅) - adapter oldalon effective director mode/source truth status bevezetve (env/response/fallback), trigger adapteren keresztul kiolvashato handoff, mode-source determinisztikus tesztek (auto->response, auto->fallback, env override) zold - kovetkezo lepes: W3.1-B1 runtime snapshot handoff Track B oldalon`.
- `[2026-03-08][Track A] Wave3.1 W3.1-A1 status zarva (✅) - HUD/planner status a snapshot director effective mode+source+stage mezoket mutatja (nem env default), B1 handoff truth vizualisan kovetheto - kovetkezo lepes: W3.1-A2 beam stance-alapu celpontszures`.
- `[2026-03-08][Track A] Wave3.1 W3.1-A2 status zarva (✅) - watchtower beam target valasztas FactionStances alapjan Hostile/War kapcsolatra szukitve, Neutral/Tense es same-faction celpontok kizartak - kovetkezo lepes: W3.1-B2 Track B territory cache utan cross-track smoke`.
- `[2026-03-08][Track B] Wave3.2 W3.2-B3/B4 status zarva (✅) - MoveTowards no-progress detection+backoff bekotve (tracked jobs: Fight/Raid/Attack + faction-threat flee), es flee behavior safe-area/refuge ring celvalasztasra valtott (nem single origin tile), runtime+full test gate zold - kovetkezo lepes: W3.2-B2 soft reservation, majd W3.2-B5 observability export`.
- `[2026-03-08][Track B] Wave3.2 W3.2-B2/B5 status zarva (✅) - soft reservation API bekotve (resource/build/retreat key alapu crowd-penalty), snapshot observability bovitve (Colony WarState/WarriorCount, NPC no-progress+backoff+decision/target debug, Eco soft reservation count), uj runtime tesztek zold + teljes solution gate zold - kovetkezo lepes: W3.2-C1/C2 es W3.2-A1/A3 parhuzamos kickoff`.
- `[2026-03-08][Track C] Wave3.2 W3.2-C1 implementalas elinditva (peaceful zero-signal fallback audit) - defense/flee trigger kuszob es planner suppression finomitas folyamatban - kovetkezo lepes: C1 teszt gate + statuszaras`.
- `[2026-03-08][Track C] Wave3.2 W3.2-C1/C2 status zarva (✅) - peaceful zero-signal defensive fallback suppression es crowd-aware AI tie-break penalty bekotve, RuntimeNpcBrain crowd pressure context exporttal, AI+runtime+full solution gate zold - kovetkezo lepes: W3.2 A-track vizualis closeout majd Wave4 readiness`.
- `[2026-03-08][Track A] Wave3.2 W3.2-A1 status zarva (✅) - actor stack debug marker render bekotve (count-severity szinkod + no-progress/backoff kiemeles), overlap hotspotok manual QA-ban gyorsan azonosithatok, build+arch test zold - kovetkezo lepes: W3.2-A2 labels es W3.2-A3 combat overlay debug tartalom`.
- `[2026-03-08][Track A] Wave3.2 W3.2-A2 status zarva (✅) - diplomacy panel faction label render F* helyett rovid nev mappingre valtott (Syl/Obs/Aet/Chi), cim+legend olvashatosag javitva (N/T/H/W + faction key), build+arch test zold - kovetkezo lepes: W3.2-A3 combat overlay debug tartalom`.
- `[2026-03-08][Track A] Wave3.2 W3.2-A3 status zarva (✅) - placeholder combat overlay lecserelve valos debug retegelesre (contested tint + in-combat keret + no-progress marker + colony war/warrior diagnostics), build+arch test zold - kovetkezo lepes: Wave3.2 Track A closeout smoke`.
- `[2026-03-09][Track B] Wave3.5 W3.5-B1 status zarva (✅) - BuildHouse start/finish gate egységesitve (NeedsHousing + teljes koltseg check), profession+planner BuildHouse inditas fallback gatherre valt ha nem megfizetheto, pseudo-work loop megszunt; uj runtime tesztek (Wave35BuildHouseStartTests) + teljes solution gate zold - kovetkezo lepes: W3.5-B2 explicit build-site targeting/state`.
- `[2026-03-09][Track B] Wave3.5 W3.5-B2 status zarva (✅) - explicit build-site state (house/wall/watchtower) bekotve: build intent eloszor konkret celhelyet valaszt, actor odamegy, munka csak site-on indul, completion ugyanarra a lockolt site-ra alkalmaz; uj runtime tesztek (Wave35BuildSiteTargetingTests) + teljes solution gate zold - kovetkezo lepes: W3.5-B3 birth spawn free-tile elosztas`.
- `[2026-03-09][Track B] Wave3.5 W3.5-B3 status zarva (✅) - newborn spawn policy parent tile helyett kozelben levo szabad fold tile-t valaszt (radiusos occupancy-alapu priorizalas), fallback parent tile csak ha nincs szabad hely; uj runtime tesztek (Wave35BirthSpawnTests) + teljes solution gate zold - kovetkezo lepes: W3.5-B4 local crowd dissipation`.
- `[2026-03-09][Track B] Wave3.5 W3.5-B4 status zarva (✅) - DeconflictPeopleEndPositions masodik fazissal bovitve (local crowd dissipation): suru csoportokbol korlatozott tickenkenti actor-athelyezes alacsonyabb neighbor-density tileokra, combat/backoff jobok vedelmevel; uj runtime tesztek (OccupancyDeconflictionTests crowd density+sparse stability) + teljes solution gate zold - kovetkezo lepes: W3.5-B5 peaceful gather/build no-progress+backoff coverage`.
- `[2026-03-09][Track B] Wave3.5 W3.5-B5 status zarva (✅) - no-progress tracking kiterjesztve peaceful move intentekre (resource/build_site), backoff cause context-taggel exportalva (`no_progress_backoff:resource|build`), build backoff eseten site state reset + peaceful backoff wait guard aktiv; uj runtime tesztek (OccupancyDeconflictionTests peaceful resource/build no-progress) + teljes solution gate zold - kovetkezo lepes: W3.5-C1/Ctrack audit es W3.5-A1 debug UI closeout`.
- `[2026-03-09][Track C] Wave3.5 W3.5-C1 implementalas elinditva (planner warm-up/double-think audit) - per-tick AI think cache es peaceful pseudo-idle fallback guard finomitas folyamatban - kovetkezo lepes: runtime+AI teszt gate + statuszaras`.
- `[2026-03-09][Track C] Wave3.5 W3.5-C1 status zarva (✅) - per-tick AI think cache megszuntette a warm-up/double-think anomaliat, peaceful zero-signal fallback guard megtartva, uj runtime teszt (Wave35PlannerWarmupTests) + teljes solution gate zold - kovetkezo lepes: W3.5-A1 debug UI closeout es Wave3.5 cross-track smoke`.
- `[2026-03-09][Track A] Wave3.5 W3.5-A1 status zarva (✅) - AI debug panel tracked NPC sor bovitve (decision cause + target key + build intent + no-progress/backoff), HudRenderer snapshotot ad at panelnek, teljes build+arch test zold - kovetkezo lepes: Wave3.5 cross-track smoke`.
- `[2026-03-09][Track B] Wave3.6 W3.6-B1 status zarva (✅) - birth spawn es build-site valasztas actor-free first policyra valtott (nem csak structure-free): World actor occupancy helper bevezetve, births actor-free pass utan explicit occupied fallbackot hasznal, house/defense build-site finder actor-free candidatokra priorizal es csak szukseg eseten fallbackol; uj runtime tesztek (Wave35BirthSpawnTests + Wave35BuildSiteTargetingTests actor-free/fallback esetek) + teljes solution gate zold - kovetkezo lepes: W3.6-B2 active peaceful intent vedelme crowd dissipation ellen`.
- `[2026-03-09][Track B] Wave3.6 W3.6-B2 status zarva (✅) - active peaceful gather/build intent vedelme bekotve: Person intent-protection flag expose (`IsActivePeacefulIntentProtected`), overlap deconflict preferencia non-protected actor mozgatasa fele, crowd dissipation guard skip protected workerre, build-site flow nem invalidalodik csendben; uj runtime tesztek (OccupancyDeconflictionTests protected gather + overlap preference) + teljes solution gate zold - kovetkezo lepes: W3.6-B3 clustering telemetry export`.
- `[2026-03-09][Track B] Wave3.6 W3.6-B3 status zarva (✅) - clustering telemetry/stuckness counter export runtime+headless oldalon: overlap resolve, crowd dissipation, birth fallback (occupied/parent), build-site reset, no-progress backoff cause bontas es dense-neighborhood tick/last actor metrics bekotve; snapshot EcoHudData + ScenarioRunner output bovitve, uj tesztassertokkal runtime gate es teljes solution gate zold - kovetkezo lepes: W3.6-B4 scenario matrix runner strukturalt outputtal`.
- `[2026-03-09][Track B] Wave3.6 W3.6-B4 status zarva (✅) - ScenarioRunner matrix runner strukturalt outputtal bovitve: multi-seed + multi-planner (Simple/Goap/Htn) + optional multi-config JSON input, jsonl/json/text modok, run-level clustering telemetry export minden kombinaciora; scenario build smoke + teljes solution gate zold - kovetkezo lepes: W3.6 cross-mode evidence pass Track A/C closeouttal`.
- `[2026-03-10][Track C] Wave3.6 W3.6-C1 implementalas elinditva (crowd-aware tie-break reconcile) - W3.2 claimhez kod+teszt visszaigazitas, AI goal score crowd penalty ujrawire alatt - kovetkezo lepes: AI/runtime teszt gate + statuszaras`.
- `[2026-03-10][Track C] Wave3.6 W3.6-C1 status zarva (✅) - W3.2 crowd-aware tie-break behavior visszaallitva (AI goal score crowd penalty), RuntimeNpcBrain crowd pressure context export ujra bekotve, AI+runtime+full solution gate zold - kovetkezo lepes: W3.6-A1/A2 handoff + W3.6 evidence pass B4 utan`.
- `[2026-03-10][Track A] Wave3.6 W3.6-A1 status zarva (✅) - tracked actor stable identity bekotve end-to-end (Person.Id -> RuntimeAiDecision.ActorId -> AiDebugSnapshot.TrackedActorId -> UI resolve), tile-shared actor drift megszunt; teljes build+arch test zold - kovetkezo lepes: W3.6-A2 sim speed controls + HUD indicator`.
- `[2026-03-10][Track A] Wave3.6 W3.6-A2 status zarva (✅) - sim speed controls bekotve (Ctrl+P pause/resume, Ctrl+-/+ speed, Ctrl+. single-step), planner/HUD status Sim indikatorral bovult, teljes build+arch test zold - kovetkezo lepes: W3.6 evidence pass`.
- `[2026-03-10][Track B] Wave4 P2-A status zarva (✅) - technologies.json katonai+eroditmeny aggal bovitve (weaponry/armor/military_training/war_drums/scouts/advanced_tactics/fortification/advanced_fortification/siege_craft), TechTree effect mapping uj runtime mezokre bekotve, fortification unlock gate tech-id alapon aktiv SimulationRuntime-ban; uj runtime tesztek (Wave4MilitaryTechTests) + teljes solution gate zold - kovetkezo lepes: P2-B colony equipment levels (weapon/armor)`.
- `[2026-03-10][Track D] Wave4 S4-A/S4-B status zarva (✅) - Java oldalon formalis director invariant kodok (INV-01..14, INV-20) validator feedbackgel bekotve, validation+conservative-retry+deterministic fallback planner loop stabil marker/feedback kimenettel kesz, uj validator+fallback tesztek es teljes Java test gate zold - kovetkezo lepes: Wave5 D5 (S5-A/S5-B) kickoff`.
- `[2026-03-10][Track B] Wave4 P2-B status zarva (✅) - colony equipment levels (WeaponLevel/ArmorLevel 0..3) bekotve, tech unlock mapping (weaponry/armor_smithing) equipment state-re kotve, warrior-role fight damage es incoming defense equipment-level + global military bonus alapjan skalazva, colony snapshot weapon/armor mezokkel bovitve; uj runtime tesztek (Wave4ColonyEquipmentTests) + teljes solution gate zold - kovetkezo lepes: P2-C advanced defenses (stone walls/gates/arrow-catapults)`.
- `[2026-03-10][Track B] Wave4 P2-C status zarva (✅) - advanced defenses bekotve: stone/reinforced wall, gate (friendly pass/hostile block), arrow tower es catapult tower AoE, upkeep unpaid eseten inaktiv state; tech unlock gate enforced (fortification/advanced_fortification/siege_craft) es snapshot defense kind+active mezok bovitve, uj runtime tesztek (Wave4AdvancedDefenseTests) + teljes solution gate zold - kovetkezo lepes: P2-D Track C handoff (tech-aware fight avoidance)`.
- `[2026-03-10][Track B] Wave4 P2-A/B/C fixup zarva (✅) - review gapok javitva: Wave4AdvancedDefenseTests 9 kotelezo esettel potolva, fortification HP multiplier konstrukciokor ervenyesitve (stone/reinforced + gate/tower path), scout radius bonus threat sensingbe bekotve, combat morale bonus colony morale targetbe (Tense/War) bekotve; runtime+full gate zold, P2-D/P2-E handoff unblock tisztazva`.
- `[2026-03-10][Track C] Wave4 P2-D implementalas elinditva (tech-aware fight avoidance) - AI context equipment/tech mezok, threat policy low-equipment gate es military-tech unlock goal wiring folyamatban - kovetkezo lepes: acceptance tesztek + teljes gate + statuszaras`.
- `[2026-03-10][Track C] Wave4 P2-D status zarva (✅) - AI context equipment/tech mezok, low-equipment threat policy es UnlockMilitaryTech goal/planner wiring bekotve, ResearchTech command runtime fallback CraftTools mappinggal aktiv, AI+runtime+full solution gate zold - kovetkezo lepes: P2-E Track A handoff + Wave4 cross-track smoke`.
- `[2026-03-10][Track A] Wave4 P2-E status zarva (✅) - advanced defense render 7 kindra bovitve (wood/stone/reinforced/gate/watch/arrow/catapult) + inactive indicator, tower projectile event match bovitve (arrow/catapult) catapult splash markerrel, tech menu military branch header es colony HUD weapon/armor sor bekotve; build+arch test zold - kovetkezo lepes: Wave4 cross-track smoke`.
- `[2026-03-10][Meta] SMR-M1 status zarva (✅) - Session-Balance-QA-Plan + Session-Perf-Profiling-Plan abszorbalva Combined-plan Wave4.5 szekcioban: invariant katalog (SURV/COMB/ECON/SCALE), perf budget tabla, SimStats/RenderStats infra felelosseg-bontas, CI workflow spec (SMR-B6 uj epic), SMR-B4 perf mode spec beepitve; SMR-B1 Track B agent prompt keszitese kovetkezik`.
- `[2026-03-10][Track B] Wave4.5 SMR-B1 status zarva (✅) - ScenarioRunner artifact bundle contract bevezetve (`manifest.json`, `runs/*.json`, `summary.json`, `anomalies.json=[]`, `run.log`), `WORLDSIM_SCENARIO_ARTIFACT_DIR` env-guarded additive output es per-run file-safe naming bekotve, uj ScenarioRunner tesztprojekt 5 artifact acceptance teszttel + teljes solution gate zold - kovetkezo lepes: SMR-B2 assertion+anomaly engine es exit-code policy implementalas`.
- `[2026-03-10][Track B] Wave4.5 SMR-B2 status zarva (✅) - ScenarioRunner assertion+anomaly engine bekotve (SURV/ECON/COMB invariant katalog, combat counter hiany graceful skip), explicit exit-code policy aktiv (`0` ok, `2` assert_fail, `3` config_error, `4` anomaly_gate_fail), artifact bundle bovitve `assertions.json` + valos `anomalies.json` kimenettel es manifest assertion/anomaly summary mezokkel; uj ScenarioRunner tesztek (assert fail, skipped combat invariants, anomaly non-block default, config error exit) + teljes solution gate zold - kovetkezo lepes: SMR-B3 baseline comparison + delta threshold policy implementalas`.
- `[2026-03-10][Track B] Wave4.5 SMR-B3 status zarva (✅) - baseline compare engine bekotve (`WORLDSIM_SCENARIO_COMPARE` + `WORLDSIM_SCENARIO_BASELINE_PATH`), run-level metric delta + pass->fail regression detektalas + SCALE-01/02 check aktiv, `WORLDSIM_SCENARIO_DELTA_FAIL` policyval threshold breach anomaly-gate failre emelheto; artifact bundle bovitve `compare.json` es manifest compare summary mezokkel, uj ScenarioRunner compare tesztek (baseline missing, delta artifact, regression report, delta-fail exit, scaling checks) + teljes solution gate zold - kovetkezo lepes: SMR-B4 unified CLI surface + perf mode`.
- `[2026-03-10][Track C] Wave4.5 SMR-C1 implementalas elinditva (AI/planner anomaly signals) - runtime AI decision signal counterek es ScenarioRunner AI anomaly export wiring folyamatban - kovetkezo lepes: runtime+scenario test gate + statuszaras`.
- `[2026-03-10][Track C] Wave4.5 SMR-C1 status zarva (✅) - SMR-hez AI/planner signal export bekotve (NoPlan, ReplanBackoff, ResearchTech counters), ScenarioRunner run/anomaly artifact mezok bovitve, runtime+scenario+full solution gate zold - kovetkezo lepes: SMR-B3 baseline compare integriacio + evidence gate`.
- `[2026-03-10][Meta] Low-cost 2D strategy referencia rogzitve - state-driven vizual + profile-aware baseline projekt-szintu guiding constraint lett - kovetkezo lepes: Combined plan Wave 7.5 low-cost baseline sequencing + Track A/B/perf doc alignment`.
- `[2026-03-10][Meta] SMR-B4 agent prompt elkeszult - Docs/Plans/Master/Wave4.5-SMR-B4-Track-B-Agent-Prompt.md - unified MODE env var (standard/assert/compare/perf/all), perf stopwatch wiring, ANOM-PERF-* anomaliak, perf.json artifact, manifest perf mezok, 7 uj teszt spec - kovetkezo lepes: Track B SMR-B4 implementalas`.
- `[2026-03-10][Meta] SMR-M2 status zarva (✅) - Docs/Plans/Master/SMR-M2-Evidence-Review-Protocol.md irva: baseline update policy (mikor/hogyan frissiteni, minimum 3 seed + 3 planner), artifact retention policy (local/CI/wave-boundary), evidence-review protocol (7-lepes checklist merge elott, shorthand fast-path graphics PR-ekhez) - Combined-plan SMR-M2 statusz ✅ - kovetkezo lepes: SMR-B4 Track B implementalas, majd SMR evidence gate`.
- `[2026-03-10][Track B] Wave4.5 SMR-B4 status zarva (✅) - unified MODE surface bekotve (`WORLDSIM_SCENARIO_MODE=standard/assert/compare/perf/all`) OR-semantikaval a legacy env flagok felett, per-tick perf stopwatch metrikak (`PerfAvgTickMs/PerfMaxTickMs/PerfP99TickMs/PerfPeakEntities`) run-level exporttal aktiv, ANOM-PERF-* red-zone anomalia es `WORLDSIM_SCENARIO_PERF_FAIL` gate policy bekotve; artifact bundle bovitve `perf.json` + manifest perf mezokkel (`perfEnabled/perfRunCount/perfRedCount/perfYellowCount`), uj PerfMode tesztek (7 required eset) + teljes solution gate zold - kovetkezo lepes: SMR-B5 lightweight drilldown evidence export`.
- `[2026-03-10][Track B] Wave4.5 SMR-B5 status zarva (✅) - lightweight drilldown evidence export bekotve (`WORLDSIM_SCENARIO_DRILLDOWN`, `WORLDSIM_SCENARIO_DRILLDOWN_TOP`, `WORLDSIM_SCENARIO_SAMPLE_EVERY`): artifact bundle `drilldown/index.json` + run-szintu `timeline.json`/`events.json`/`replay.json` kimenettel bovult, worst-run deterministic scoring+top-N valasztas es replay-oriented payload aktiv, manifest drilldown mezok (`drilldownEnabled/drilldownSelectedRuns/drilldownTopN/drilldownSampleEvery`) hozzaadva; uj ScenarioRunner drilldown tesztek + teljes solution gate zold - kovetkezo lepes: SMR-B6 CI workflow integracio`.
- `[2026-03-10][Track B] Wave4.5 SMR-B6 status zarva (✅) - CI workflow bekotve `.github/workflows/smr-headless.yml` alatt: push/PR/workflow_dispatch trigger, assert+perf matrix futas, SMR exit-code gate, artifact upload `smr-artifacts-<mode>-<run_id>` naminggel, retention policy M2 szerint (alap 14 nap, assert/perf red/non-zero eseten 30 nap), manifest summary GitHub step summary-be exportalva - kovetkezo lepes: SMR evidence gate (B4+B5+B6+C1+M1/M2)`.
- `[2026-03-10][Meta] SMR minimum ops checklist rogzitve - `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md` standard run-profile nevekkel, artifact path szaballyal (`.artifacts/smr/<run-name>/`), minimum report format-tal es baseline discipline-nel - kovetkezo lepes: sessionek ezt hasznaljak napi workflowhoz, a nagyobb reporting/workflow/baseline tooling kesobb kulon backlogkent kezelendo`.
- `[2026-03-10][Meta] Elso committed SMR evidence note rogzitve - `Docs/Evidence/SMR/baseline-candidate-001/README.md` a small-default smoke lane baseline-jelolt eredmenyet dokumentalja (0 anomaly / 0 assert fail / 1 perf yellow), de nem kezeli teljes projekt-szintu canonical baseline-kent - kovetkezo lepes: compare-baseline run ugyanarra a matrixra, kulon medium/standard evidence kesobb`.
- `[2026-03-16][Meta] Manual Test Helper session terv rogzitve - `Docs/Plans/Session-Manual-Test-Helper-Plan.md` a manual app/SMR kerdesek, PowerShell parancsok, env var setup es gyors codebase-aware QA segites dedikalt sessione; cel hogy a Meta Coordinator ne teljen meg operational kerdesekkel - kovetkezo lepes: hasznalat on-demand manual test es SMR futtatasok mellett`.
- `[2026-03-14][Track D] Wave5 S5-A status zarva (✅) - Java director runtime hardening kesz: in-memory telemetry counterek (requests/validated/fallback/rejected/retry avg), `/v1/director/telemetry` debug endpoint, retry/fallback/dropped-op dedupe accounting es pipeline diagnostic logging bekotve; planner+controller teszt gate zold - kovetkezo lepes: S5-B invariant pack closeout`.
- `[2026-03-14][Track D] Wave5 S5-B status zarva (✅) - INV-20 same-domain ellentetes modifier tiltas validatorban aktiv, director payload additiv severity/effects/biases shape tamogatassal bovult, 1000 random no-crash fuzz teszt es invariant rationale dokumentalas frissitve, teljes Java test gate zold - kovetkezo lepes: Wave6 S6-A kickoff`.
- `[2026-03-14][Track B] Wave5 C5 P3-A/P3-B status zarva (✅), P3-C Track B runtime fel kesz (🔄) - formation/group combat phase world tickbe kotve (clustered group pairing + multi-tick battle resolve), per-person morale/routing/commander state es colony/group/battle snapshot export bekotve, AI context commander+group morale mezokkel bovitve; runtime+AI+arch+scenario gate zold - kovetkezo lepes: Track C P3-C AI commander logic es Track A P3-D battle overlay handoff`.
- `[2026-03-14][Track C] Wave5 C5 P3-C status zarva (✅) - commander-aware AI policy bekotve (group morale + commander stability alapjan fight/raid/retreat valtas), Simple/Goap/Htn planner commander logikaval frissitve, AI+runtime+full solution gate zold - kovetkezo lepes: P3-D Track A handoff + Wave5 cross-track verifikacio`.
- `[2026-03-14][Track A] Wave5 C5 P3-D status zarva (✅) - battle overlay battle-zone+formation marker retegelesre bovitve (CombatGroups/Battles snapshot consume), actor render battle morale/routing/commander markerrel kiegeszitve, build+arch gate zold - kovetkezo lepes: Wave5 cross-track smoke/verifikacio`.
- `[2026-03-16][Track A] Wave5.1 W5.1-A1 status zarva (✅) - battle readability cleanup: battle-zone fill visszafogva, routing edge/actor marker kiemelve, commander anchor badge kontraszt javitva, no-progress marker battle-contexten kivul tartva, debug HUD legend sor frissitve; build+arch gate zold - kovetkezo lepes: W5.1-C1 closeout utan wave5.1 smoke evidence`.
- `[2026-03-16][Track B] Wave5.1 W5.1-B1/B2/B4 status zarva (✅) - ScenarioRunner combat counter parity bekotve (COMB-01/02 mar nem skip combat-enabled futasban), combat observability export bovitve (run+timeline: CombatDeaths/PredatorKillsByHumans/BattleTicks/PeakActiveBattles/PeakActiveCombatGroups/PeakRoutingPeople/TicksWithActiveBattle/MinCombatMorale), es predator-human toggle szemantika ketiranyu lett (predator harass + ember fight predator branch csak toggle ON), runtime+scenario+AI gate zold - kovetkezo lepes: W5.1-B3 battle-local spacing/egress/contact realization closeout`.
- `[2026-03-16][Track B] Wave5.1 W5.1-B3 status zarva (✅) - battle-local spacing pass bekotve (active battle context actor de-stack + routing-first reposition), routing egress origin bias bevezetve (BeginRouting origin + Flee move-away prioritas), es medium combat lane no-progress zaj kontroll stabilizalva; runtime+scenario+AI gate zold - kovetkezo lepes: W5.1-C1 AI re-engage/congestion audit kickoff`.
- `[2026-03-16][Track C] Wave5.1 W5.1-C1 implementalas elinditva (AI re-engage/congestion audit) - routing/re-engage gate es congestion-aware fight suppression finomitas folyamatban - kovetkezo lepes: acceptance tesztek + teljes gate + statuszaras`.
- `[2026-03-16][Track C] Wave5.1 W5.1-C1 status zarva (✅) - routing/re-engage suppression bekotve (IsRouting/RoutingTicks/Backoff context), commander/morale aware re-engage gate finomitva, AI+runtime+full solution gate zold - kovetkezo lepes: Wave5.1 smoke evidence`.
- `[2026-03-19][Track B] Wave6 C6 P3-E/P3-F status zarva (✅) - siege state+breach runtime wire bekotve (active siege sessions, per-tick pressure tracking, breach events/counters), structure raid damage resolver siege priority celponttal es siege_craft scalinggal bovitve, snapshot siege+breach read-model export kesz; runtime+AI+arch+scenario gate zold - kovetkezo lepes: Track C P3-G siege tactics handoff + Track A P3-H overlay consume`.
- `[2026-03-19][Track D] Wave6 S6-A status zarva (✅) - Java LLM director proposal stage aktiv: OpenRouter config+client wiring, director snapshot (`snapshot.director`) consume legacy fallbackkal, prompt/parser sanitize+clamp es deterministic op mapping bekotve; malformed/hibas LLM output graceful fallback iranyba terelve, Java test gate zold - kovetkezo lepes: S6-B iterative correction loop closeout`.
- `[2026-03-19][Track D] Wave6 S6-B status zarva (✅) - Director retry loop LLM feedback-reprompt ciklussal bovult (invalid -> feedback -> uj candidate), conservative retry es deterministic fallback policy megtartva, iteration logging/telemetry kompatibilitas megorizve; uj retry-loop tesztek + teljes Java test gate zold - kovetkezo lepes: S6-C influence budget kickoff Track B handshakel`.
- `[2026-03-19][Track A] Wave6 C6 P3-H status zarva (✅) - siege overlay consume bekotve (siege zone + breach marker + hot siege HUD summary), battle/siege marker szemantika F8 legendben frissitve, build+arch gate zold - kovetkezo lepes: Wave6 cross-track smoke/verifikacio`.
- `[2026-03-19][Track D] Wave6 S6-C (D part) status zarva (✅) - influence budget semantics Java oldalon stabilizalva: INV-15 validator budget-check (domain modifier + goal bias cost formula), director snapshot/runtime-facts budget consume (`constraints.maxBudget` -> snapshot fallback -> env default), prompt budget-context es `budgetUsed:<decimal>` explain marker wire minden director pathon (mock/validated/fallback), uj budget mapper/validator/prompt tesztek + teljes Java test gate zold - kovetkezo lepes: Track B S6-C (B part) budget state mirror + checkpoint reset + snapshot export`.
- `[2026-03-19][Track C] Wave6 C6 P3-G status zarva (✅) - AI siege taktika bekotve (tower-pressure target shift, siege retreat/sortie policy, Simple/Goap/Htn raid+defend decision update), runtime fallback branch siege-aware kontextussal szinkronizalva (AttackStructure handoff, raid/attack retreat gate), uj AI+runtime tesztek (siege policy + siege context export) es teljes solution gate zold - kovetkezo lepes: Wave6 cross-track smoke/verifikacio`.
- `[2026-03-19][Track B] Wave6 S6-C (B part) status zarva (✅) - DirectorState budget mirror bekotve (Max/Remaining/LastUsed/CheckpointTick), checkpoint eleji budget reset + response `budgetUsed:<decimal>` marker parse runtime visszairassal aktiv, live request constraints.maxBudget+optional outputMode kuldessel bovult, refinery snapshot es director render state valos budget mezoket exportal; runtime+adapter tesztek budget reset/mirror esetekkel zold - kovetkezo lepes: Wave6 cross-track smoke/verifikacio`.
- `[2026-03-20][Track A] Wave6 Step3 S6-C consume closeout (✅) - director budget HUD/debug consume finomitva: budget sor marad remaining/max/used formatban, debug nezet checkpoint tick + used% mezokkel bovult, snapshot boundary tartva (csak DirectorRenderState consume); kovetkezo lepes: Wave6 manual smoke/evidence budget visibility ellenorzessel`.
- `[2026-03-22][Track D] Wave6.1 tervezes rogzitve - live F6 smoke ket hardening gapet hozott: (1) Java-valid story beat lehet C# apply-invalid ha effect duration != beat duration, (2) apply fail eseten a HUD stage idle-n marad; Wave6.1-ben a jelenlegi runtime-safe contract marad source of truth (effect duration == beat duration), a gazdagabb per-effect duration modell kesobbre halasztva - kovetkezo lepes: D6.1-A contract alignment, majd D6.1-B apply observability hardening es D6.1-C live smoke/docs frissites`.
