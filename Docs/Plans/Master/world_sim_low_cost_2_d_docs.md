# WorldSim Low-Cost 2D Docs

## 1. Architecture Plan

# WorldSim Low-Cost 2D World Architecture Plan
**Cél:** letisztult, erős identitású 2D világ, amely nagyon kevés teljesítményt igényel, és a jelenlegi WorldSim architektúrára építve is reálisan megvalósítható.

**Dátum:** 2026-03-10  
**Projekt-alap:** WorldSim jelenlegi gitingest snapshot + hivatalos engine/dokumentációs kutatás  
**Fókusz:** MonoGame-alapú megoldás megtartása, low-cost vizuális rendszerek, skálázható sim + render, opcionális jövőbeli data-oriented bővítések

---

## 0. Rövid ítélet

A rövid, őszinte válasz:

**Nem kell most más architektúra.**  
A WorldSim jelenlegi iránya kifejezetten alkalmas arra, hogy ráépíts egy **procedurális, rendszerszintű, nagyon olcsó 2D vizuális réteget**. A mostani szétválasztásod — `App -> Graphics -> Runtime`, snapshot/read-model boundary, külön `ScenarioRunner`, külön AI- és adapter-határok — pont azt támogatja, hogy a látvány és a szimuláció külön optimalizálható legyen.

A videóban látott gondolkodásmódot viszont **érdemes átvenni**:

- ne kézzel gyárts százféle assetet,
- ne egyedi draw-trükkökből élj,
- ne “minden entity külön kis mini-világ” legyen,
- hanem építs **olyan szabályokat**, amelyekből a világ kinézete és viselkedése nagy részt automatikusan előáll.

A legfontosabb következtetés:

> **A WorldSim 2D-s megfelelője nem a Substance + DOTS másolása, hanem ez:**
>
> `Runtime számol állapotot -> Snapshot exportál -> Graphics szabályalapúan vizualizál -> Quality/Profile/Perf layer skálázza`

Ez a projekt jelenlegi formájával kompatibilis.

---

## 1. Mi a kiinduló helyzet most?

A jelenlegi repo-szerkezet alapján a projekt már egy elég jó irányba van szétválasztva:

- `WorldSim.App/` — host, input wiring
- `WorldSim.Graphics/` — render, HUD, camera, overlays
- `WorldSim.Runtime/` — szimuláció, ecology/economy/tech
- `WorldSim.AI/` — leválasztható AI réteg
- `WorldSim.Contracts/` — shared contract boundary
- `WorldSim.RefineryAdapter/` + `WorldSim.RefineryClient/` — külső planner/refinery kapcsolat
- `WorldSim.ScenarioRunner/` — headless futtatási irány

A README és AGENTS alapján ez tudatos moduláris migráció, és fontos boundary szabály, hogy a `Graphics` csak a `Runtime` read-model/snapshot típusaiból dolgozzon, a `Runtime` pedig ne függjön MonoGame-től vagy a grafikai rétegtől.

Ez nagyon fontos, mert pontosan ez teszi lehetővé a későbbi:

- headless futtatást,
- több példányos futtatást,
- külön perf-profilingot,
- render és sim külön optimalizálását,
- és akár később részleges architektúra-cserét is anélkül, hogy a teljes projekt szétesne.

A jelenlegi render oldal már most pass-alapú:

- `TerrainRenderPass`
- `ResourceRenderPass`
- `StructureRenderPass`
- `ActorRenderPass`
- overlay hookok
- `WorldRenderer` mint orchestrator
- `RenderStats` alapok

A Track A dokumentumok szerint már tervben vagy részben folyamatban van:

- `RenderTarget2D` pipeline
- post-fx
- telemetry
- quality profile-ok
- viewport culling
- particle pool/memory caps
- nagyobb mapek kezelése
- cinematic capture flow

A Perf plan alapján már ki van mondva, hogy hiányzik:

- FPS rolling mérés
- sim tick timing
- snapshot build timing
- draw call/entity count stats
- viewport culling
- snapshot builder optimalizáció
- `ScenarioRunner --perf`

Összességében:  
**az alap architektúra jó**, a hiányosság főleg a **vizuális rendszerek mélysége** és a **perf-hardening**.

---

## 2. Mit akarunk pontosan elérni?

A kívánt végállapot nem pusztán “szebb játék”, hanem egy nagyon konkrét profil:

### 2.1 Fő cél
Egy **vizuálisan tiszta, élőnek ható, mégis olcsó 2D világ**, ahol:

- a terep nem steril lapos színmező,
- a világ állapota vizuálisan reagál a szimulációra,
- a mozgás és élet-érzet inkább rendszerből jön, nem asset-hegyekből,
- a gépigény kicsi marad,
- a projekt jó alap marad későbbi AI / simulation / portfolio / demo célokra.

### 2.2 Teljesítmény-cél
A “10 példány fusson egy átlagosan jó laptopon” célhoz nagyon fontos egy tisztázás:

**10 teljes, valós idejű, 1080p-s, effektezett, aktívan rajzoló játékpéldány egyszerre nem jó elsődleges tervezési cél.**  
Nem azért, mert lehetetlen, hanem mert rossz optimalizációs fókuszt ad.

Helyette három működési módot érdemes definiálni:

#### A) Showcase Mode
- 1 példány
- 1080p
- 60 FPS cél
- teljes vizuális réteg
- post-fx opcionálisan bekapcsolható

#### B) Dev Lite Mode
- több ablak/példány
- csökkentett quality profile
- minimális vagy nulla post-fx
- culling aktív
- kisebb viewport / kisebb render scale
- 30 FPS vagy adaptív cap

#### C) Headless / Sim Mode
- sok példány
- nincs render vagy csak minimális export/debug
- `ScenarioRunner`-szerű futás
- tömeges balansz-, AI-, stressz- vagy evolution-tesztekhez

A 10 példányos cél **így reális**:

- 1-2 példány lehet “szép”
- 3-10 példány legyen “lite/dev/headless”

Ez sokkal értelmesebb cél, mint mind a 10 példányt showcase-ra optimalizálni.

---

## 3. Miért nem kell most engine/architektúra váltás?

### 3.1 MonoGame oldalról
A MonoGame hivatalosan ma is aktív, cross-platform .NET framework, és sok ismert játék használta/használja. A hivatalos dokumentáció és a GitHub alapján a jelenlegi latest release a 3.8.4.1, ami fenntartási kiadás a 3.8.4 fölött; a desktop buildjeid szempontjából ez nem jelent olyan változást, ami miatt “az engine zsákutca” lenne. A release note szerint 3.8.4.1-re váltásnál DotNet 9-re kell frissíteni a kliens projektet, és a korábbi `RestoreDotNetTools`-jellegű működés is változott.  

Magyarul: a MonoGame **nem halott platform**, és a jelenlegi WorldSim irányhoz továbbra is használható.

### 3.2 Funkcionálisan is elég
A hivatalos docs alapján MonoGame támogatja:

- `RenderTarget2D` alapú offscreen render + postprocess lánc,
- shader/effect használatot HLSL alapon,
- SpriteBatch-alapú 2D renderelést,
- texture atlas optimalizációt,
- quality profile alapú grafikai rétegezést.

Ez pont elég ahhoz, hogy a WorldSimhez építs:

- olcsó post-fx-et,
- noise/gradient alapú terrain variationt,
- weather / haze / pulse / glint effekteket,
- UI/HUD rétegeket,
- több quality módot,
- screenshot/cinematic flow-t.

### 3.3 A jelenlegi projekted már engine-agnosztikusabb, mint egy tipikus hobby game
A legfontosabb pont nem is maga a MonoGame, hanem az, hogy a runtime már **MonoGame-től leválasztott irányba van tolva**.  
Ez azt jelenti, hogy:

- ha maradsz MonoGame-en, gyorsan tudsz haladni;
- ha később váltanál, kisebb a veszteséged, mint egy monolit projektben.

Tehát most a helyes stratégia nem “azonnal válts”, hanem:

> **előbb vidd ki a maximumot a mostani architektúrából olcsó, nagy hatású rendszerekkel.**

---

## 4. Mikor kellene mégis más architektúrán gondolkodni?

Itt fontos nem romantizálni a mostani setupot.

### 4.1 Maradhatsz MonoGame-en, ha:
- 2D marad a fókusz,
- a világ absztrakt/letisztult,
- a wow-hatás inkább rendszerből, mint shader-orgiából jön,
- az entity-szám “sok, de nem brutális”,
- a render oldal főleg tile/sprite/overlay/postfx jellegű.

### 4.2 Érdemes részleges data-oriented belső refactoron gondolkodni, ha:
- több ezer aktív actor/army/projectile lesz egyszerre,
- a `World.Update()` és az AI loop lesz a fő bottleneck,
- a snapshot builder nagyon sokat másol/allokál,
- a territory/combat/pathfinding tömegesen nő.

### 4.3 Engine-váltás vagy nagy architektúra-váltás akkor indokolt, ha:
- full ECS/DOTS-szerű tömegszimuláció kell,
- sokmagos CPU-k kihasználása a fő bottleneck,
- sokkal erősebb editor/tooling kell,
- vagy a vizuális cél már tényleg Unity/Godot/Unreal-szintű editoros workflow-t kíván.

Őszintén: a WorldSim jelenlegi állapotában **ez még nincs itt**.

---

## 5. A legfontosabb design-döntés: “asset-driven” helyett “state-driven visuals”

A videó lényege szerintem ez volt:

> Nem kézzel csinálok meg 100 dolgot, hanem létrehozok egy rendszert, ami 100 dolgot viselkedéssé és megjelenéssé fordít.

A WorldSimben ennek a megfelelője:

### 5.1 Ne ezt csináld
- külön kézzel rajzolt variáns minden terepre,
- külön kézzel hangolt színpaletta minden eventre,
- sok egyedi sprite-animáció minden entity-típusra,
- sok frame-by-frame effekt,
- túl sok speciális ág a rendererben.

### 5.2 Hanem ezt
A runtime exportáljon olyan állapotmezőket, mint például:

- `Fertility`
- `Moisture`
- `Pollution`
- `Heat`
- `RecoveryProgress`
- `Danger`
- `Activity`
- `OwnershipStrength`
- `PopulationDensity`
- `BiomeBlend`
- `SeasonInfluence`

És a graphics ebből csináljon:

- színátmenetet,
- overlay-t,
- pulse-t,
- noise modulated mintázatot,
- glow/haze intenzitást,
- víz/fü/homok mikro-mozgást,
- világ-gyógyulási átmenetet.

Így a világ látványa nem assetekből, hanem **state-ből** épül.

Ez a kulcs.

---

## 6. Ajánlott cél-architektúra: “Low-Cost Visual Systems Layer”

Itt nem új engine kell, hanem egy új réteg a jelenlegi fölé.

### 6.1 Architektúra-vázlat

```text
+------------------------------------------------------+
| WorldSim.App                                         |
| - host / window / input / quality profile selection |
+-----------------------------+------------------------+
                              |
                              v
+------------------------------------------------------+
| WorldSim.Runtime                                      |
| - simulation, ecology, economy, combat, AI hooks     |
| - computes visual-driving state values               |
+-----------------------------+------------------------+
                              |
                              v
+------------------------------------------------------+
| Snapshot / ReadModel Boundary                        |
| - compact render data                                |
| - perf-friendly export                               |
| - visual driver fields                               |
+-----------------------------+------------------------+
                              |
                              v
+------------------------------------------------------+
| WorldSim.Graphics                                     |
| - pass-based renderer                                |
| - terrain/resource/actor/overlay passes              |
| - low-cost visual systems layer                      |
| - quality-gated post-fx                              |
+-----------------------------+------------------------+
                              |
                              v
+------------------------------------------------------+
| Profiles / Perf / Telemetry                           |
| - Showcase / DevLite / Headless                       |
| - frame stats / draw stats / snapshot stats           |
| - perf budgets / smoke checks                         |
+------------------------------------------------------+
```

### 6.2 A vizuál réteg alrendszerei

Ajánlott alrendszerek:

1. **Terrain State Visualization Layer**
   - tile-state -> palette / tint / pattern / edge blend

2. **Ambient Life Layer**
   - nagyon olcsó mikro-mozgások, pl. víz shimmer, fű sway, activity pulse

3. **Atmosphere Layer**
   - haze, fog, pollution veil, heat tint, recovery glow

4. **Quality Gating Layer**
   - melyik effekt melyik profilban megy

5. **Perf Telemetry Layer**
   - render/sim/snapshot költség mérve és visszacsatolva

---

## 7. Mit érdemes konkrétan hozzáadni a Runtime / Snapshot oldalhoz?

A jelenlegi snapshot alap valószínűleg még túl “szűk” a gazdag, mégis olcsó vizualizációhoz.

### 7.1 Ne túl sok, hanem jó vizuális driver mező kell

Javasolt snapshot-driver mezők tile vagy chunk szinten:

- `Fertility`
- `Moisture`
- `Pollution`
- `RecoveryProgress`
- `Danger`
- `OwnershipStrength`
- `Activity`
- `PopulationDensity`
- `Heat`
- `BiomeBlendPrimary`
- `BiomeBlendSecondary`

Ezek nem “szépészeti extra adatok”, hanem a vizuális rendszer bemenetei.

### 7.2 Miért jó ez?

Mert így a Graphics nem találgat, nem duplikál logikát, nem saját életet él, hanem:

- a runtime dönti el a világállapotot,
- a snapshot átviszi,
- a graphics ezt olcsón és determinisztikusan leképezi.

### 7.3 Fontos tiltás

Ne engedd, hogy a graphics elkezdjen közvetlenül runtime logikából számolni komplex állapotot.  
A vizuál driver mezők a boundary részei legyenek.

---

## 8. Low-cost vizuális technikák, amik nagyon sokat adhatnak

Itt jön a lényeg: hogyan legyen a világ szép **olcsón**.

### 8.1 Noise-alapú terrain variation

A legegyszerűbb high-ROI ötlet.

Ahelyett, hogy minden tile ugyanúgy néz ki:

- seedelt pseudo-random vagy noise alapján enyhe színbeli eltérés,
- brightness/value offset,
- minimális hue drift,
- edge darkening vagy soft border,
- biome-tól függő textúra-intenzitás.

Ez CPU-side is megoldható első körben, drága shader nélkül.

### 8.2 Palette-driven world states

Pl. ugyanaz a terrain más palette-réteget kap:

- egészséges/élő
- kiszáradt
- szennyezett
- visszagyógyuló
- veszélyzóna

A világ állapotát nem új textúrákkal, hanem **paletta- és overlay-logikával** mutatod.

### 8.3 Overlay mintázatok

Olcsó és látványos lehet:

- pollution veil
- moisture sheen
- recovery pulse
- territory contour
- influence field

Ezek lehetnek egyszerű alpha-texture vagy procedurális mintázat alapú rétegek.

### 8.4 Mikro-animációk

Nem teljes animáció kell, hanem kis életjelek:

- víz enyhe UV-szerű eltolás vagy frame offset
- fű/mező enyhe brightness oscillation
- recoverelő világ nagyon finom pulse
- structure aura intensity modulation

Az egész világ “élőbbnek” hat tőlük.

### 8.5 Egyszerű atmoszféra

MonoGame-en belül nagyon olcsó lehet:

- full-screen haze overlay,
- day/night tint,
- danger zone red veil,
- clean-air / polluted-air gradient,
- weather-like soft screen-space réteg.

### 8.6 Egyszerű, de erős silhouette design

A low-cost világ egyik legerősebb trükkje nem a részletgazdagság, hanem a **jó formanyelv**.

Ha a structure/resource/actor silhouette-ek tiszták:
- akkor kevesebb részlet is elég,
- és olcsóbb a rajzolás / asset-karbantartás.

---

## 9. Konkrét quality profile stratégia

Ezt nagyon erősen javaslom formalizálni.

## 9.1 Profilok

### Showcase
- teljes felbontás
- post-fx engedélyezhető
- terrain variation max
- atmoszféra rétegek teljesebbek
- screenshot/capture mód támogatott

### DevLite
- csökkentett render scale opció
- minimal post-fx
- egyszerűsített overlay-k
- culling agresszívebb
- target: több ablak / több példány

### Headless
- nincs render
- vagy csak minimális debug output
- target: sok párhuzamos szimuláció

## 9.2 A legfontosabb szabály

> **A default fejlesztői alap ne a Showcase legyen.**

Ez tipikus csapda.

A napi fejlesztés és a többszörös példány-futtatás alapja inkább `DevLite` legyen.  
A Showcase legyen tudatosan külön mód.

---

## 10. Perf stratégia: mit kell mérni ténylegesen?

A low-cost cél csak akkor valós, ha mérve van.

### 10.1 Minimum mérendő adatok

Mindenképp kellene:

- frame time (avg / p95 / rolling)
- sim tick time
- snapshot build time
- draw count / pass count
- visible tile count
- visible actor/resource/structure count
- allocations (ha tudod legalább közelítően)
- ScenarioRunner batch run timing

### 10.2 Perf budget példa

Ez nem kőbe vésett, csak irány:

#### Showcase
- 16.6 ms körül cél 60 FPS-hez
- render budget lazább lehet, ha csak 1 példány fut

#### DevLite
- 30 FPS is elég lehet
- cél inkább stabilitás és kis költség

#### Headless
- a render budget lényegében nulla
- itt sim throughput a lényeg

### 10.3 Különösen fontos fejlesztések

- viewport culling
- snapshot export költség csökkentése
- object pooling transient effektekre
- pass-level telemetry
- ScenarioRunner perf mode

---

## 11. A “10 példány” cél realista bontása

Legyünk őszinték.

### 11.1 Mi számít itt sikernek?

Szerintem nem az, hogy 10 darab full szép build fusson egyszerre 60 FPS-sel, hanem hogy:

- 1-2 darab Showcase stabilan menjen,
- 3-5 darab DevLite kényelmesen menjen,
- 10+ darab Headless / minimal-debug batch fusson tesztekre.

Ez már nagyon erős rendszer-szintű eredmény lenne.

### 11.2 Miért jobb ez?

Mert:
- valódi fejlesztői workflow-t támogat,
- AI/balance testekhez praktikus,
- nem pazarolsz erőforrást arra, hogy minden mód ugyanazt tudja,
- sokkal jobban illik a projekted céljához.

---

## 12. Mi lenne a legjobb költség/haszon arányú roadmap?

Itt a legfontosabb rész.

## Phase 0 — Instrumentáció és profilok

**Cél:** előbb lásd, mi történik.

### Tedd be:
- quality profile rendszer (`Showcase`, `DevLite`, `Headless`)
- perf HUD / debug overlay
- frame/sim/snapshot timing
- visible counts
- pass timing
- ScenarioRunner perf mode alap

### Miért első?
Mert különben vakon optimalizálsz.

---

## Phase 1 — State-driven terrain & atmosphere

**Cél:** a világ olcsón legyen sokkal élőbb.

### Tedd be:
- tile-state driver mezők snapshotba
- palette-driven terrain tint
- noise-based variation
- pollution/moisture/recovery overlay logika
- egyszerű atmosphere layer
- viewport culling

### Eredmény:
Már itt látványosan jobb lesz a világ, nagy asset-költség nélkül.

---

## Phase 2 — Ambient life & transient effect economy

**Cél:** legyen mozgás/élet-érzet olcsón.

### Tedd be:
- mikro-animációs pulse-ok
- simple water shimmer / growth pulse / aura
- projectile/impact/transient pool
- optional particles quality gate mögött

### Eredmény:
A világ “lélegzik”, nem csak statikus térkép.

---

## Phase 3 — Advanced optimization

**Cél:** ha már kell.

### Tedd be:
- chunk-level snapshot export optimalizáció
- data-oriented belső hot path refactor
- pathfinding/combat profiling
- actor batching ahol érdemes
- optional shader path a CPU-side baseline fölé

### Fontos
Ez már csak akkor kell, ha tényleg a runtime lesz a limit.

---

## 13. Mik a legnagyobb technikai kockázatok?

### 13.1 Túl korai shader-romantika

Könnyű elcsúszni abba, hogy “majd egy menő shader megoldja”.  
De a WorldSim low-cost céljához első körben valószínűleg a CPU-side, egyszerű, jól kontrollált vizuális logika jobb.

### 13.2 Graphics elkezd gameplay-logikát számolni

Ez boundary-roncsolás lenne.  
A graphics vizualizáljon, ne szimuláljon.

### 13.3 Minden egyszerre legyen szép ÉS ultraolcsó

Ebből lesz a rossz default.  
Külön profilokkal kell ezt kezelni.

### 13.4 Túl sok state field egyszerre

A vizuál driver mezők legyenek indokoltak.  
Nem kell 50 új mező, inkább 6–12 jó.

### 13.5 Perf mérés hiánya

A “szerintem gyors” nem stratégia.  
Evidence kell.

---

## 14. Mit csinálnék én a te helyedben most?

Ha most nekem kellene irányt mondani, ezt választanám:

### Első döntés
**Maradnék MonoGame-en.**  
Nem váltanék most se Unityre, se Godotra, se teljes ECS rewrite-ra.

### Második döntés
**A WorldSim következő nagy témája nem pusztán visual polish lenne, hanem a “Low-Cost Visual Systems Layer”.**

### Harmadik döntés
Három profilt vezetnék be explicit módon:
- `Showcase`
- `DevLite`
- `Headless`

### Negyedik döntés
A következő nagy implementációs blokk:
- snapshot driver mezők,
- state-driven terrain rendering,
- culling,
- perf telemetry,
- transient pool.

Ez adná szerintem a legnagyobb előrelépést a legjobb költség/haszon aránnyal.

---

## 15. Végső összegzés

A WorldSim jelenlegi architektúrája **alkalmas** arra, hogy ráépíts egy nagyon erős, letisztult, olcsó 2D világot.  
Nem az a kérdés, hogy “rá lehet-e húzni”, hanem inkább az, hogy **milyen fegyelmezetten** húzod rá.

A helyes irány szerintem ez:

- **marad a MonoGame**,
- **marad a moduláris Runtime/Graphics szétválasztás**,
- **jön egy state-driven, low-cost vizuális réteg**,
- **jön explicit quality profile stratégia**,
- **jön bizonyíték-alapú perf mérés**,
- és **csak akkor** gondolkodsz mélyebb architektúra-váltásban, ha már a runtime/hot path tényleg plafonba ütközik.

Ez nem csak technikailag reális, hanem portfólió- és projekt-identitás szempontból is erős.

Mert a WorldSim igazi ereje nem az lesz, hogy “tele van effektekkel”, hanem az, hogy:

> **kevésből sokat mutat, és a világ szépsége a rendszerből jön.**

---

## 2. Integration Prompt Pack

# WorldSim Low-Cost 2D Integration Prompt Pack

Dátum: 2026-03-10

Ez a fájl azt a célt szolgálja, hogy a `WorldSim_Low_Cost_2D_Architecture_Plan_2026-03-10.md` terv ne csak különálló gondolat maradjon, hanem be legyen húzva a meglévő WorldSim koordinációs rendszerbe:

- projekt-szintű Meta Coordinator
- Combat Coordinator
- Track A / B / C / D sessionök
- kapcsolódó sessionök: Performance Profiling, Balance/QA

A promptok úgy vannak megírva, hogy kompatibilisek legyenek a projekt mostani, dokumentum-vezérelt működésével: `AGENTS.md`, `Docs/Plans/*`, Track-planek, Combined Execution Sequencing logika.

---

## 0. Repo-integráció

### 0.1. Hova kerüljön a tervfájl?

Ajánlott célhely:

`Docs/Plans/Master/world_sim_low_cost_2_d_docs.md`

Miért ide:
- a többi stratégiai és session szintű terv is a `Docs/Plans/` alatt van,
- a Meta Coordinator runbook és a sessionök innen hivatkoznak tervekre,
- így a low-cost 2D terv nem „külön melléklet”, hanem hivatalos projektterv lesz.

### 0.2. Mely meglévő fájlokat kell első körben frissíteni?

Minimum:

1. `AGENTS.md`
   - új kereszt-hivatkozás a low-cost 2D tervre
   - uzenőfal entry arról, hogy ez lett a vizuális/perf és multi-instance irány egyik referencia terve

2. `Docs/Plans/Meta-Coordinator-Runbook.md`
   - új workflow vagy meglévő workflow bővítés:
     - `agents-maintenance` alatt a low-cost terv hivatkozásának ellenőrzése
     - `project-futures` alatt a low-cost vizuális/perf backlog szintetizálása
     - opcionálisan új workflow: `low-cost-2d-sync`

3. `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md`
   - a wow/portfolio célok mellé explicit low-cost/default profile policy
   - különválasztás: `Showcase` vs `DevLite`

4. `Docs/Plans/Session-Perf-Profiling-Plan.md`
   - a low-cost tervből performance budgetek és profile-ok átvezetése
   - perf-check workflow kimenetéhez igazítás

5. `Docs/Plans/Session-Balance-QA-Plan.md`
   - doc drift javítása: a fájl régi baseline-ja szerint a ScenarioRunner még nem tud több dolgot, miközben a Wave 3.6 szerint már van structured matrix runner, clustering telemetry és multi-planner/multi-config output

### 0.3. Erősen ajánlott extra fájl

Új fájl:

`Docs/Plans/Low-Cost-2D-Integration-Runbook.md`

Célja:
- rövid, operatív összekötő dokumentum legyen a nagy terv és a napi track/session munka között,
- ne kelljen minden sessionnek végigolvasnia az egész nagy architecture plant,
- legyen benne: célprofilok, DoD, tiltások, escalation rule-ok.

---

## 1. Projekt-szintű alapelv, amit minden sessionnek át kell vennie

A low-cost 2D tervből a legfontosabb közös elv:

> A WorldSim alapvető iránya: `Runtime számol állapotot -> Snapshot exportál -> Graphics szabályalapúan vizualizál -> Quality/Profile/Perf layer skálázza`.

Ennek következménye:
- nincs engine-váltás most,
- nincs korai ECS/DOTS rewrite,
- az elsődleges fókusz: snapshot-barát, olcsó, state-driven vizuál + bizonyíték-alapú perf mérés,
- a multi-instance célt profile-okkal és headless/devlite módokkal kell megfogni, nem minden buildre ráerőltetett showcase effektekkel.

---

## 2. Prompt a projekt-szintű Meta Coordinatorhoz

Használd ezt, amikor a fő Meta Coordinator sessiont nyitod meg.

```text
Olvasd be teljesen ezeket a fájlokat és kezeld őket közös forrásként:
- AGENTS.md
- Docs/Plans/Meta-Coordinator-Runbook.md
- Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md
- Docs/Plans/Session-Perf-Profiling-Plan.md
- Docs/Plans/Session-Balance-QA-Plan.md
- Docs/Plans/Session-Combat-Coordinator-Plan.md
- Docs/Plans/Master/world_sim_low_cost_2_d_docs.md
- WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md
- WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Sprint3-Execution-Plan.md

Feladatod nem production kód írása, hanem projekt-szintű szinkronizáció.

Cél:
Integráld a low-cost 2D architecture tervet a meglévő WorldSim dokumentumrendszerbe úgy, hogy:
1. ne sérüljön a jelenlegi Track A/B/C/D és Combat Coordinator workflow,
2. a terv hivatkozott, elsőrendű stratégiai referenciává váljon,
3. a doc driftet azonosítsd és javítsd,
4. a sessionök számára egyértelmű legyen, mi változik mostantól prioritásban.

Konkrét teendők:
- Javasolj végleges repo-helyet a low-cost tervfájlnak, és ha hiányzik a repo-ból, készíts beilleszthető patch-tervet.
- Frissítsd vagy javasold frissítésre az AGENTS.md-t úgy, hogy a low-cost 2D terv explicit referenciává váljon.
- Azonosítsd, mely session tervek elavultak a Wave 3.6 fényében, különösen ahol a ScenarioRunner képességeiről már nem aktuális állítás szerepel.
- Készíts egy rövid “Low-Cost 2D Sync Report” markdown jelentést ezekkel:
  - mi kompatibilis már most,
  - mi lett részben megvalósítva,
  - mely docok szorulnak frissítésre,
  - milyen új workflow vagy szabály kell a runbookba.
- Ha indokolt, javasolj új runbook workflow-t `low-cost-2d-sync` néven.

Korlátok:
- Ne tervezz engine-váltást.
- Ne javasolj ECS/DOTS rewrite-ot közeli lépésként.
- Tartsd tiszteletben a snapshot boundary-t és a declared dependency graphot.
- A wow/portfolio célokat külön kezeld a low-cost/default futási profiltól.

Output formátum:
1. rövid állapotkép,
2. doc drift lista,
3. javasolt fájlmódosítások,
4. beilleszthető AGENTS.md / runbook patch javaslatok.
```

---

## 3. Prompt a második “meta coordinatorhoz” = Combat Coordinatorhoz

Ez a koordinátor nem az egész projektre, hanem a Combat Master Plan és a trackek közti szinkronra fókuszál.

```text
Olvasd be teljesen ezeket a fájlokat:
- AGENTS.md
- Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md
- Docs/Plans/Session-Combat-Coordinator-Plan.md
- Docs/Plans/Session-Perf-Profiling-Plan.md
- Docs/Plans/Session-Balance-QA-Plan.md
- Docs/Plans/Master/world_sim_low_cost_2_d_docs.md
- WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md
- WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Sprint3-Execution-Plan.md

Feladat:
Szinkronizáld a Combat Coordinator működését a low-cost 2D architecture tervvel.

A cél nem az, hogy a combat tervet lecseréld, hanem hogy minden jövőbeli combat sprintnél figyelembe vedd ezeket:
- a default build legyen olcsó és stabil,
- a showcase / cinematic / postfx út külön profile-on fusson,
- új snapshot mezők és combat overlay-ek csak snapshot-forward-compatible módon menjenek be,
- a perf és balance gate ne egyetlen manuális runra támaszkodjon, hanem a jelenlegi ScenarioRunner matrix + telemetry infrastruktúrára,
- ha egy combat feature veszélyezteti a multi-instance / devlite / headless célokat, azt explicit kockázatként kezeld.

Konkrét teendők:
- Készíts “Combat x Low-Cost Alignment” jelentést.
- Sorold fel, hogy Track A/B/C/D combat feladatai közül melyek kompatibilisek már most a low-cost tervvel.
- Adj sprint-gate kiegészítéseket a Combat Coordinator checklisthez:
  - perf-check,
  - snapshot Delta ellenőrzés,
  - low-cost profile regresszió figyelés,
  - ScenarioRunner evidence-alapú összevetés.
- Ha kell, javasolj pontos szöveget a Session-Combat-Coordinator-Plan.md frissítéséhez.

Korlátok:
- Ne nyiss új grand plan-t a meglévő Combat Master Plan helyett.
- Ne told össze a showcase és low-cost célokat egyetlen default profillá.
- Minden javaslat maradjon a meglévő Track A/B/C/D struktúrán belül.
```

---

## 4. Prompt Track A sessionhöz

Track A a legfontosabb, mert a low-cost 2D terv jelentős része itt csapódik le.

```text
Olvasd be teljesen ezeket:
- Docs/Plans/Master/world_sim_low_cost_2_d_docs.md
- WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md
- WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Sprint3-Execution-Plan.md
- WorldSim.Graphics/Docs/Plans/Phase1-Sprint3-Smoke-Checklist.md
- AGENTS.md
- WorldSim.Graphics/Rendering/* releváns részei
- WorldSim.App/GameHost.cs

Feladatod:
Szinkronizáld a Track A vizuális tervet a low-cost 2D architecture tervvel.

Fókusz:
- a renderer maradjon snapshot-driven és pass-alapú,
- a default profil legyen olcsó,
- a showcase profil maradhat látványosabb,
- minden postfx / bloom / vignette / haze / cinematic elem quality-gated és fallback-barát legyen,
- a következő nagy érték ne csak “még több polish” legyen, hanem state-driven 2D vizuális rendszerek bevezetése.

Konkrétan vizsgáld meg és javasolj tervfrissítést ezekre:
1. `Showcase` vs `DevLite` vs `Headless` profil logika
2. viewport culling mint kötelező low-cost baseline
3. tile-state-driven terrain variation
4. olcsó CPU-side variation a drága shaderes út helyett első körben
5. settings overlay és smoke checklist kiegészítése low-cost regresszió ellenőrzésekkel
6. particle/bloom/grain explicit optional státusza

Output:
- rövid Track A alignment report,
- patch-javaslat a Track-A plan dokumentum(ok)hoz,
- top 5 implementációs prioritás a low-cost cél szemszögéből,
- külön jelöld: mi immediate, mi later.

Korlátok:
- Ne javasolj engine-váltást.
- Ne vezesd be a drágább shaderes/material rendszert mint kötelező alapot.
- A wow-cél maradjon secondary/default-on-top-of-low-cost, ne fordítva.
```

---

## 5. Prompt Track B sessionhöz

```text
Olvasd be teljesen ezeket:
- Docs/Plans/Master/world_sim_low_cost_2_d_docs.md
- Docs/Plans/Session-Perf-Profiling-Plan.md
- Docs/Plans/Session-Balance-QA-Plan.md
- Docs/Plans/Track-B-Prep-Roadmap.md
- AGENTS.md
- WorldSim.Runtime/ReadModel/*
- WorldSim.Runtime/Simulation/* releváns részei
- WorldSim.ScenarioRunner/Program.cs

Feladatod:
Igazítsd a Track B runtime/snapshot/perf irányt a low-cost 2D tervhez.

Elsődleges célok:
- a renderbarát snapshot boundary erősítése,
- a state-driven vizuálhoz szükséges mezők tiszta exportja,
- a perf és evidence infrastruktúra rendezése,
- a multi-instance / headless / devlite célok támogatása.

Konkrét teendők:
1. Vizsgáld meg, mely runtime state-eket érdemes explicit vizuális driver mezőkké emelni a snapshotban (pl. fertility, pollution, recovery, activity, tension, ecology pressure).
2. Készíts javaslatot snapshot mezőbővítésre úgy, hogy Track A olcsón tudjon belőle dolgozni.
3. Egyeztesd a low-cost terv `ScenarioRunner --perf / --json / multi-config matrix` részeit a mostani állapottal.
4. Azonosítsd a már megvalósult részeket és a terv elavult pontjait.
5. Javasolj frissítést a perf/balance docokhoz, különösen ahol a Wave 3.6 miatt doc drift van.

Output:
- Track B alignment report,
- snapshot mező-javaslatok,
- perf/balance doc drift lista,
- priorizált runtime backlog (small / medium / large).

Korlátok:
- Ne pusholj teljes architektúraváltást.
- Ne keverj mutable runtime állapotot közvetlenül a graphics réteghez.
- Minden új mező indokolt legyen konkrét low-cost vizuális vagy perf célból.
```

---

## 6. Prompt Track C sessionhöz

```text
Olvasd be teljesen ezeket:
- Docs/Plans/Master/world_sim_low_cost_2_d_docs.md
- Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md
- AGENTS.md
- a Track C releváns tervfájljai
- WorldSim.AI/* releváns részei
- WorldSim.RefineryAdapter/* és kapcsolódó planner integrációs részek

Feladatod:
Igazítsd a Track C / AI / planner / adapter irányt a low-cost 2D tervhez.

A cél itt nem vizuális feature gyártása, hanem annak biztosítása, hogy az AI/planner oldali változtatások ne tegyék nehezebbé:
- a snapshot boundary tisztaságát,
- a headless / multi-instance batch futtatást,
- a perf evidence gyűjtést,
- a determinisztikus vagy legalább összehasonlítható ScenarioRunner outputot.

Konkrét teendők:
- Vizsgáld meg, hogy a planner/AI output mely részei hathatnak a vizuális state-driver mezőkre.
- Sorold fel, milyen adapter vagy contract változtatások okozhatnak low-cost regressziót.
- Készíts rövid alignment note-ot arról, hogy a Track C roadmap hogyan maradhat kompatibilis a Showcase/DevLite/Headless profil stratégiával.
- Ha indokolt, javasolj telemetry vagy planner-output annotációt, amely segíti a perf/balance összehasonlítást.

Output:
- Track C alignment note,
- kockázatlista,
- javasolt guardrail-ek.

Korlátok:
- Ne terjeszd túl a scope-ot grafikai implementációra.
- A fókusz kompatibilitás és futtathatóság legyen.
```

---

## 7. Prompt Track D sessionhöz

Track D nálad jellemzően az integratív / “director / master plan / compatibility” jellegű vonalhoz kapcsolódik, ezért itt a fókusz a teljes világ-koherencia.

```text
Olvasd be teljesen ezeket:
- Docs/Plans/Master/world_sim_low_cost_2_d_docs.md
- Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md
- AGENTS.md
- a Track D releváns master plan és sequencing dokumentumai
- WorldSim.Runtime / WorldSim.Contracts releváns részei

Feladatod:
Integráld a low-cost 2D architecture tervet a Track D / master-plan kompatibilitási logikába.

Különösen figyelj erre:
- a low-cost vizuális stratégia ne legyen külön sziget, hanem a projekt egészének egyik guiding constraintje,
- az új feature-szekvenciák legyenek kompatibilisek a Showcase / DevLite / Headless profilrendszerrel,
- a sequencing logikában jelenjen meg, hogy mi baseline és mi optional polish,
- a state-driven visual mezők backlogja ne legyen összekeverve a sim core és AI core feladataival.

Konkrét teendők:
- Adj sequencing javaslatot arra, mikor kerüljenek be a low-cost 2D terv részei.
- Jelöld, mi prerequisite, mi parallelizálható, mi függ perf evidence-től.
- Készíts rövid “Track D x Low-Cost Strategy Note” dokumentumot.

Output:
- sequencing javaslat,
- dependency/parallelization note,
- master-plan kompatibilitási megjegyzések.

Korlátok:
- Ne generálj új, a meglévő fölé ülő meta-master-plant.
- A cél integráció, nem teljes újrakeretezés.
```

---

## 8. Prompt Performance Profiling sessionhöz

```text
Olvasd be teljesen ezeket:
- Docs/Plans/Master/world_sim_low_cost_2_d_docs.md
- Docs/Plans/Session-Perf-Profiling-Plan.md
- AGENTS.md
- WorldSim.App/GameHost.cs
- WorldSim.Graphics/Rendering/* releváns részei
- WorldSim.ScenarioRunner/Program.cs

Feladatod:
Frissítsd a performance profiling tervet a low-cost 2D architecture plan alapján.

Cél:
- legyen explicit külön perf target a Showcase / DevLite / Headless profilokra,
- kerüljön be a frame/sim/snapshot/pass telemetry minimumcsomag,
- a culling, transient pooling és render target stratégia ne csak ötletként, hanem mérési backlogként jelenjen meg,
- legyen világos, hogy a “10 instance” cél milyen profilkeveréssel értelmezendő.

Konkrét outputot adj:
1. perf plan patch-javaslat,
2. minimum telemetry checklist,
3. performance budget skeleton,
4. regresszió-ellenőrzési javaslatok.

Korlátok:
- Ne csak manuális profilingra építs.
- Támaszkodj a ScenarioRunner evidence útvonalaira is.
```

---

## 9. Prompt Balance / QA sessionhöz

```text
Olvasd be teljesen ezeket:
- Docs/Plans/Master/world_sim_low_cost_2_d_docs.md
- Docs/Plans/Session-Balance-QA-Plan.md
- Docs/Plans/Session-Perf-Profiling-Plan.md
- AGENTS.md
- WorldSim.ScenarioRunner/Program.cs
- a legfrissebb Wave 3.6 / telemetry / matrix-run releváns dokumentumok

Feladatod:
Frissítsd a Balance/QA tervet a low-cost 2D architecture terv és a legfrissebb ScenarioRunner képességek alapján.

Különösen:
- javítsd a doc driftet ott, ahol a terv még régi baseline-t feltételez,
- vezesd be a profile-aware QA gondolkodást (`Showcase`, `DevLite`, `Headless`),
- különítsd el a vizuális regresszió, perf regresszió és sim/balance regresszió fogalmát,
- használd a structured matrix runner / telemetry export / clustering output lehetőségeit evidence-ként.

Output:
- doc drift lista,
- patch-javaslat a Session-Balance-QA-Plan.md-hez,
- QA checklist bővítés profile-aware módon,
- javasolt regression matrix.

Korlátok:
- Ne kezeld a low-cost tervet pusztán vizuális dokumentumként; ez perf- és workflow-constraint is.
```

---

## 10. Ajánlott futtatási sorrend

Ha most akarod ezt valósan beintegrálni a sessionökbe, ezt a sorrendet javaslom:

1. **Fő Meta Coordinator**
   - hivatalos repo-integráció
   - doc drift feltárás
   - runbook/AGENTS frissítési javaslat

2. **Track A**
   - mert itt csapódik le először a legtöbb kézzelfogható haszon

3. **Track B**
   - snapshot/state-driver/perf alignment

4. **Performance Profiling**
   - hogy ne elméleti low-cost terv maradjon

5. **Balance/QA**
   - profile-aware regressziós gondolkodás

6. **Combat Coordinator**
   - combat sprint gate-ek és profile-risk alignment

7. **Track C / Track D**
   - integrációs és sequencing finomhangolás

---

## 11. Rövid záróelv, amit akár AGENTS.md-be is be lehet emelni

Ajánlott rövid szöveg:

> A WorldSim vizuális és teljesítmény-stratégiája a `Docs/Plans/Master/world_sim_low_cost_2_d_docs.md` alapján state-driven, snapshot-barát, profile-aware irányt követ. A default fejlesztői baseline nem a showcase polish, hanem az olcsó és stabil futás; a látványosabb effekt- és capture-utak külön quality/profile rétegen keresztül épülnek rá erre az alapra.

Ez tömören összefoglalja a lényeget.
