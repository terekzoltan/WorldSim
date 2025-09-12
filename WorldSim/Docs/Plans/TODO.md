
we good

-----------------------

- (Több Job, ResourseTypeok: ritkítani a kettő eddigit)

- növények, azok természetes szaporodása

- FOOD (lentebb codex task)
-> gyumolcs
-> allatkaja
-> novenykaja

-IRON GOLD belerak(); (lentebb codex task)

-novenyevok hasznossaga emberekhez->szaras, novenytermeles or st like that
-ragadozok tamadasa emebereket->vedekezes->visszatamadas, tamadasi rendszer

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


Élelemmechanika + STAMINA + Komplexebb állatviselkedés implementálása: 
1. Vezess be új munkatípust (GatherFood) a WorldSim/Simulation/Person.cs-ben, implementáld a gyűjtési logikát
(közeli Food node keresése, szedés, készletbe helyezés).
2. Generálj Food típusú erőforrás-csomópontokat a WorldSim/Simulation/World.cs-ben, mennyiség és regeneráció kezeléssel.
3. Minden tick-kor vonj le élelemkészletet a Person vagy Colony frissítésében, kezeld az éhség, stamina csökkenés/töltés, 
éhhalál vagy születési korlát állapotait.
4. Egészítsd ki a WorldSim/Simulation/Animal.cs-t olyan logikával, amely figyelembe veszi az élelemkeresést, ragadozást, menekülést.
5. Adj hozzá szaporodási mechanikát és populációkontrollt az Animal osztály Update metódusában.
6. Integráld az új viselkedéseket és az élelemmechanikát a WorldSim/Simulation/World.cs frissítési ciklusába.


-Technológiákhoz előfeltételek és költségek hozzáadása: 
1. Bővítsd a `Tech/technologies.json` szerkezetét `prerequisites` és `cost` mezőkkel.
2. A `WorldSim/Simulation/TechTree.cs` `Technology` osztályában vezess be megfelelő tulajdonságokat.
3. Módosítsd az `Unlock` metódust, hogy ellenőrizze az előfeltételek teljesülését és levonja az erőforrás-költséget.




Egyéb Thoughts:

-MANY LAYER AI SIM -> felső layerek olcso llm->gods

-Tervezesi mintak:
-STRATEGY, OBSERVER, DI?, FACTORY, STATE, (Template Method, Mediator, Memento)







