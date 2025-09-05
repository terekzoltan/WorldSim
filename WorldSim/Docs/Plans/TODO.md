
we good

(*) jelentése: mennyiségben ahány csillag ahanyadik a prior sorrendben. (*) -> első ; (****) -> negyedik

- IDEIG DOLGOZZON NE INSTANT (X) -> Munkaidő bevezetése a személyek számára


- ÁgensTerv végrehajtása még most, alapok refaktorálása BEING DONE (*)

ÁGENSTERV VÉGREHAJTÁSA();
-> codex feladat végrehajtása->másolás, promptkent alakítás és egybe kérés->Copilot finomítás+beillesztés jelenlegibe


-LOITER NEM CLEAN-> random burst mozgas teleportalxd



- IRON, GOLD belerak, megerteni distributeation: (***)
1. Bővítsd a `Resource` felsorolást a `WorldSim/Simulation/Tile.cs` fájlban `Iron` és `Gold` elemekkel.
2. Módosítsd a `WorldSim/Simulation/World.cs` biomban és erőforrás-generálásban, hogy vas és arany csomópontokat is létrehozzon.
3. Tegyél be megfelelő textúrákat és rajzoló logikát a `WorldSim/Game1.cs` fájlban, hogy az új erőforrások megjelenjenek a térképen.


Élelemmechanika + STAMINA + Komplexebb állatviselkedés implementálása: (**)
1. Vezess be új munkatípust (GatherFood) a WorldSim/Simulation/Person.cs-ben, implementáld a gyűjtési logikát
(közeli Food node keresése, szedés, készletbe helyezés).
2. Generálj Food típusú erőforrás-csomópontokat a WorldSim/Simulation/World.cs-ben, mennyiség és regeneráció kezeléssel.
3. Minden tick-kor vonj le élelemkészletet a Person vagy Colony frissítésében, kezeld az éhség, stamina csökkenés/töltés, 
éhhalál vagy születési korlát állapotait.
4. Egészítsd ki a WorldSim/Simulation/Animal.cs-t olyan logikával, amely figyelembe veszi az élelemkeresést, ragadozást, menekülést.
5. Adj hozzá szaporodási mechanikát és populációkontrollt az Animal osztály Update metódusában.
6. Integráld az új viselkedéseket és az élelemmechanikát a WorldSim/Simulation/World.cs frissítési ciklusába.


-Technológiákhoz előfeltételek és költségek hozzáadása: (****)
1. Bővítsd a `Tech/technologies.json` szerkezetét `prerequisites` és `cost` mezőkkel.
2. A `WorldSim/Simulation/TechTree.cs` `Technology` osztályában vezess be megfelelő tulajdonságokat.
3. Módosítsd az `Unlock` metódust, hogy ellenőrizze az előfeltételek teljesülését és levonja az erőforrás-költséget.


- Növény ültetés + természetes terjedés (kő ig permanent?)





Egyéb Thoughts:

-MANY LAYER AI SIM -> felső layerek olcso llm->gods

-FAR FUTURE TERV:
codex -> *Write instructions for task execution
szept. 1.*

-Tervezesi mintak:
-STRATEGY, OBSERVER, DI?, FACTORY, STATE, (Template Method, Mediator, Memento)







