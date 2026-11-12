# KUtils-Addons

[![Read in English](https://img.shields.io/badge/Read%20in-English-2563eb?style=for-the-badge)](README.en.md)

Doplňky ke stažení pro [Keyboard Utils](https://github.com/tomandesMSH/Keyboard-Utils), aplikaci na cvičení
ZUŠ Moravský Krumlov.

## Balíčky nástrojů

Piano je v aplikaci vždycky; každý další nástroj je balíček se skutečným zvukem nástroje. Appka balíček stáhne, když
nástroj poprvé přidáš (záložka nahoře nebo **Nastavení > Nástroje (stáhnout, odebrat)...**), ověří jeho velikost
a SHA-256 podle `catalog.json` a rozbalí ho do `%APPDATA%\KeyboardUtils\instruments\<id>`.

Balíčky jsou přílohy releasů; appka čte
`https://github.com/tomandesMSH/KUtils-Addons/releases/latest/download/catalog.json`.

| Balíček | Zvuk | Zdroj | Velikost |
|---|---|---|---|
| guitar | klasická (španělská) kytara | FreePats | 13,6 MB |
| violin | sólové housle, arco + pizzicato | VSCO 2 CE | 5,7 MB |
| viola | violy (skupina), arco + pizzicato | VSCO 2 CE | 5,0 MB |
| cello | violoncella (skupina), arco + pizzicato | VSCO 2 CE | 5,4 MB |
| flute | příčná flétna | VSCO 2 CE | 3,3 MB |
| recorder | dřevěná zobcová flétna (Eugene Vlaskin) | FreePats | 8,1 MB |
| clarinet | klarinet (Tyler a Kaili Dence) | FreePats | 7,0 MB |
| trumpet | trubka | VSCO 2 CE | 2,8 MB |
| altosax | saxofon (tenorsaxofon v rozsahu altky) | FreePats / Versilian Community Sample Library | 6,9 MB |

Každý zip obsahuje `sound.sf2` (SoundFont 2), `pack.json` (id, verze, presety: `main` a u smyčců `pizzicato`),
`LICENSE.txt` a `readme.txt`.

## Autoři zvuků a licence

Všechny zvuky jsou volné dílo (CC0 1.0):

- **FreePats** – https://freepats.zenvoid.org
- **VSCO 2 Community Edition** od Versilian Studios – https://github.com/sgossner/VSCO-2-CE. Balíčky z VSCO jsou
  pro Keyboard Utils převedené do SoundFontů: jedna dynamická vrstva, mono, tón se drží smyčkou, opravená oktáva
  a hlasitost.

Balení v tomhle repozitáři je také pod CC0 1.0.
