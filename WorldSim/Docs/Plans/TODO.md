(*) jelentése: mennyiségben ahány csillag ahanyadik a prior sorrendben. (*) -> első ; (****) -> negyedik


- IDEIG DOLGOZZON NE INSTANT (X) -> Munkaidő bevezetése a személyek számára


- ÁgensTerv végrehajtása még most, alapok refaktorálása MOST (*)

- IRON, GOLD belerak, megerteni distributeation: (***)
1. Bővítsd a `Resource` felsorolást a `WorldSim/Simulation/Tile.cs` fájlban `Iron` és `Gold` elemekkel.
2. Módosítsd a `WorldSim/Simulation/World.cs` biomban és erőforrás-generálásban, hogy vas és arany csomópontokat is létrehozzon.
3. Tegyél be megfelelő textúrákat és rajzoló logikát a `WorldSim/Game1.cs` fájlban, hogy az új erőforrások megjelenjenek a térképen.


-Élelemmechanika beépítése: (**)
1. Vezess be új munkatípust (`GatherFood`) a `WorldSim/Simulation/Person.cs`-ben és írd meg a gyűjtési logikát.
2. A `WorldSim/Simulation/World.cs`-ben generálj `Food` erőforrás-csomópontokat.
3. A `Colony` vagy `Person` frissítésében vonj le élelemkészletet minden tick-kor, és kezeld az éhhalált vagy születési korlátokat.


-Technológiákhoz előfeltételek és költségek hozzáadása: (****)
1. Bővítsd a `Tech/technologies.json` szerkezetét `prerequisites` és `cost` mezőkkel.
2. A `WorldSim/Simulation/TechTree.cs` `Technology` osztályában vezess be megfelelő tulajdonságokat.
3. Módosítsd az `Unlock` metódust, hogy ellenőrizze az előfeltételek teljesülését és levonja az erőforrás-költséget.


-Observer minta bevezetése a játék eseményeihez: (*****)
1. Hozz létre egy esemény-központot (pl. `EventBus`) egy új modulban: `WorldSim/Simulation/Events`.
2. Publikáld az olyan eseményeket, mint `PersonBorn`, `PersonDied`, `HouseBuilt` a megfelelő helyeken (`Person.cs`, `World.cs`).
3. Feliratkozással (observer) kezeld az eseményeket a `Game1.cs`-ben vagy más rendszerekben, hogy UI vagy statisztikák frissüljenek.


-Komplexebb állatviselkedés implementálása: (******)
1. Egészítsd ki a `WorldSim/Simulation/Animal.cs` fájlt olyan logikával, amely figyelembe veszi az élelemkeresést, ragadozást vagy menekülést.
2. Adj hozzá szaporodási mechanikát és populációkontrollt az `Animal` osztály `Update` metódusában.
3. Integráld az új viselkedéseket a `WorldSim/Simulation/World.cs` frissítési ciklusába.
1. 

- Tervezési mintákkal + Ágensterv elkezd(most meg processing)

- Növény ültetés + természetes terjedés (kő ig permanent?)


Tervezesi mintak:
-STRATEGY, OBSERVER, DI?, FACTORY, STATE, (Template Method, Mediator, Memento)





