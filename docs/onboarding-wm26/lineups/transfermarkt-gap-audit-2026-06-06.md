# WM26 Transfermarkt Gap Audit

Status as of 2026-06-06 after the final-squad seed backfill passes and a local
validation run of:

```powershell
dotnet run --no-build --project src/Orchestrator -- collect-context lineups --community-context ehonda-ai-arena --competition fifa-world-cup-2026 --duckdb-path data/wm26/lineups/private/data/transfermarkt-datasets.duckdb --dry-run --verbose
```

The WM26 final-squad seed no longer produces header-only lineup context
documents, but the local Transfermarkt DuckDB snapshot still leaves:

- `71` player rows with blank `Transfermarkt_Player_Id`
- `8` additional players with a valid player id but missing
  `Market_Value_EUR`

The purpose of this note is to preserve what was tried, why the remaining rows
were left unresolved, and which helper script was used during the manual audit.

## What We Tried

The lookup passes used the same conservative workflow for every still-open row:

1. Try the built-in resolver path first:
   `current_national_team_id` plus normalized player name.
2. Search the web for the player name, country, and football context to confirm
   identity and collect better spellings, transliterations, or aliases.
3. Search again with the player's current club when needed, then compare that
   club against DuckDB `players.current_club_name`.
4. For high-confidence cases, check public Transfermarkt profile pages or other
   primary football references and compare those identities against the local
   DuckDB snapshot.
5. Re-run `collect-context lineups --dry-run --verbose` after each seed batch.

This method did resolve additional club-backed aliases, for example:

- `Kevin Pina` -> DuckDB `Kevin Lenini` (`544586`)
- `Alhashmi Alhussein` -> DuckDB `Al-Hashmi Al-Hussain` (`943150`)
- `Mohamed Manai` -> DuckDB `Mohamed Al-Mannai` (`822935`)
- `Jehad Thikri` -> DuckDB `Jehad Thakri` (`901334`)

## Script Used

The tracked helper used for the club-backed DuckDB candidate sweeps is:

- [transfermarkt_duckdb_player_lookup.py](scripts/transfermarkt_duckdb_player_lookup.py)

Example usage:

```powershell
uv --cache-dir .uv-cache run python docs/onboarding-wm26/lineups/scripts/transfermarkt_duckdb_player_lookup.py --db .tmp/transfermarkt-datasets-query.duckdb --name "Mohamed Manai" --team-id 14162 --club "Al Shamal"
```

When the live DuckDB cache file was locked by another local process, the audit
copied it to a read-only scratch file first, for example
`.tmp/transfermarkt-datasets-query.duckdb`.

## Why The Remaining Rows Are Still Open

The remaining unresolved rows generally fall into one of these buckets:

- The player is easy to identify on the web, but the local DuckDB snapshot does
  not contain a matching `players` row at all.
- The player's country or club is covered poorly in the snapshot, so neither
  `current_national_team_id` nor `current_club_name` provides a reliable join.
- A near-match exists, but it is not safe enough to write into the seed because
  the name, birth year, club, or public profile does not line up.
- The player id is already known, but the snapshot still lacks a market value.

Representative confirmed snapshot gaps:

- `Ali Ahmed` (Canada, Norwich City) is identifiable publicly as Transfermarkt
  player `995642`, but that exact player id is not present in the local
  DuckDB snapshot.
- `Benjamin Asare` (Ghana, Hearts of Oak) is identifiable publicly as
  Transfermarkt player `837368`, but that exact player id is not present in the
  local snapshot.
- `Luis Mejia` (Panama, Club Nacional) is identifiable publicly as
  Transfermarkt player `76715`, but that exact player id is not present in the
  local snapshot.
- `Gustavo Velazquez` (Paraguay, Cerro Porteno) is identifiable publicly as
  Transfermarkt player `389399`, but that exact player id is not present in the
  local snapshot.
- `Ahmed Reda Tagnaouti` (Morocco, AS FAR) is identifiable publicly as
  Transfermarkt player `238997`, but that exact player id is not present in the
  local snapshot.
- `Firas Chaouat` (Tunisia, Club Africain) is identifiable publicly as
  Transfermarkt player `402087`, but that exact player id is not present in the
  local snapshot.

Representative unsafe near-matches:

- `Yannick Semedo` has a public Transfermarkt profile as player `620307`, but
  the local snapshot only surfaced `Semedo` (`138649`, `CD Feirense`, born
  1988), which does not line up cleanly enough to reuse.
- `Mostafa Shoubir` only surfaced a suspicious near-match
  `Oufa Shobeir` (`661455`), which was not safe to force into the seed.
- `Mohammad Abuzraiq` is easy to identify publicly via Raja Casablanca, but the
  local snapshot still did not provide a safe player row to connect.

## Remaining Blank Player Ids

- Ägypten: Hamza Abdelkarim, Mahdy Soliman, Marawan Attia, Mohamed Alaa,
  Mohanad Lashin, Mostafa Shoubir, Mostafa Zico, Tarek Alaa
- Algerien: Melvin Mastil
- Bosnien-Herzegowina: Mladen Jurkas
- Curaçao: Jearl Margaritha
- DR Kongo: Brian Cipenga, Fiston Mayele
- Ecuador: Gonzalo Valle
- Ghana: Benjamin Asare, Brandon Thomas-Asante
- Haiti: Carl Sainte, Josue Duverger, Keeto Thermoncy, Leverton Pierre,
  Markhus Lacroix, Martin Experience, Ricardo Ade, Woodensky Pierre
- Irak: Ahmed Qasim, Munaf Younus, Rebin Ghareeb
- Iran: Ali Nemati, Amirmohammad Razaghinia, Arya Yousefi, Danial Iri,
  Dennis Dargahi, Hossein Hosseini, Hossein Kanani, Mehdi Torabi,
  Roozbeh Cheshmi, Saleh Hardani, Shoja Khalilzadeh
- Jordanien: Anas Badawi, Mohammad Abuzraiq
- Kanada: Ali Ahmed
- Kap Verde: Kelvin Pires, Marcio Rosa, Pico Lopes, Stopira, Vozinha,
  Yannick Semedo
- Marokko: Ahmed Reda Tagnaouti
- Panama: Cecilio Waterman, Cesar Samudio, Jose Fajardo, Luis Mejia
- Paraguay: Alejandro Romero Gamarra, Gaston Olveira, Gustavo Velazquez
- Saudi-Arabien: Ala Alhajji
- Schottland: Dominic Hyam, Tyler Fletcher
- Südafrika: Bradley Cross, Ime Okon, Sphephelo Sithole, Thapelo Maseko,
  Themba Zwane
- Tunesien: Firas Chaouat, Khalil Ayari, Mohamed Amine Ben Hmida,
  Mouhib Chamakh, Raed Chikhaoui
- Usbekistan: Azizbek Amonov, Behruzjon Karimov, Khojiakbar Alijonov

## Supplemental-Only Gaps

These players already have a valid `Transfermarkt_Player_Id`, but the local
snapshot still left `Market_Value_EUR` unresolved in the validation run:

- DR Kongo: Timothy Fayulu
- Ecuador: Jordy Alcivar
- Haiti: Dominique Simon
- Paraguay: Gustavo Caballero
- Saudi-Arabien: Mohammed Alowais
- Tunesien: Elias Saad, Moutaz Neffati
- Usbekistan: Sherzod Esanov
