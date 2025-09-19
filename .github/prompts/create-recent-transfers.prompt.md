---
mode: agent
---
# Recent Transfers for a Bundesliga Team

## Objective

We want to create a summary document of the recent transfers of a Bundesliga team to be used as context in match predictions.

## Output Document

### Format

The output will be a CSV document with the following columns:

* `Date`
* `Name`
* `Position`
* `From_Team`
* `To_Team`
* `Assessment`

### Location

* The document must be created under `transfers-documents/inputs/<team_abbreviation>-transfers.csv`
* The `<team_abbreviation>` must be taken from the following table:

| Team | Abbreviation |
| --- | --- |
| 1. FC Heidenheim 1846 | fch |
| 1. FC Köln | fck |
| 1. FC Union Berlin | fcu |
| 1899 Hoffenheim | tsg |
| Bayer 04 Leverkusen | b04 |
| Bor. Mönchengladbach | bmg |
| Borussia Dortmund | bvb |
| Eintracht Frankfurt | sge |
| FC Augsburg | fca |
| FC Bayern München | fcb |
| FC St. Pauli | fcs |
| FSV Mainz 05 | m05 |
| Hamburger SV | hsv |
| RB Leipzig | rbl |
| SC Freiburg | scf |
| VfB Stuttgart | vfb |
| VfL Wolfsburg | wob |
| Werder Bremen | svw |

### Column Details

* `Name`, `From_Team`, `To_Team`
  * Are explicitly given in the transfer listing
* `Date`
  * The date of the transfer in `YYYY-MM-DD` format
  * The information can be extracted from the transfer listing
* `Position`
  * Should be deduced from the detailed description of the transfer
  * Must be spelled out for easy understanding and clarity
* `From_Team`
  * The `vereinslos` value must be translated to `Free Agent`
* `Assessment`
  * Should be a concise assessment of transfer impact, deducted from the listed detailed description
  * Must be in english

## Task Specific Instructions

* A link to a page of the team's recent transfers will be provided as input
* The name of the team can be deduced from the link
* Create the file and insert the header 
* Use the #fetch tool to retrieve the page content
* For each transfer listed there, create a line in the document by extracting data for the fields from the listed transfer in accordance with the column details above
