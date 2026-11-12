# KUtils-Addons

[![Číst česky](https://img.shields.io/badge/%C4%8C%C3%ADst-%C4%8Desky-2563eb?style=for-the-badge)](README.md)

Downloadable add-ons for [Keyboard Utils](https://github.com/tomandesMSH/Keyboard-Utils), a music practice app.

## Instrument packs

The piano is built into the app; every other instrument comes as a pack with the instrument's real sound. The app
downloads a pack the first time the instrument is added (a tab at the top, or **Settings > Instruments**), checks its
size and SHA-256 against `catalog.json`, and unpacks it into `%APPDATA%\KeyboardUtils\instruments\<id>`.

Packs are published as release assets; the app reads
`https://github.com/tomandesMSH/KUtils-Addons/releases/latest/download/catalog.json`.

| Pack | Sound | Source | Size |
|---|---|---|---|
| guitar | Spanish classical guitar | FreePats | 13.6 MB |
| violin | Solo violin, arco + pizzicato | VSCO 2 CE | 5.7 MB |
| viola | Viola section, arco + pizzicato | VSCO 2 CE | 5.0 MB |
| cello | Cello section, arco + pizzicato | VSCO 2 CE | 5.4 MB |
| flute | Flute | VSCO 2 CE | 3.3 MB |
| recorder | Wooden recorder (Eugene Vlaskin) | FreePats | 8.1 MB |
| clarinet | Clarinet (Tyler and Kaili Dence) | FreePats | 7.0 MB |
| trumpet | Trumpet | VSCO 2 CE | 2.8 MB |
| altosax | Saxophone (tenor sax, in the alto's range) | FreePats / Versilian Community Sample Library | 6.9 MB |

Each zip holds `sound.sf2` (a SoundFont 2), `pack.json` (id, version, the presets: `main`, and `pizzicato` for the
bowed strings), `LICENSE.txt` and `readme.txt`.

## Credits and licence

All sounds are in the public domain (CC0 1.0):

- **FreePats** – https://freepats.zenvoid.org
- **VSCO 2 Community Edition** by Versilian Studios – https://github.com/sgossner/VSCO-2-CE. The VSCO packs were
  built into SoundFonts for Keyboard Utils: one dynamic layer, mono, looped sustain, octave and loudness corrected.

The packaging in this repository is also released under CC0 1.0.
