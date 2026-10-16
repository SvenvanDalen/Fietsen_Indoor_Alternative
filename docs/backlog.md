# Backlog: virtuele fietsapp met extra klimmen

**Datum:** 2026-10-15 · **Status:** versie 1, afgestemd met de eigenaar (antwoorden op featurevragen verwerkt)

## 0. Instructies voor Claude Code (lees eerst)

Dit bestand is de enige bron van waarheid voor de backlog. Doe het volgende:

1. Maak in de GitHub-repository de volgende labels aan (met `gh label create`, sla bestaande over):
   - Fase: `fase:MVP`, `fase:V1`, `fase:V2`
   - Prioriteit: `prio:Must`, `prio:Should`, `prio:Could`, `prio:Later`
   - Epic: `epic:E1` t/m `epic:E14`
   - Type: `type:story`, `type:epic`
   - Punten: `pt:1`, `pt:2`, `pt:3`, `pt:5`, `pt:8`, `pt:13`, `pt:21`
2. Maak milestones aan: MVP, V1, V2.
3. Maak per epic (sectie 4) één epic-issue met label `type:epic`, de epic-omschrijving en een takenlijst (`- [ ] #nummer`) met alle stories van die epic. Werk de takenlijst bij nadat de story-issues bestaan.
4. Maak per story (`#### x.y ...` in sectie 4) één issue:
   - Titel: `x.y Titel van de story`
   - Body: de userstory, de acceptatiecriteria als takenlijst en de regel `Epic: Ex`
   - Labels: `type:story`, `fase:...`, `prio:...`, `epic:Ex`, `pt:N`
   - Milestone: gelijk aan de fase
5. Maak een GitHub Project (board) met kolommen Backlog, Sprint, Bezig, Review, Klaar en zet alle issues in Backlog. De stories uit sectie 8 (Sprint 1) gaan naar Sprint.
6. Voeg dit bestand toe als `docs/backlog.md` en maak `docs/decisions.md` aan met de besluiten uit sectie 2.
7. Commit in kleine stappen met duidelijke berichten. Vraag bevestiging voordat je meer dan 30 issues in één keer aanmaakt, en meld hoeveel er zijn aangemaakt.

**Puntenschaal:** 1 = een paar uur, 2 = een halve dag, 3 = een dag, 5 = een paar dagen, 8 = een week, 13 = eerst splitsen, 21 = veel te groot, splitsen. Prioriteit volgens MoSCoW (Must, Should, Could), Later = bewust uitgesteld.

## 1. Visie en scope

De app is een virtuele fietswereld voor een slimme trainer (zoals MyWhoosh en Zwift), met één onderscheidende kern: veel meer en betere klimmen, van korte muurtjes tot alpencols, met echte stijgingspercentages en hoogteprofielen.

- **Doelgroep:** klimmers en recreatieve renners met een slimme trainer die genoeg hebben van dezelfde paar klimmen.
- **Kernbelofte:** elke training voelt als een nieuwe klim; de weerstand volgt het echte hellingspercentage.
- **MVP:** trainer verbinden via Bluetooth of ANT+, één doorlopende low-poly wereld met Stelvio, Alpe d'Huez, Mont Ventoux en Colle delle Finestre, realistische fysica met per klim instelbare hellingsgevoeligheid, FTP-test en workouts, ritopname als .fit-bestand en upload naar Strava.
- **Bewust niet in de MVP:** multiplayer, avatars, racen, eigen GPX-import, adaptieve trainingsplannen.
- **Randvoorwaarden:** gratis hobbyproject, solo, gebouwd met Claude en de Unity-MCP voor Windows; alleen gratis of open assets en data, geen budget.

## 2. Besluiten (uit de antwoorden van de eigenaar)

| Onderwerp | Besluit |
|---|---|
| Doel | Gratis hobbyproject, voorlopig solo |
| Belangrijkste reden | Meer klimmen |
| Trainer | FitBike Smart Drive 50 (direct drive, Bluetooth en ANT+, max. 20% helling, 1200 W) |
| Sensoren | ANT+ en Bluetooth |
| Platform | Windows eerst, daarna telefoon (Android, via Bluetooth) |
| Engine | Unity (C#), gebouwd met Claude en Unity-MCP |
| Klimmen | Mix van echte en fantasieklimmen; vier echte iconen eerst: Stelvio, Alpe d'Huez, Mont Ventoux, Colle delle Finestre |
| Wereld | Eén doorlopende wereld, low-poly, een verzonnen landschap met nagebouwde klimmen |
| Afdalingen | Zelf rijden |
| Ondergronden | Kassei, grind, sneeuw: alleen beeld en extra weerstand, geen gevoel via de trainer |
| GPX-import door gebruikers | Nog niet |
| Hellingsvolging | De trainer volgt de helling; gevoeligheid per klim instelbaar in het menu, niet tijdens de rit |
| Training | Workouts en FTP-test vanaf het begin; adaptief coachplan nog niet |
| Versnellingen | Geen virtuele versnellingen, vaste weerstand zoals MyWhoosh |
| Multiplayer | Voorlopig solo |
| Leaderboards, ghosts, badges | Ja |
| Ranglijst-backend | Gratis via GitHub (Action en Pages), niet vals-spel-bestendig, prima voor een hobbyproject |
| Strava | Ja |
| Garmin Connect, Intervals.icu | Automatisch waar mogelijk |
| Budget | Geen, alles gratis |

## 3. Techniek en architectuur

De grootste technische keuze is de engine: Unity geeft 3D, fysica en input snel; een eigen engine kost maanden zonder dat het klimmen er beter van wordt.

| Onderdeel | Voorstel | Alternatief | Opmerking |
|---|---|---|---|
| Engine | Unity (C#) | Godot 4, Unreal | Unity heeft de meeste Bluetooth-plugins en voorbeelden |
| Trainer-protocol | Bluetooth FTMS (Fitness Machine Service) | ANT+ FE-C, Wahoo-protocol | FTMS dekt de meeste moderne trainers; ANT+ zit ook in de MVP omdat de eigenaar beide gebruikt |
| Sensoren | BLE vermogen, cadans, hartslag | ANT+ | Standaard BLE-profielen, goed gedocumenteerd |
| Fysica | Eigen model: vermogen, massa, helling, rolweerstand, luchtweerstand | Bestaande bibliotheek | Zelf schrijven is klein en bepaalt de klimfeel |
| Routedata | GPX met hoogtedata (open data) | Handmatig getekend | Hoogte uit open datasets, glad gemaakt |
| Wereld | Gegenereerd langs route (terrein, weg, bomen) | Handgemaakte levels | Procedureel is de enige manier om veel klimmen te halen |
| Backend | Eenvoudige API (Postgres + object storage) | Firebase, Supabase | Pas nodig voor accounts en leaderboards; eerst GitHub-variant (story 11.4) |
| Multiplayer | Later, server-authoritative | Peer-to-peer | Niet in de MVP |
| Export | .fit-bestand, Strava-upload | TCX, GPX | Strava-API vereist een app-registratie |

Fysicamodel (vermogen = som van weerstanden × snelheid):

```latex
P = v \cdot \left( m g \sin\theta + C_{rr}\, m g \cos\theta + \tfrac{1}{2}\, \rho\, C_d A\, v^2 \right)
```

Uit deze formule volgt de snelheid per tijdstap; de trainer krijgt de helling als weerstand terug, begrensd op wat de trainer kan (de FitBike Smart Drive 50: 20%).

## 4. Epics en stories

### Overzicht epics

| # | Epic | Fase | Prioriteit |
|---|---|---|---|
| E1 | Trainer- en sensorverbinding | MVP | Must |
| E2 | Fysica en ritbesturing | MVP | Must |
| E3 | Wereld en routerendering | MVP | Must |
| E4 | Klimmen-content en klimcategorieën | MVP | Must |
| E5 | Ritopname, export en Strava | MVP | Must |
| E6 | Interface en HUD | MVP | Must |
| E7 | Accounts en profiel | V1 | Could |
| E8 | Training: workouts, FTP-test, zones | MVP | Must |
| E9 | Voortgang en uitdagingen (klimbadges, Everesting) | V1 | Should |
| E10 | Multiplayer en groepsritten | V2 | Later |
| E11 | Racen en leaderboards per klim | V2 | Could |
| E12 | Content-tools: routes importeren en delen | V2 | Could |
| E13 | Ontwikkelomgeving en Unity-MCP-workflow | MVP | Must |
| E14 | Mobiele versie (rijden op telefoon) | V1 | Should |

### E1 Trainer- en sensorverbinding (MVP)

#### 1.1 Bluetooth-apparaten zoeken

Epic: E1 · Fase: MVP · Prio: Must · Punten: 5

Als rijder wil ik Bluetooth-apparaten zoeken, zodat ik mijn trainer kan kiezen.

- [ ] Lijst met FTMS-trainers en sensoren
- [ ] Signaalsterkte zichtbaar

#### 1.2 Trainer bootst de helling na

Epic: E1 · Fase: MVP · Prio: Must · Punten: 8

Als rijder wil ik dat mijn trainer de helling nabootst.

- [ ] Helling wordt via FTMS gezet
- [ ] Vertraging onder 1 seconde

#### 1.3 Vermogen en cadans live zien

Epic: E1 · Fase: MVP · Prio: Must · Punten: 3

Als rijder wil ik vermogen en cadans live zien.

- [ ] Waarden verversen minstens 1 keer per seconde

#### 1.4 Hartslagband koppelen

Epic: E1 · Fase: MVP · Prio: Must · Punten: 2

Als rijder wil ik een hartslagband koppelen.

- [ ] BLE-hartslagprofiel werkt
- [ ] Hartslag getoond in HUD

#### 1.5 Verbinding herstelt automatisch

Epic: E1 · Fase: MVP · Prio: Should · Punten: 3

Als rijder wil ik dat de verbinding automatisch herstelt.

- [ ] Herverbinden binnen 10 seconden na wegvallen

#### 1.6 Vermogen simuleren zonder trainer

Epic: E1 · Fase: MVP · Prio: Should · Punten: 2

Als rijder zonder trainer wil ik vermogen simuleren (voor testen).

- [ ] Handmatig wattage of toetsenbord-invoer

#### 1.7 ANT+ gebruiken

Epic: E1 · Fase: MVP · Prio: Must · Punten: 8

Als rijder wil ik ANT+ gebruiken.

- [ ] ANT+ FE-C via USB-dongle

#### 1.8 Protocol van de FitBike Smart Drive 50 vaststellen

Epic: E1 · Fase: MVP · Prio: Must · Punten: 3

Als ontwikkelaar wil ik testen welk protocol de FitBike Smart Drive 50 aanbiedt.

- [ ] Bluetooth FTMS of ANT+ FE-C vastgesteld
- [ ] Helling begrensd op 20% (maximum van deze trainer)

### E2 Fysica en ritbesturing (MVP)

#### 2.1 Snelheid volgt uit vermogen

Epic: E2 · Fase: MVP · Prio: Must · Punten: 5

Als rijder wil ik dat snelheid uit mijn vermogen volgt.

- [ ] Model uit de formule in sectie 3
- [ ] Gewicht van fiets en rijder instelbaar

#### 2.2 Helling uit het routeprofiel

Epic: E2 · Fase: MVP · Prio: Must · Punten: 3

Als rijder wil ik dat de helling uit het routeprofiel komt.

- [ ] Helling per 10 meter
- [ ] Gladgestreken tegen schokken

#### 2.3 Instelbare weerstandsgevoeligheid

Epic: E2 · Fase: MVP · Prio: Must · Punten: 2

Als rijder wil ik instelbare weerstandsgevoeligheid.

- [ ] Schaal 50-150% van de echte helling
- [ ] Per klim instelbaar in het menu, niet tijdens de rit

#### 2.4 Afdalingen voelen realistisch

Epic: E2 · Fase: MVP · Prio: Must · Punten: 3

Als rijder wil ik dat afdalingen realistisch aanvoelen.

- [ ] Versnelling en vrijloop
- [ ] Optioneel remmen

#### 2.5 Uitrusting kiezen (aero, licht)

Epic: E2 · Fase: MVP · Prio: Could · Punten: 3

Als rijder wil ik uitrusting kiezen.

- [ ] Wijzigt CdA en massa

#### 2.6 Wind en slipstream

Epic: E2 · Fase: MVP · Prio: Could · Punten: 8

Als rijder wil ik wind en slipstream.

- [ ] Wind op het parcours
- [ ] Groepseffect

### E3 Wereld en routerendering (MVP)

#### 3.1 3D-weg langs een route

Epic: E3 · Fase: MVP · Prio: Must · Punten: 13 (eerst splitsen)

Als rijder wil ik een 3D-weg langs een route zien.

- [ ] Weg gegenereerd uit GPX
- [ ] 60 fps op een gemiddelde laptop

**Gesplitst (2026-10-15):**

- 3.1a Routemodel: afstand naar positie, hoogte en helling (2 pt)
- 3.1b Weg als mesh langs de route, met berm en talud (3 pt)
- 3.1c Rijder en camera volgen de route; de helling van de route gaat naar de rit (2 pt)
- 3.1d Weg uit GPX, samen met 4.1 (3 pt)
- 3.1e Prestaties: weg in stukken opdelen en alleen het zichtbare deel tekenen, 60 fps (3 pt)

#### 3.2 Terrein dat past bij de klim

Epic: E3 · Fase: MVP · Prio: Must · Punten: 8

Als rijder wil ik terrein dat past bij de klim.

- [ ] Bergen, bomen, rotsen langs de weg

#### 3.3 Dag, avond en weer

Epic: E3 · Fase: MVP · Prio: Should · Punten: 5

Als rijder wil ik dag, avond en weer.

- [ ] Tijd van de dag en bewolking instelbaar

#### 3.4 Kilometerborden en bochtnummers

Epic: E3 · Fase: MVP · Prio: Must · Punten: 3

Als rijder wil ik kilometerborden en bochtnummers.

- [ ] Bordjes met afstand tot de top
- [ ] Hellingspercentage zichtbaar

#### 3.5 Dorpjes en publiek in de klim

Epic: E3 · Fase: MVP · Prio: Could · Punten: 8

Als rijder wil ik dorpjes en publiek in de klim.

- [ ] Props en NPC's langs de weg

### E4 Klimmen-content (MVP, het onderscheidende deel)

Hier wint of verliest het product: de klimmen moeten er veel zijn, anders verschilt de app niet van Zwift en MyWhoosh. De route is simpel: GPX met hoogte als bron, een automatische pijplijn die er een rijdbare wereld van maakt.

**Bronnen en rechten:** gebruik GPX van openbare bronnen of zelf getekende routes met open hoogtedata. Neem geen gelicentieerde kaartdata of Strava-segmentgegevens zonder toestemming over.

#### 4.1 Pijplijn: GPX naar klim

Epic: E4 · Fase: MVP · Prio: Must · Punten: 8

Als ontwikkelaar wil ik een pijplijn die GPX omzet naar een klim.

- [ ] Input GPX, output route met hoogteprofiel
- [ ] Afstand, gemiddelde en maximale helling berekend

#### 4.2 Hoogtedata gladstrijken

Epic: E4 · Fase: MVP · Prio: Must · Punten: 5

Als ontwikkelaar wil ik hoogtedata gladstrijken.

- [ ] Geen pieken boven 1% per meter door meetruis

#### 4.3 Klimcategorieën (4, 3, 2, 1, HC)

Epic: E4 · Fase: MVP · Prio: Must · Punten: 2

Als rijder wil ik klimcategorieën.

- [ ] Categorie volgens lengte × gemiddelde helling

**Stand (2026-10-16):** gebouwd (`ClimbCategories`, grenzen in `docs/decisions.md`); de categorie staat in het hoogteprofiel. Mont Ventoux vanaf Bédoin is HC.

#### 4.4 Vijf startklimmen, waaronder vier echte iconen

Epic: E4 · Fase: MVP · Prio: Must · Punten: 8

Als rijder wil ik 5 startklimmen, waaronder vier echte iconen.

- [ ] Stelvio, Alpe d'Huez, Mont Ventoux en Colle delle Finestre
- [ ] Plus één fantasieklim

**Stand (2026-10-15):** Mont Ventoux vanaf Bédoin is nagemaakt (21,4 km, 313 → 1887 m, gemiddeld 7,4%, max 12,5% per 100 m) met echte weg, echt landschap, boomgrens en zendmast. Bronnen in `docs/bronnen.md`. De pijplijn voor 4.1 en 4.2 is daarmee ook gebouwd.

**Stand (2026-10-16):** Alpe d'Huez erbij (D 211, 12,9 km, 723 → 1791 m, gemiddeld 8,3%, HC, de haarspelden herkenbaar), met alpenweide boven de boomgrens. Elke klim is nu een `Climb`-asset in `Routes/` (weg, landschap, begroeiing, herkenningspunt); kiezen gebeurt nog in de inspector van `RouteView`, straks in het ritmenu (6.3). Nog te doen: Stelvio, Colle delle Finestre, een fantasieklim.

#### 4.5 Bibliotheek met 50+ klimmen

Epic: E4 · Fase: V1 · Prio: Should · Punten: 13 (eerst splitsen)

Als rijder wil ik 50+ klimmen in de bibliotheek.

- [ ] Gevarieerd: Alpen, Pyreneeën, Dolomieten, lokale heuvels

#### 4.6 Klimmen op naam vinden

Epic: E4 · Fase: V1 · Prio: Should · Punten: 3

Als rijder wil ik klimmen op naam kunnen vinden.

- [ ] Zoeken en filteren op land, gemiddelde helling en lengte

#### 4.7 Etappe van meerdere klimmen

Epic: E4 · Fase: V1 · Prio: Should · Punten: 8

Als rijder wil ik meerdere klimmen aan elkaar (etappe).

- [ ] Etappe van 2-4 klimmen
- [ ] Korte afdaling ertussen

#### 4.8 Fantasieklimmen met extreme stijgingen

Epic: E4 · Fase: V2 · Prio: Could · Punten: 3

Als rijder wil ik fantasie-klimmen met extreme stijgingen.

- [ ] Muurtjes tot 25%
- [ ] Trainer begrenst zichtbaar

#### 4.9 Klim van de dag

Epic: E4 · Fase: V2 · Prio: Could · Punten: 5

Als rijder wil ik een "klim van de dag".

- [ ] Dagelijks wisselend
- [ ] Met ranglijst

#### 4.10 Routes automatisch genereren

Epic: E4 · Fase: V2 · Prio: Could · Punten: 13 (eerst splitsen)

Als ontwikkelaar wil ik routes automatisch genereren.

- [ ] Willekeurige klim met opgegeven lengte en gemiddelde helling

#### 4.11 Eén doorlopende wereld

Epic: E4 · Fase: MVP · Prio: Must · Punten: 13 (eerst splitsen)

Als rijder wil ik één doorlopende wereld waarin alle klimmen liggen, in een verzonnen landschap met nagebouwde klimmen.

- [ ] Eén kaart met alle klimmen als aansluitende routes
- [ ] Kiezen via wereldkaart
- [ ] Overgang zonder laadscherm of met korte fade

#### 4.12 Afdalingen zelf rijden op elke klim

Epic: E4 · Fase: MVP · Prio: Must · Punten: 5

Als rijder wil ik afdalingen die ik zelf rijd op elke klim.

- [ ] Elke klim heeft een afdaling met vrijloop en bochten
- [ ] Geen automatische overgang

#### 4.13 Ondergronden (kassei, grind, sneeuw)

Epic: E4 · Fase: V1 · Prio: Should · Punten: 8

Als rijder wil ik ondergronden zien en merken (kassei, grind, sneeuw), alleen via beeld en extra weerstand.

- [ ] Extra rolweerstand en visuele trilling per ondergrond
- [ ] Colle delle Finestre heeft grindstroken

### E5 Ritopname en export (MVP)

#### 5.1 Rit opslaan als .fit

Epic: E5 · Fase: MVP · Prio: Must · Punten: 5

Als rijder wil ik dat mijn rit wordt opgeslagen als .fit.

- [ ] Vermogen, hartslag, cadans en hoogte per seconde

#### 5.2 Automatisch uploaden naar Strava

Epic: E5 · Fase: MVP · Prio: Must · Punten: 5

Als rijder wil ik automatisch uploaden naar Strava.

- [ ] OAuth-koppeling
- [ ] Rit verschijnt als virtuele rit

#### 5.3 Ritsamenvatting

Epic: E5 · Fase: MVP · Prio: Should · Punten: 3

Als rijder wil ik een ritsamenvatting zien.

- [ ] Tijd, gemiddeld vermogen, klimtijd en hoogtemeters

#### 5.4 Uploaden naar Garmin Connect en Intervals.icu

Epic: E5 · Fase: MVP · Prio: Should · Punten: 3

Als rijder wil ik mijn rit ook naar Garmin Connect en Intervals.icu sturen.

- [ ] Automatisch waar de dienst dat toestaat (Intervals.icu heeft een open API)
- [ ] Voor Garmin Connect eerst controleren, anders een .fit-bestand handmatig

### E6 Interface en HUD (MVP)

#### 6.1 Vermogen, cadans, snelheid en hartslag in beeld

Epic: E6 · Fase: MVP · Prio: Must · Punten: 3

Als rijder wil ik vermogen, cadans, snelheid en hartslag zien.

- [ ] Altijd zichtbaar
- [ ] Schaalbaar

#### 6.2 Hoogteprofiel met mijn positie

Epic: E6 · Fase: MVP · Prio: Must · Punten: 5

Als rijder wil ik een hoogteprofiel met mijn positie.

- [ ] Profiel van de hele klim met voortgangsstip
- [ ] Helling per segment

#### 6.3 Ritmenu met klimmen kiezen

Epic: E6 · Fase: MVP · Prio: Must · Punten: 5

Als rijder wil ik een ritmenu met klimmen kiezen.

- [ ] Lijst met filter op lengte, gemiddelde helling en categorie

#### 6.4 Pauzeren

Epic: E6 · Fase: MVP · Prio: Should · Punten: 2

Als rijder wil ik pauzeren zonder dat de rit stopt met tellen.

- [ ] Pauzeknop
- [ ] Tijd en afstand blijven kloppen

### E7 Accounts en profiel (V1)

#### 7.1 Account met e-mail of Apple/Google-login

Epic: E7 · Fase: V1 · Prio: Should · Punten: 5

Als rijder wil ik een account met e-mail of Apple/Google-login.

- [ ] Veilig inloggen
- [ ] Wachtwoord herstel

#### 7.2 Gewicht, FTP en max hartslag opslaan

Epic: E7 · Fase: V1 · Prio: Should · Punten: 3

Als rijder wil ik mijn gewicht, FTP en max hartslag opslaan.

- [ ] Profiel synchroniseert tussen apparaten

#### 7.3 Avatar en fietskeuze

Epic: E7 · Fase: V1 · Prio: Could · Punten: 5

Als rijder wil ik een avatar en fietskeuze.

- [ ] Eenvoudige aanpasbare figuur

### E8 Training (MVP)

#### 8.1 FTP-test

Epic: E8 · Fase: MVP · Prio: Must · Punten: 5

Als rijder wil ik een FTP-test doen (20 min of ramp).

- [ ] Resultaat wordt opgeslagen in het profiel

#### 8.2 Gestructureerde workouts (ERG-modus)

Epic: E8 · Fase: MVP · Prio: Must · Punten: 8

Als rijder wil ik gestructureerde workouts.

- [ ] Intervallen met doelvermogen
- [ ] De trainer regelt het vermogen

#### 8.3 Workout-editor

Epic: E8 · Fase: V1 · Prio: Could · Punten: 8

Als rijder wil ik een workout-editor.

- [ ] Blokken slepen
- [ ] Opslaan en delen

#### 8.4 Trainingsplannen voor een klim of event

Epic: E8 · Fase: V2 · Prio: Could · Punten: 13 (eerst splitsen)

Als rijder wil ik trainingsplannen voor een klim of event (nog niet nodig, adaptief plan is uitgesteld).

- [ ] Meerweekse plannen
- [ ] Gekoppeld aan klimdoelen

#### 8.5 Vermogenszones tijdens het rijden

Epic: E8 · Fase: MVP · Prio: Must · Punten: 2

Als rijder wil ik vermogenszones tijdens het rijden.

- [ ] Zonekleuren in de HUD

### E9 Voortgang en uitdagingen (V1)

#### 9.1 Badges voor voltooide klimmen

Epic: E9 · Fase: V1 · Prio: Should · Punten: 5

Als rijder wil ik badges voor klimmen die ik heb voltooid.

- [ ] Badge per klim
- [ ] Overzichtskaart

#### 9.2 Beste tijd per klim

Epic: E9 · Fase: V1 · Prio: Should · Punten: 3

Als rijder wil ik mijn beste tijd per klim bijhouden.

- [ ] Persoonlijk record per klim
- [ ] Vergelijking met vorige poging

#### 9.3 Everesting-uitdaging

Epic: E9 · Fase: V1 · Prio: Could · Punten: 5

Als rijder wil ik een Everesting-uitdaging (8848 hoogtemeter).

- [ ] Teller over meerdere ritten
- [ ] Voortgangsbalk

#### 9.4 Ghost van mijn beste poging

Epic: E9 · Fase: V1 · Prio: Should · Punten: 5

Als rijder wil ik een ghost van mijn beste poging.

- [ ] Doorzichtige renner op het parcours

### E10 Multiplayer (V2, uitgesteld: je rijdt voorlopig solo)

#### 10.1 Andere rijders zien op dezelfde klim

Epic: E10 · Fase: V2 · Prio: Later · Punten: 21 (eerst splitsen)

Als rijder wil ik andere rijders zien op dezelfde klim.

- [ ] Tot 50 renners per sessie
- [ ] Synchronisatie onder 200 ms

#### 10.2 Groepsrit met vrienden plannen

Epic: E10 · Fase: V2 · Prio: Later · Punten: 8

Als rijder wil ik een groepsrit met vrienden plannen.

- [ ] Uitnodigen
- [ ] Starttijd en lobby

#### 10.3 Chat en emotes

Epic: E10 · Fase: V2 · Prio: Later · Punten: 3

Als rijder wil ik chat en emotes.

- [ ] Vooraf gedefinieerde berichten

### E11 Racen en leaderboards (V2)

#### 11.1 Ranglijst per klim

Epic: E11 · Fase: V2 · Prio: Could · Punten: 8

Als rijder wil ik een ranglijst per klim.

- [ ] Dagelijks, wekelijks, ooit
- [ ] Filter op gewicht en leeftijd

#### 11.2 Racen met een startschot

Epic: E11 · Fase: V2 · Prio: Could · Punten: 13 (eerst splitsen)

Als rijder wil ik racen met een startschot.

- [ ] Gelijke start
- [ ] Uitslag

#### 11.3 Fair play (vermogensverificatie)

Epic: E11 · Fase: V2 · Prio: Could · Punten: 13 (eerst splitsen)

Als organisator wil ik fair play.

- [ ] Controle op onmogelijk vermogen en trainerkalibratie

#### 11.4 Gratis ranglijst via GitHub

Epic: E11 · Fase: V2 · Prio: Could · Punten: 8

Als rijder wil ik een gratis ranglijst via GitHub.

- [ ] Uitslag als bestand in een repository
- [ ] Een GitHub Action bouwt de ranglijst
- [ ] GitHub Pages toont hem
- [ ] Bekend en geaccepteerd: niet vals-spel-bestendig

### E12 Content-tools (V2)

#### 12.1 Eigen GPX importeren

Epic: E12 · Fase: V2 · Prio: Could · Punten: 8

Als gebruiker wil ik mijn eigen GPX importeren (nog niet in de MVP).

- [ ] Hoogte automatisch aangevuld
- [ ] Preview

#### 12.2 Routes delen

Epic: E12 · Fase: V2 · Prio: Could · Punten: 8

Als gebruiker wil ik routes delen.

- [ ] Link
- [ ] Zoekbare bibliotheek

### E13 Ontwikkelomgeving (MVP)

#### 13.1 Unity-project voor Windows

Epic: E13 · Fase: MVP · Prio: Must · Punten: 2

Als ontwikkelaar wil ik een Unity-project voor Windows met vaste mappenstructuur.

- [ ] Build start op een schone Windows-pc

#### 13.2 Unity-MCP koppelen aan Claude

Epic: E13 · Fase: MVP · Prio: Must · Punten: 3

Als ontwikkelaar wil ik Unity-MCP koppelen zodat Claude scènes en scripts kan maken.

- [ ] Claude maakt een testscène
- [ ] Claude wijzigt een script via de MCP

#### 13.3 Unit-tests voor het fysicamodel

Epic: E13 · Fase: MVP · Prio: Must · Punten: 3

Als ontwikkelaar wil ik unit-tests voor het fysicamodel.

- [ ] Bekende vermogen-helling-combinaties geven de verwachte snelheid binnen 1%

#### 13.4 Git met LFS voor assets

Epic: E13 · Fase: MVP · Prio: Should · Punten: 2

Als ontwikkelaar wil ik Git met LFS voor assets.

- [ ] Repository met grote bestanden via LFS

#### 13.5 Bronvermelding per klim

Epic: E13 · Fase: MVP · Prio: Should · Punten: 2

Als ontwikkelaar wil ik per klim een bronvermelding bijhouden.

- [ ] Lijst met herkomst en licentie van GPX, hoogtedata en assets

### E14 Mobiele versie (V1)

#### 14.1 App op de telefoon draaien

Epic: E14 · Fase: V1 · Prio: Should · Punten: 5

Als rijder wil ik de app op mijn telefoon draaien.

- [ ] Unity-build voor Android (iOS volgt als dat nodig is)
- [ ] Installeerbaar zonder winkel

#### 14.2 Touch-interface met leesbare HUD

Epic: E14 · Fase: V1 · Prio: Should · Punten: 5

Als rijder wil ik een touch-interface met een leesbare HUD.

- [ ] Grote knoppen
- [ ] HUD schaalt mee op kleine schermen

#### 14.3 Trainer en sensoren via Bluetooth koppelen op de telefoon

Epic: E14 · Fase: V1 · Prio: Should · Punten: 8

Als rijder wil ik trainer en sensoren via Bluetooth koppelen op de telefoon.

- [ ] FTMS, vermogen, cadans en hartslag werken zonder dongle

#### 14.4 Soepel rijden op een middenklasse telefoon

Epic: E14 · Fase: V1 · Prio: Should · Punten: 8

Als rijder wil ik soepel rijden op een middenklasse telefoon.

- [ ] Minstens 30 fps met de low-poly wereld
- [ ] Instelbare kwaliteit

#### 14.5 Ritten op de telefoon opslaan als .fit

Epic: E14 · Fase: V1 · Prio: Should · Punten: 3

Als rijder wil ik dat ritten op de telefoon ook als .fit worden opgeslagen.

- [ ] Zelfde export en Strava-upload als op Windows

#### 14.6 ANT+ op de telefoon

Epic: E14 · Fase: V1 · Prio: Could · Punten: 8

Als rijder wil ik ANT+ op de telefoon.

- [ ] Alleen Android met ANT+-hardware of dongle; iOS ondersteunt het niet

## 5. Eisen, risico's en juridisch

Het grootste risico is niet techniek maar content: zonder rechten op de routes en het merk bouw je op drijfzand.

| Risico | Impact | Aanpak |
|---|---|---|
| Trainer-compatibiliteit verschilt per merk | Hoog | Test met 2-3 trainers vroeg; start met FTMS |
| Hellingsrealisme voelt onnatuurlijk (schokkerige weerstand) | Hoog | Hoogtedata gladstrijken, weerstand in kleine stappen |
| Content maken kost te veel tijd | Hoog | Pijplijn voor automatische klim-generatie vóór handwerk |
| Rechten op kaartdata en namen van klimmen | Midden | Open data (OpenStreetMap, open hoogtedata), eigen namen of vrije namen |
| Handelsmerken (Zwift, MyWhoosh, merknamen klimmen) | Midden | Eigen naam en stijl; geen kopieën van hun werelden of logo's |
| Strava-API-voorwaarden | Midden | App registreren, merkrichtlijnen volgen |
| Prestaties op laptops zonder zware GPU | Midden | Low-poly stijl, instelbare kwaliteit |
| Solo-ontwikkelaar raakt gemotiveerd kwijt | Midden | Kleine mijlpalen, elke sprint iets rijdbaars |
| Geen budget voor assets en kaartdata | Hoog | Alleen gratis of open bronnen (CC0, OpenStreetMap), zelf low-poly genereren met Claude en Unity-MCP |
| FitBike Smart Drive 50: helling tot 20%, FTMS niet bevestigd | Midden | Vroeg testen (story 1.8); als alleen ANT+ FE-C werkt, begint de MVP daarmee |

**Niet-functionele eisen:**

- 60 fps op een gemiddelde laptop met geïntegreerde GPU, minimaal 30 fps.
- Weerstandsvertraging onder 1 seconde tussen helling en trainer.
- Een rit mag nooit verloren gaan: opslaan elke 10 seconden, herstel na crash.
- Werkt op Windows in de MVP; Android daarna (E14).
- Privacy: gewicht, hartslag en ritten alleen met toestemming delen.

## 6. Roadmap

Eerste rijdbare versie na ongeveer vier tot vijf sprints van twee weken; elke sprint eindigt met iets dat je zelf op je trainer kunt proberen.

- **Sprint 1, verbinden:** 1.1, 1.3, 1.6, 1.7, 1.8, 6.1, 13.1, 13.2, 13.3. Doel: vermogen en cadans live in een leeg scherm (zie sectie 8).
- **Sprint 2, fysica:** 2.1, 2.2, 2.4, 1.2. Doel: een vlakke dummy-weg waarop de trainer de helling volgt.
- **Sprint 3, de eerste klim:** 4.1, 4.2, 3.1 (basis), 6.2. Doel: één echte klim uit GPX, met hoogteprofiel.
- **Sprint 4-5, afronden MVP:** 4.3, 4.4, 4.11, 4.12, 5.1, 5.2, 6.3, 8.1, 8.2, 8.5. Doel: vier icoonklimmen in één wereld, ritopname, Strava-upload, FTP-test en workouts.
- **Sprint 5-8, V1:** E14 (mobiele versie), E9 (badges, records, ghost), E7 (profiel, optioneel), meer klimmen (4.5, 4.7), grind en kasseien (4.13).
- **Later, V2:** multiplayer, racen, content-tools, GitHub-ranglijst.

**Definitie van klaar:** de feature werkt op een echte trainer, is getest met minstens één .fit-rit, en staat in de changelog.

## 7. Open vragen

Alle eerdere vragen zijn beantwoord (zie sectie 2). Nog open of te verifiëren:

- [ ] Biedt de FitBike Smart Drive 50 Bluetooth FTMS aan, of alleen ANT+ FE-C en een eigen Bluetooth-protocol? (story 1.8)
- [x] Is er een gratis Unity-pakket voor Bluetooth LE op Windows, of is een eigen .NET-brug nodig? (sectie 8, stap 4) **Besloten 2026-10-15:** eigen .NET-brug (`Bridge/`), zie `docs/decisions.md`.
- [ ] Staat Garmin Connect automatische upload toe voor een hobbyproject, of alleen handmatig via .fit? (story 5.4)
- [ ] Zijn de Bluetooth-identificaties in sectie 8 (UUID's en opcodes) gecontroleerd tegen de officiële Bluetooth-specificatie? **Deels (2026-10-15):** alle service- en characteristic-UUID's kloppen met de Bluetooth SIG assigned numbers; de Control Point-opcodes (0x00, 0x07, 0x11, antwoord 0x80) zijn bij 1.2 gecontroleerd tegen GoldenCheetah en Auuki (de FTMS-specificatie zelf is niet vrij te downloaden); ANT+-pagina 51 nog controleren bij 1.7/1.8.

## 8. Sprint 1 in detail: trainer koppelen

**Doel:** aan het eind van de sprint zie je live vermogen, cadans en hartslag in Unity, kun je de helling op de FitBike Smart Drive 50 zetten, en draait alles ook zonder trainer.

**Stories:** 1.1, 1.3, 1.6, 1.7, 1.8, 6.1, 13.1, 13.2, 13.3.

1. **Project (13.1, 13.2):** Unity LTS met URP, Windows-build, Git met LFS, en de Unity-MCP aan Claude gekoppeld. Test: Claude maakt een lege scène en een script.
2. **Gemeenschappelijke trainer-interface (1.6):** één C#-interface `ITrainer` met vermogen, cadans, hartslag en `SetGrade(float)`. Eerste implementatie is een toetsenbord-simulator, zodat de rest zonder trainer te testen is.
3. **HUD (6.1):** vermogen, cadans, snelheid, hartslag in een eenvoudig scherm, gevoed door `ITrainer`.
4. **Bluetooth op Windows (1.1, 1.3):** Unity heeft op Windows geen ingebouwde Bluetooth LE. Voorstel: een klein .NET-hulpprogramma dat de Windows-Bluetooth-API gebruikt en de data via een lokale WebSocket als JSON aan Unity geeft. Zoek eerst of er een gratis Unity-pakket is dat dit al doet; controleer licentie en onderhoud voordat je kiest.
5. **FTMS lezen en sturen (1.2, 1.8):** zie de tabel hieronder. Eerst alleen lezen, dan besturing aanvragen en helling zetten, en het resultaat noteren in de backlog.
6. **ANT+ (1.7):** via een ANT+-USB-stick en FE-C. Pas na stap 5, en alleen als FTMS op de Smart Drive 50 niet volstaat of als je beide wilt.
7. **Fysicamodel testen (13.3):** unit-tests voor snelheid uit vermogen en helling.

| Onderdeel | Identificatie | Gebruik |
|---|---|---|
| Fitness Machine Service | 0x1826 | Hoofdservice van de trainer |
| Indoor Bike Data | 0x2AD2 | Snelheid, cadans, vermogen, lezen |
| Fitness Machine Control Point | 0x2AD9 | Besturing aanvragen (0x00), starten (0x07), helling en weerstand zetten (0x11) |
| Cycling Power Service | 0x1818 | Vermogensmeter als de trainer geen FTMS heeft |
| Heart Rate Service | 0x180D | Hartslagband |
| ANT+ FE-C | pagina 51 (track resistance) | Helling zetten via ANT+ |

> **Let op:** de identificaties in deze tabel komen uit kennis van de FTMS-standaard en zijn niet bij een bron gecontroleerd; controleer ze tegen de officiële Bluetooth-specificatie voordat je erop bouwt.

**Klaar als:** met trainer en hartslagband verbonden zie je live waarden in de HUD, de helling volgt een testprofiel van 0 tot 10% en terug, en de simulator-modus werkt zonder hardware.
