
we good

SPRINT 1 (Eco-first) - KALIBRALT SORREND

- Food modell S1-ben: egyetlen Resource.Food (absztrakt "kaloria" stock)
- Food forrasok S1-ben:
  - forage/novenyi food node (map resource)
  - herbivore vadászatbol hus -> Resource.Food
- FONTOS kesobbre (NE FELEJTSUK): Food felbontasa tipusokra (pl. plant/meat, majd milk/egg stb.)
- FONTOS kesobbre: domestication + passive income loop (tej/tojas) + vedelem ragadozok ellen

S1 implementacios sorrend:
1) Elobb termelo oldal (1.2): legyen mit enni
   - food node spawn + minimal regrowth
   - herbivore food fogyasztas
   - predator/herbivore minimal balansz
   - human hunting -> food gain
2) Utana fogyaszto oldal (1.1): hunger+stamina loop
   - GatherFood / EatFood / Rest
   - starvation health hatas
   - stamina fogyasztas es visszatoltes
3) 1.3 maradjon MINIMAL world event szint
   - season/event log legyen
   - vilagra gyakorolt hatas kezdetben kicsi

SPRINT 2 STATUS (Society-first, seed implementation)
- profession rendszer + enyhe auto-rebalance bekerult
- morale + frakcio passzivok bekerultek
- predator->human minimal damage scaffold bekerult
- predator mortality + stuck-recovery telemetria bekerult
- KNOWN BUG (kesobbi fix): shoreline/pathfinding menten animal/NPC beragadas meg elofordulhat
- ideiglenes policy: predator->human tamadas default OFF, ameddig nincs teljes fight/retaliation rendszer (emberi visszautes/vedekezes)

-----------------------

- (Több Job, ResourseTypeok: ritkítani a kettő eddigit)

- növények, azok természetes szaporodása -> novenyevok szaporodasa
-> "Design plant and animal ecosystem dynamics" ELŐKÉSZÍTVE
- 
- kulonbozo epuletek(sawmill, ültető, állathely, stb)
- STAMINA
- FOOD (lentebb codex task)
-> gyumolcs
-> allatkaja
-> novenykaja
-> S1: ideiglenesen egyben kezeljuk Resource.Food-kent
-> KESOBB: bontas tipusokra (plant/meat/milk/egg/...) + passive income rendszer

-IRON GOLD belerak(); (lentebb codex task)

-novenyEvok hasznossaga emberekhez->szaras, novenytermeles or st like that
-ragadozok tamadasa emebereket->vedekezes->visszatamadas, tamadasi rendszer
-> domestication/fence/vedelmi loop KESOBBI sprintben (S2/S3)

- Állatok Icons

-----------------------


- After these->GOAP codex

Goal System egyenlőre csak fallback, régi logika működik..
•	Cooldowns use real time (DateTime.UtcNow), not simulation time, so timeScale affects perceived behavior.
•	BuildHouse can fall back to GatherWood inside the planner, bypassing utility re-evaluation.
•   Altalaban nincs is trigger

-----------------------


ÁGENSTERV VÉGREHAJTÁSA();
-> codex feladat végrehajtása->másolás, promptkent alakítás és egybe kérés->Copilot finomítás+beillesztés jelenlegibe

Codex ref:
GOAP kész: Create GoapPlanner and HtnPlanner classes
Implement action planning using GOAP or HTN (this for tasks)
Write instructions for task execution(Majd amikor LLM-nel vagyunk fontos részletek végén)


Roadmap – Mini lépések
1. Érzékelés/Percepció
 1.1: NPC tudja érzékelni a közvetlen szomszédos tile-okat (fa, kő).
 1.2: Fekete tábla (Blackboard) adatszerkezet: ide írja be, mit látott.
 1.3: Próbáld ki, hogy NPC körbenéz, és listázza a közeli resource-okat.
2. Célok (Utility AI)
 2.1: Egyetlen szükséglet – pl. éhség.
 2.2: Utility függvény: ha éhség nő → pontszám nő.
 2.3: Válassza ki a legnagyobb pontszámú célt.
(Tanulás: megérteni mi a különbség Utility AI és fix prioritás között.)
3. Tervező (GOAP/HTN)
 3.1: Írj 2 akciót: GatherWood, EatFood.
 3.2: Adj hozzá precondition + effect logikát.
 3.3: Egy nagyon egyszerű GOAP/A* planner futtatása ezekre.
(Tanulás: megérteni, hogyan keres útvonalat az akciók között.)
4. Végrehajtó (Behavior Tree)
 4.1: Legyen egy BT, ami csak sorrendben végrehajtja az akciókat.
 4.2: Adj hozzá fallback ágat (pl. ha nincs bogyó → keress).
 4.3: Debug: logold, hogy éppen milyen csomópont fut.
5. Motor réteg
 5.1: NPC menjen A pontból B-be (alap pathfinding).
 5.2: Adja vissza, ha odaért (Action: MoveTo).
 5.3: Próbálj ki több NPC-t egyszerre mozgatni.
6. Infrastruktúra
 6.1: EventBus minimális (NPC → log, vagy NPC → UI jelzés).
 6.2: Telemetria: rajzold ki a BT futását vagy a GOAP tervet konzolra.
7. NPC agy interfész
 7.1: Készítsd el az INpcBrain interfészt (Think() metódussal).
 7.2: Implementáld UtilityBrain-t (heurisztikus fallback).
8–13. (LLM integráció és extra)
Ezeket már csak akkor kezd el, ha az 1–7 alap működik. Ott is érdemes apró chunkokban:
 8.1: Dummy LLM client (mindig MoveTo(0,0) választ ad).
 8.2: Aszinkron queue kipróbálása.
 8.3: Bridge szerver, ami JSON-t fogad/válaszol.
 8.4: Valódi LLM bekötése.



- IRON, GOLD belerak, megerteni distributeation: 
1. Bővítsd a `Resource` felsorolást a `WorldSim/Simulation/Tile.cs` fájlban `Iron` és `Gold` elemekkel.
2. Módosítsd a `WorldSim/Simulation/World.cs` biomban és erőforrás-generálásban, hogy vas és arany csomópontokat is létrehozzon.
3. Tegyél be megfelelő textúrákat és rajzoló logikát a `WorldSim/Game1.cs` fájlban, hogy az új erőforrások megjelenjenek a térképen.


-Technológiákhoz előfeltételek és költségek hozzáadása: 
A Tech/technologies.json most csak az azonosítót, nevet, leírást és egy effect kulcsot tartalmaz, 
amely alapján a kód eldönti, milyen tulajdonságot módosítson a világban vagy a kolóniában. 
Az Unlock jelenleg azonnal beírja a technológiát a kolónia készletére, majd meghív egy switch-et, 
nincs sem előfeltétel-ellenőrzés, sem erőforrás-levonás. A TODO-ban szereplő prerequisites és cost mezők ezért 
azt tennék lehetővé, hogy az adatfájl határozza meg, milyen más technológiák vagy mennyi fa/kő/… kell egy 
fejlesztéshez, és ezt az Unlock a JSON alapján ellenőrizze, illetve levonja a kolónia készleteiből (ami most is 
központilag elérhető a Stock szótárban). Így a tech-fa sorrendje és gazdasági balansza adatszinten hangolható lenne, 
a kód csak az értelmezést végezné.



Egyéb Thoughts:

-MANY LAYER AI SIM -> felső layerek olcso llm->gods

-Tervezesi mintak:
-STRATEGY, OBSERVER, DI?, FACTORY, STATE, (Template Method, Mediator, Memento)







