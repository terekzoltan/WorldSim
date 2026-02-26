# Meta Coordinator Runbook

> Ez a dokumentum a WorldSim projekt Meta Coordinator chat/session reszletes munkafolyamatait irja le.
> A Meta Coordinator egyetlen celja: a parhuzamosan dolgozo AI agensek kozotti szinkronizacio,
> a projekt-szintu konzisztencia es a strategiai iranyitas biztositasa.
>
> Referencia tabla: `AGENTS.md` -> `Meta Coordinator Workflows` szekció.

---

## Altalanos szabalyok

- A Meta Coordinator **nem ir production kodot**. Kizarolag dokumentaciot, konfigot es audit-reportokat keszit.
- Minden workflow outputja vagy AGENTS.md edit, vagy markdown report a felhasznalo szamara.
- A workflow-k egyenkent is futtathatoak, vagy egyutt (`full-sweep`).
- Git allapotra **NEM** tamaszkodunk (messy repo); helye: fajlok tenyleges tartalma a lenyeg.

---

## 1. `agents-maintenance`

**Cel:** AGENTS.md naprakeszen tartasa -- a tobbi agens ebbol tajekozodik.

**Mikor:** Minden session elejen (a Meta Coordinator chat megnyitasakor).

**Lepesek:**

1. Olvasd be `AGENTS.md` teljes tartalmat.
2. **Track statuszok ellenorzese:**
   - Nezdeld at a `Trackok (A-D)` szekciot.
   - Ha barmely `Allapot` sor elavult (pl. blokkolo megoldodott, vagy track kesz lett), frissitsd.
3. **Uzenofal trimmelese:**
   - Szamold meg az `Entries` lista elemet.
   - Ha >10, torold a legregebbieket (felulrol) ugy, hogy pontosan 10 maradjon.
4. **Elavult hivatkozasok:**
   - Keress `Game1`-re (mar `GameHost`), vagy mas ismert atnevezes.
   - Keress torott plan-fajl hivatkozasokra (glob: `Docs/Plans/*.md`, `WorldSim.*/Docs/Plans/*.md`).
5. **Output:** AGENTS.md edit (ha volt valtozas).

---

## 2. `arch-audit`

**Cel:** A deklaralt dependency graph vs. tenyleges .csproj referenciak osszevetes.

**Mikor:** Hetente, vagy PR-review / nagy merge elott.

**Lepesek:**

1. Olvasd be `AGENTS.md` -> `Dependency graph` szekciot. Jegyezd meg az engedelyezett es tiltott iranyokat.
2. Gyujtsd ki az osszes `.csproj` fajlt:
   - `WorldSim.App/*.csproj`
   - `WorldSim.Graphics/*.csproj`
   - `WorldSim.Runtime/*.csproj`
   - `WorldSim.AI/*.csproj`
   - `WorldSim.Contracts/*.csproj`
   - `WorldSim.RefineryAdapter/*.csproj`
   - `WorldSim.RefineryClient/*.csproj`
3. Minden `.csproj`-ban keresd a `<ProjectReference>` elemeket.
4. Vesd ossze az engedelyezett iranyokkal:
   - **OK:** Ha a referencia az engedelyezett listaban van.
   - **VIOLATION:** Ha tiltott iranyba mutat (pl. `Runtime -> Graphics`).
   - **UNLISTED:** Ha a referencia sem engedelyezett, sem tiltott (uj fuggoseg?).
5. **Output:** Report formatum:

```
## Arch Audit Report - [datum]

### OK (engedelyezett referenciak)
- WorldSim.App -> WorldSim.Graphics ✓
- ...

### VIOLATIONS (tiltott referenciak!)
- [nincs / felsorolas]

### UNLISTED (ismeretlen, vizsgalando)
- [nincs / felsorolas]
```

Ha VIOLATION talalhato, uzenofal bejegyzes is kotelezo.

---

## 3. `dod-check`

**Cel:** Track Definition of Done kriteriumok tenyleges teljesuleset ellenorizni.

**Mikor:** Sprint vegen vagy merfoldo elott.

**Lepesek:**

1. Olvasd be minden Track `Definition of Done` listajat az AGENTS.md-bol.
2. Kriteriumonkent ellenorizd a kodbazist:
   - **Track A:** "F1/F6 UI flow nem torik" -> nezd meg, letezik-e `GameHost` es a render pipeline kulon osztalyokban.
   - **Track B:** "Runtime buildel standalone" -> probald: `dotnet build WorldSim.Runtime/WorldSim.Runtime.csproj` (ha lehetseges).
   - **Track B:** "Runtime nem fugg RefineryClient-tol" -> arch-audit ezen pontjat ellenorizd.
   - **Track C:** "AI modul kulon tesztelheto" -> letezik-e `WorldSim.AI` testproject vagy tesztelheto-e izolaltan.
   - **Track D:** "Unknown op/tech determinisztikus hiba" -> nezd meg van-e error handling a RefineryAdapter-ben.
3. **Output:** DoD matix:

```
## DoD Progress - [datum]

### Track A - Graphics/UI
- [x] GameHost csak host, render kulon osztalyokban
- [ ] F1/F6 UI flow tesztelve
- [x] Zoom/pan mukodik

### Track B - Runtime Core
- [x] Runtime buildel standalone
- [ ] Snapshot alapjan gameplay kirajzolhato
- ...
```

---

## 4. `plans-review`

**Cel:** Docs/Plans dokumentumok konzisztenciajat vizsgalni AGENTS.md-vel.

**Mikor:** Fazisvaltas eseten, vagy ha uj plan keszult.

**Lepesek:**

1. Gyujtsd ossze az osszes plan fajlt:
   - `Docs/Plans/*.md`
   - `WorldSim.*/Docs/Plans/*.md`
2. Minden plan fajlra:
   - Van-e ra hivatkozas AGENTS.md-ben? Ha nincs, jelold "arva plan"-nak.
   - AGENTS.md hivatkozik-e nem letezo plan-ra? Ha igen, jelold "torott hivatkozas"-nak.
3. Tartalmi konzisztencia:
   - A plan-ben emlitett fazis/sprint megegyezik az AGENTS.md "Current focus" szekcioval?
   - Vannak-e ellentmondas a plan es az AGENTS.md Track scope kozott?
4. **Output:** Konzisztencia report + javitasi javaslatok.

**Ismert plan fajlok (referencia):**
- `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md` -- Track A Phase 1, 3 sprint
- `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Sprint3-Execution-Plan.md` -- Track A Sprint 3 reszletek
- `WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md` -- Track D Season Director
- `Docs/Plans/Track-C-Prep-CrossTrack-Handshake.md` -- Track C cross-track contract
- `Docs/Plans/Track-C-AI-Context-Contract-v1.md` -- Track B->C AI context contract
- `Docs/Plans/Track-B-Prep-Roadmap.md` -- Track B prep roadmap
- `Docs/Plans/Combat-Defense-Campaign-Master-Plan.md` -- Jovobeni combat/campaign master plan
- `WorldSim.Runtime/Docs/Plans/TODO.md` -- Runtime korai TODO (legacy)
- `Docs/Plans/Session-Combat-Coordinator-Plan.md` -- Combat Coordinator session playbook
- `Docs/Plans/Session-Perf-Profiling-Plan.md` -- Performance Profiling session plan
- `Docs/Plans/Session-Balance-QA-Plan.md` -- Balance/QA Agent session plan
- `Docs/Plans/Track-C-Phase0-Preflight-Checklist.md` -- Track C Phase 0 preflight checklist
- `Docs/Plans/Track-C-Combat-Masterplan-Readiness-Roadmap.md` -- Track C combat readiness roadmap

---

## 5. `conflict-scan`

**Cel:** Cross-track utkozesek korai felderitese, mielott build-break tortenne.

**Mikor:** Hetente, vagy ha >3 uj uzenofal bejegyzes erkezett.

**Lepesek:**

1. Olvasd be az uzenofal `Entries` listat.
2. Azonositsd az atfedo temakoroket:
   - Ugyanazt a fajlt/tipust/contractot erinti tobb track? (pl. `WorldRenderSnapshot`, `RenderFrameContext`, AI context mezok)
   - Ugyanazt az interfeszt vagy configot modositja tobb track?
3. Mintazat-kereses a kodbazisban:
   - Ha ket track ugyanazt a namespace-t vagy tipust emliti, ellenorizd a tenyleges fajlt.
4. **Output:** Figyelmeztetes lista:

```
## Conflict Scan - [datum]

### Potencialis utkozesek
- ⚠️ Track A (snapshot shape) vs Track B (snapshot combat fields): mindketto WorldRenderSnapshot-ot boviti
  -> Javaslat: egyeztessek az uj mezok sorrendjet, egy PR-ben vigyak be

### Nem talalt utkozes
- Track C / Track D: jelenleg fuggetlen munkaagak
```

---

## 6. `sprint-plan`

**Cel:** Sprint closeout osszefoglalo + kovetkezo sprint scope javaslat.

**Mikor:** Sprint hataron (jellemzoen 1-2 hetente).

**Lepesek:**

1. Olvasd be minden Track "Current focus" / "Current Track X focus" szekciot az AGENTS.md-bol.
2. Olvasd be az uzenofal legutobbi bejegyzeseit (utolso sprint idoszakara vonatkozo entryk).
3. Olvasd be a relevans plan fajlokat, kulonosen a "Sprint N" szekciokat.
4. **Closeout osszefoglalo:**
   - Track-enkent: mi keszult el, mi maradt nyitva, mi lett blokkolt.
   - Cross-track: milyen uj fuggosegek merultek fel.
5. **Kovetkezo sprint scope javaslat:**
   - Track-enkent: mi a kovetkezo 3-5 legfontosabb feladat.
   - Milyen cross-track koordinacio szukseges.
6. **Output:**
   - Closeout osszefoglalo (markdown report).
   - AGENTS.md "Current focus" szekciok frissitese (ha elfogadta a felhasznalo).

---

## 7. `onboarding-snapshot`

**Cel:** Friss, tomor allapotleiras generalasa egy uj agens vagy ember szamara.

**Mikor:** On-demand, ha valaki uj track-re csatlakozik, vagy ha kontextus kell egy mas chatben.

**Input:** Track azonosito: `A`, `B`, `C`, `D`, vagy `all`.

**Lepesek:**

1. Olvasd be az AGENTS.md-bol a kert Track(ek) teljes szekciot.
2. Olvasd be a hozzatartozo plan fajl(oka)t.
3. Olvasd be a relevan uzenofal bejegyzeseket.
4. Generalj egy tomor (max 30-40 sor) osszefoglalot:

```
## Onboarding Snapshot: Track [X] - [datum]

### Cel
[1 mondat]

### Jelenlegi fazis
[Sprint N, fo fokusz]

### Legutolso eredmenyek
- ...

### Nyitott feladatok / blokkolok
- ...

### Cross-track fuggosegek
- [mely mas track-ektol var / kinek szallit]

### Relevant fajlok es tervek
- ...
```

---

## 8. `risk-update`

**Cel:** A `Kockazatok es mitigacio` szekció felulvizsgalata es bovitese.

**Mikor:** 2 hetente, vagy incident (build-break, blokkolofaktor) utan.

**Lepesek:**

1. Olvasd be az AGENTS.md `Kockazatok es mitigacio` szekciot.
2. Olvasd at az uzenofal bejegyzeseit: van-e olyan, ami uj kockazatra utal?
3. Olvasd at a plan fajlok "risk" vagy "kockazat" szekszioit (ha vannak).
4. Ellenorizd:
   - Van-e elavult kockazat, ami mar nem relevas? -> Torold vagy jelold megoldottnak.
   - Van-e uj kockazat, ami meg nem szerepel? -> Add hozza.
   - Megvaltozott-e valamelyik kockazat sulya? -> Frissitsd.
5. **Output:** AGENTS.md `Kockazatok es mitigacio` szekció edit.

**Tipikus kockazat-forrasok:**
- Cross-track uzenofal entries (snapshot/context drift, build-break)
- Plan fajlok kockazat szekcioi
- ArchTest eredmenyek

---

## 9. `project-futures`

**Cel:** Strategiai attekintes -- milyen uj track/session/agens iranyok erdekesek a projekt jovoje szempontjabol.

**Mikor:** Merfoldo eleresekkor, vagy ha a felhasznalo keri.

**Lepesek:**

1. Olvasd be a teljes AGENTS.md-t (track-ek, uzenofal, kockazatok).
2. Olvasd be a `Combat-Defense-Campaign-Master-Plan.md`-t (legnagyobb jovobeli scope).
3. Gondolkodj el a kovetkezokon:
   - Melyik Track kozelit a kesz allapothoz? Mi jonhet utana?
   - Van-e olyan teruleti feladat, ami uj Track-et indokolna?
   - Milyen uj session/chat tipusok hasznosak? (pl. dedikalt combat AI session, performance profiling session, stb.)
   - Van-e architekturalis dont, amit most kell meghozni a jovoben felmerul igenyek miatt?
4. **Output:** Strategiai javaslat dokumentum (markdown report), ami tartalmazza:
   - Lehetseges uj track-ek / session-ok / agens-tipusok felsorolasa
   - Prioritas es fuggosegek
   - Ajanlott idozites

**Megjegyzes:** Ez a workflow kreativitast igenylel, nem csak mechanikus auditor. Celja, hogy a projektet
a felhasznalo aktivan tudja iranyitani, nem csak kezben tartani.

---

## 10. `full-sweep`

**Cel:** Az osszes workflow vegigfutatasa egyben, vegul osszesitett riport generalasa.

**Mikor:** Havonta, vagy milestone / fazisvaltas eseten.

**Futtatasi sorrend:**

1. `agents-maintenance` -- tiszta alap, AGENTS.md naprakesz
2. `arch-audit` -- dependency graph ellenorzes
3. `dod-check` -- track progress
4. `plans-review` -- dokumentacio konzisztencia
5. `conflict-scan` -- potencialis utkozesek
6. `sprint-plan` -- closeout + kovetkezo scope (ha relevan)
7. `risk-update` -- kockazatok
8. `project-futures` -- strategiai attekintes
9. `onboarding-snapshot(all)` -- vegleges friss allapot-osszefoglalo

**Output:** Osszesitett riport:

```
## Full Sweep Report - [datum]

### 1. AGENTS.md Maintenance
[mi valtozott]

### 2. Architecture Audit
[OK / VIOLATION szam]

### 3. DoD Progress
[Track-enkent % vagy lista]

### 4. Plans Review
[konzisztens / hany torott hivatkozas]

### 5. Conflict Scan
[hany potencialis utkozes]

### 6. Sprint Status
[closeout osszefoglalo]

### 7. Risk Update
[uj / eltavolitott kockazatok]

### 8. Strategic Outlook
[fo iranyok]

### 9. Current State (Onboarding)
[Track-enkent 2-3 mondat]

### Osszesitett akcio-lista
1. [legfontosabb tennivalo]
2. ...
```

---

## 11. `perf-check`

**Cel:** Performance baseline meres es budget-osszevetes barmelyik sessionbol triggerelhetoen.

**Mikor:** Combat Phase 3+ sprint gate-eknel, vagy ha FPS < 60 barmelyik map preseten.
Barmelyik session hivhatja. A `full-sweep` opcionalis lepesenek is hasznalhato.

**Elofeltetel:** A Perf Profiling session Phase A infrastrukturaja keszen van:
- `SimStats` letezik (`WorldSim.Runtime/Diagnostics/SimStats.cs`)
- `RenderStats` bovitve (FPS, entity count)
- ScenarioRunner `--perf` mode mukodik

Ha az elofeltetel NEM teljesul, a workflow jelzi, hogy az infrastruktura hianyzik, es
a Perf Profiling session-t kell megnyitni eloszor (Phase A).

**Lepesek:**

1. **Headless meres:**
   ```
   WORLDSIM_SCENARIO_PERF=true
   WORLDSIM_SCENARIO_SEEDS=101,202,303,404,505
   WORLDSIM_SCENARIO_TICKS=1200
   dotnet run --project WorldSim.ScenarioRunner -c Release
   ```
2. **JSON output parse:** Olvasd ki seed-enkent: avgTickMs, maxTickMs, p99TickMs, peakEntities.
3. **Budget osszevetes** (a `Session-Perf-Profiling-Plan.md` Section 4 budgetjei alapjan):
   | Metric | Green | Yellow | Red |
   |---|---|---|---|
   | Sim tick avg | <= 4ms | 4-8ms | > 8ms |
   | Sim tick p99 | <= 8ms | 8-12ms | > 12ms |
   | Peak entities | <= 5000 | 5000-10000 | > 10000 |
4. **Output:** Pass/fail report:
   ```
   ## Perf Check - [datum]
   
   | Seed | Avg Tick | p99 Tick | Peak Entities | Status |
   |------|----------|----------|---------------|--------|
   | 101  | 1.2ms    | 3.1ms   | 289           | GREEN  |
   | ...  | ...      | ...     | ...           | ...    |
   
   Overall: PASS / WARN / FAIL
   ```
5. Ha FAIL: uzenofal entry + Perf Profiling session triggerelese.

**Referencia:** `Docs/Plans/Session-Perf-Profiling-Plan.md` (section 4-5, section 8).

---

## Megjegyzesek

- Ez a chat session maga a Meta Coordinator. Amikor a felhasznalo megnyitja, barmelyik workflow futtathat.
- A workflow-k nev szerint hivatkozhatok: pl. "futtasd le az `arch-audit`-ot".
- Ha a felhasznalo nincs jelen, a Meta Coordinator onalloan nem csinalhat erdemi valtoztatast;
  viszont reportot generalhat, amit a felhasznalo a kovetkezo sessionben attekinthet.
- A `project-futures` workflow kulonleges: ez nem mechanikus audit, hanem kreativ strategiai gondolkodas.
  Ide tartozik a kerdes: "milyen uj chateket / session-oket / agenseket erdemes nyitni?".
