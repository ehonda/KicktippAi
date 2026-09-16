# Bundesliga 2026/27 Club Elo refresh

The Club Elo source is governed by [ADR-0083](../../plans/bundesliga-2026-27/decisions/0083-activate-official-club-elo-context-refresh.md). Its only acquisition URL is https://clubelo.com/GER. This source remains disabled until the separately required operational activation gates are completed.

## Current HTML grammar

The descriptor root remains club-elo-official-html-descriptor/v1. New observations emit the paired nested identities club-elo-official-html-parser/v2 and club-elo-official-html-table/v2. Durable validation accepts exactly (parser/v1, table/v1) for historical records or (parser/v2, table/v2) for current records. Mixed, missing, null, wrong-type and unknown nested identities fail closed.

The v2 lexer accepts either the historical bare literal declaration/call envelope or the captured opaque helper envelope. It never evaluates JavaScript. The captured helper prefix is canonical UTF-8 without a BOM or final newline: 1,475 bytes, SHA-256 045a3ee82a23ed59f88d18feb2047567e81d6945feac8e03c2cc3e2b2c0e5f7c. The interstitial comment is 39 bytes, SHA-256 1fa3dee76343dc95f00b82adbd0695fb1d1c7980134b43a81c1da18bd5a126a5. Only CRLF-to-LF conversion and ASCII boundary whitespace trimming apply when matching those opaque segments.

The accepted literal data has at most 262,144 script characters, 512 rows, four single-quoted strings per row, and 8,192 decoded characters per string. It has no executable statements, comments inside the declaration, templates, regexes, expressions, trailing commas, double-quoted JavaScript strings or alternate candidate selection.

Each first cell is lexically checked before parsing in a new inert tr context. Historical td/a/small/a cells remain supported. Current cells require td class=l, the /GER federation anchor containing only the fixed German flag image, a positive canonical rank in small, and a final provider-route anchor containing one nonempty NFC name text node followed by an empty span class=min481. The fixed image URL and style are text checks only: no resource is loaded. Repairs, active markup, comments, entities, Unicode lookalikes, percent-escaped routes, extra attributes or elements, invalid scalars, noncanonical integers and raw & fail closed.

## Evaluation and evidence

The handler makes an exact GET with redirects, automatic decompression and cookies disabled. It preserves the existing response, size, heading-date, mapping, 18-club coverage, seven-day freshness and retained-selection checks. An accepted response stores club-elo/source.html; rejected observations retain only evidence allowed by their first failed gate.

The parked full capture is dated 2026-09-15 and is 564187 bytes, SHA-256 0e57d18ea92dfbece860b3fd67bc49963277e7ceeda95b29187d2f1563dda68d. current-official-2026-09-15.html is a faithful selected excerpt: it preserves the capture's exact Germany heading, eloTable markup (including header class, onclick and title attributes), tbody comment and whitespace, and complete selected script. The checked-in excerpt is 7890 UTF-8 bytes, SHA-256 ddb348d8bb5db64b10bc5b7f8e9c35afe0ab07f2f53855b9f61cb5621f41c0e1. official-helper-prefix.txt and official-before-call.txt are the exact normalized opaque envelope artifacts. These fixtures are parser-mechanics evidence only and do not authorize redistribution, live acquisition, production writes, schedule changes, model calls or activation.