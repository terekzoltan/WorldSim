# WorldSim - Colony Simulation Game

A MonoGame-alapú kolónia szimuláció, inspirációként a *WorldBox - God Simulator* szolgált. A cél egy dinamikus, fejlődő világ létrehozása, ahol az emberek kolóniát alapítanak, nyersanyagokat gyűjtenek, házakat építenek, és később komplexebb viselkedések is megjelennek.

## Fő jellemzők

- 🧍 Egyének attribútumokkal (erő, intelligencia)
- ⌛ Életkor növekedés és idővel halálozás
- 👶 Egyszerű szaporodási logika házkapacitás alapján
- 🪓 Erőforrás-gyűjtés (fa, kő stb.)
- 🏠 Házépítés kolónián belül, kapacitáslimit
- 🌍 Véletlen térkép erőforráseloszlással
- ⏱️ Tick-alapú szimuláció (4x/sec)
- 🪨 Kőházak építése (Stone Masonry technológia után)
- 🔄 Erőforrás megosztás kolóniák között (Improved Logistics után)


## Technológiák

- C# 10
- MonoGame 3.8.3
- Visual Studio 2022

## Tervezett fejlesztések

- Emberek viselkedésének finomhangolása (pl. ha nincs fa → ne csak kóboroljanak)
- Többféle munka / szerepkör (pl. bányászat, farmolás)
- Élelem-fogyasztás, halál éhségtől
- Ház típusok és vizualizációjuk különbsége
- Egyszerű AI-logika fejlesztése („vágyak”, prioritások)
- Játékállás mentése/betöltése
