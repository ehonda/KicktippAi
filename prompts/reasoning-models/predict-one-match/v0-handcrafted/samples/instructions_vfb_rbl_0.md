# Single Match Prediction in Kicktipp

## Goal

Predict the outcome of a single Bundesliga match in the Kicktipp prediction game. Maximize your expected Kicktipp score.

## Match Input Format

The match will be provided as minified JSON in the following format:

```json
{"homeTeam":"string","awayTeam":"string","startsAt":"string"}
```

For example:

```json
{"homeTeam":"VfB Stuttgart","awayTeam":"RB Leipzig","startsAt":"2025-01-18T15:30:00Z"}
```

## Context Input Format

Provided context can be though of as a set of documents, each with a name and content. Documents will be presented in the following way:

```text
<document_name>

<document_content>
```

We will present the set of documents as follows:

```text
<document_0>

---

<document_1>

---

...

<document_n>
```

## Context

---

community-rules-scoring-only.md

# Prediction Community Rules

## Scoring System

| Result Type | Tendency | Goal Difference | Exact Result |
|-------------|----------|-----------------|--------------|
| Win         | 2        | 3               | 4            |
| Draw        | 3        | -               | 4            |

* Tendency: Predicting the winner or a draw
* Goal Difference: Predicting the winner and the goal difference
* Exact Result: Predicting the exact score

## Examples

### Tendency

```text
Prediction: 2:1
Outcome:    3:1

Prediction: 1:1
Outcome:    2:2
```

### Goal Difference

```text
Prediction: 2:1
Outcome:    3:2
```

### Exact Result

```text
Prediction: 2:1
Outcome:    2:1

Prediction: 1:1
Outcome:    1:1
```

---

bundesliga-standings.csv

Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses
1,FC Bayern München,34,82,99:32,67,25,7,2
2,Bayer 04 Leverkusen,34,69,72:43,29,19,12,3
3,Eintracht Frankfurt,34,60,68:46,22,17,9,8
4,Borussia Dortmund,34,57,71:51,20,17,6,11
5,SC Freiburg,34,55,49:53,-4,16,7,11
6,FSV Mainz 05,34,52,55:43,12,14,10,10
7,RB Leipzig,34,51,53:48,5,13,12,9
8,Werder Bremen,34,51,54:57,-3,14,9,11
9,VfB Stuttgart,34,50,64:53,11,14,8,12
10,Bor. Mönchengladbach,34,45,55:57,-2,13,6,15
11,VfL Wolfsburg,34,43,56:54,2,11,10,13
12,FC Augsburg,34,43,35:51,-16,11,10,13
13,1. FC Union Berlin,34,40,35:51,-16,10,10,14
14,FC St. Pauli,34,32,28:41,-13,8,8,18
15,1899 Hoffenheim,34,32,46:68,-22,7,11,16
16,1. FC Heidenheim 1846,34,29,37:64,-27,8,5,21
17,Holstein Kiel,34,25,49:80,-31,6,7,21
18,VfL Bochum,34,25,33:67,-34,6,7,21

---

last-10-rbl.csv

League,Home_Team,Away_Team,Score
1.BL,Werder Bremen,RB Leipzig,0:0
1.BL,RB Leipzig,FC Bayern München,3:3
1.BL,Eintracht Frankfurt,RB Leipzig,4:0
1.BL,RB Leipzig,Holstein Kiel,1:1
1.BL,VfL Wolfsburg,RB Leipzig,2:3
1.BL,RB Leipzig,1899 Hoffenheim,3:1
DFB,VfB Stuttgart,RB Leipzig,3:1
1.BL,Bor. Mönchengladbach,RB Leipzig,1:0

---

last-10-vfb.csv

League,Home_Team,Away_Team,Score
1.BL,VfB Stuttgart,FC Augsburg,4:0
1.BL,FC St. Pauli,VfB Stuttgart,0:1
1.BL,VfB Stuttgart,1. FC Heidenheim 1846,0:1
1.BL,1. FC Union Berlin,VfB Stuttgart,4:4
1.BL,VfB Stuttgart,Werder Bremen,1:2
1.BL,VfL Bochum,VfB Stuttgart,0:4
DFB,VfB Stuttgart,RB Leipzig,3:1
1.BL,Eintracht Frankfurt,VfB Stuttgart,1:0

---
