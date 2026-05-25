# WM26 Lineup Source Status

Status as of 2026-05-26.

The current `ehonda-dev-wm26` lineup seed is an ignored private verification seed at `.agents/skills/wm26-lineups/private/input/lineups-seed.csv`. It is intentionally a small provisional subset for exercising the DuckDB enrichment, upload, prompt-routing, and Langfuse verification workflow. It is not a production full-squad source.

The local supplemental database used for verification is `.agents/skills/wm26-lineups/private/data/transfermarkt-datasets.duckdb`, downloaded from `dcaribou/transfermarkt-datasets`. Keep it ignored and refresh it locally when the upstream dataset changes.

Generated prompt context must use:

```csv
Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR
```

The `$wm26-lineups` `--status provisional|official` flag is workflow metadata only. It is used in payload descriptions and console output, not in the CSV content shown to the model.

## Official Squad Timing

FIFA says all squads are provisional until the final 26-player lists are announced by FIFA after team submission on 2026-06-02. Until then, use `--status provisional`. Once FIFA publishes the final lists, rebuild full-squad lineup documents for every WM26 team and upload/copy them with `--status official`.

Primary trackers:

- FIFA squad timing: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/squad-lists-number-date
- FIFA squad announcement tracker: https://www.fifa.com/en/articles/all-world-cup-squad-announcements

## Dev Slate Source State

| Team | Current public official source state | Next action |
| --- | --- | --- |
| Mexico | FIFA has a 55-player prelist. | Replace the private verification subset with the final FIFA 26 after 2026-06-02. |
| South Africa | FIFA has a 32-player preliminary squad. | Replace with final FIFA 26 after 2026-06-02. |
| Korea Republic | FIFA has a 26-player squad announcement; still provisional until FIFA final publication. | Re-check against the final FIFA list after 2026-06-02. |
| Czechia | FIFA has a 29-player provisional squad. | Replace with final FIFA 26 after 2026-06-02. |
| Canada | Canada Soccer says the roster reveal is scheduled for 2026-05-29. | Wait for the reveal, then re-check after FIFA final publication. |
| Bosnia-Herzegovina | FIFA has a 26-player squad announcement; still provisional until FIFA final publication. | Re-check against the final FIFA list after 2026-06-02. |
| USA | U.S. Soccer says the 26-player roster reveal is scheduled for 2026-05-26 at 3 p.m. ET. | Re-check after the reveal, then again after FIFA final publication. |
| Paraguay | No official squad announcement was captured in this pass. | Watch FIFA tracker and the federation site; do not invent a squad. |
| Qatar | FIFA has a 34-player preliminary squad. | Replace with final FIFA 26 after 2026-06-02. |
| Switzerland | FIFA has a 26-player squad announcement; still provisional until FIFA final publication. | Re-check against the final FIFA list after 2026-06-02. |
| Brazil | FIFA has a 26-player squad announcement; still provisional until FIFA final publication. | Re-check against the final FIFA list after 2026-06-02. |
| Morocco | No official squad announcement was captured in this pass. | Watch FIFA tracker and the federation site; do not invent a squad. |
| Haiti | FIFA has a 26-player squad announcement; still provisional until FIFA final publication. | Re-check against the final FIFA list after 2026-06-02. |
| Scotland | FIFA has a 26-player squad announcement; still provisional until FIFA final publication. | Re-check against the final FIFA list after 2026-06-02. |
| Australia | FIFA has an initial train-on squad only, not a full World Cup squad. | Wait for the official squad announcement. |
| Turkiye | FIFA has a 35-player provisional squad. | Replace with final FIFA 26 after 2026-06-02. |

Useful direct source pages from this pass:

- Mexico: https://www.fifa.com/es/tournaments/mens/worldcup/canadamexicousa2026/articles/mexico-prelista-55-jugadores-mundial-2026
- South Africa: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/south-africa-hugo-broos-squad-announced
- Korea Republic: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/korea-republic-world-cup-squad-hong-myungbo
- Czechia: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/czechia-world-cup-squad-announced
- Canada: https://canadasoccer.com/news/canada-soccer-to-unveil-its-mens-national-team-fifa-world-cup-2026-roster-on-29-may-in-primetime-special-on-tsn-ctv-crave-and-rds/
- Bosnia-Herzegovina: https://www.fifa.com/en/articles/bosnia-and-herzegovina-sergej-barbarez-names-squad
- USA: https://www.ussoccer.com/stories/2026/04/usmnt/mauricio-pochettino-reveal-world-cup-roster-may-26-new-york-city-fox
- Qatar: https://www.fifa.com/en/articles/qatar-announce-preliminary-squad
- Switzerland: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/switzerland-squad-announcement-murat-yakin
- Brazil: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/brazil-squad-announcement-carlo-ancelotti
- Haiti: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/haiti-squad-announcement-sebastien-migne
- Scotland: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/scotland-squad-announced-steve-clarke
- Australia train-on squad: https://www.fifa.com/en/tournaments/mens/worldcup/articles/australia-name-train-on-squad
- Turkiye: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/turkiye-preliminary-world-cup-squad-announced
