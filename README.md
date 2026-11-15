# Supaprastinta blokų grandinė

VU BGT 2-oji praktinė užduotis. C# programa generuoja vartotojus ir transakcijas, sudaro penkis blokų kandidatus, ieško PoW ir po patikros atnaujina grandinę bei balansus.

| Versija | Turinys                                                                   |
|---------|---------------------------------------------------------------------------|
| `v0.1`  | Vartotojai, transakcijos, sava maiša, blokai, PoW ir konsolės išvestis    |
| `v0.2`  | Merkle medis, validacija, penki kandidatai, testai ir lygiagretus kasimas |
| `v0.3`  | Kasimo optimizavimas, įvesčių patikra ir surikiuoto fondo podėlis         |

## Paleidimas

**.NET 10 SDK, C# 14.** Programa neturi išorinių paketų; testams naudojamas xUnit 2.9.3. Iš projekto šaknies:

```sh
dotnet build BlockChain.slnx -c Release
dotnet test BlockChain.slnx -c Release
dotnet run --project BlockChain -c Release -- --demo --sequential
dotnet run --project BlockChain -c Release -- --full --seed 676767 --sequential
dotnet run --project BlockChain -c Release -- --full --seed 676767 --parallel
dotnet run --project BlockChain -c Release -- --replay runs/full-seed-676767 --sequential
dotnet run --project BlockChain.Bench -c Release -- 5
```

Režimas (`--sequential` / `--parallel`) rašomas paskutinis. Numatytas – nuoseklus; pakartojant naudojama išsaugota reikšmė, jei režimas nenurodytas. Prieš kasimą `runs/<vykdymas>/inputs.json` išsaugomi vartotojai, pradiniai balansai, transakcijos ir konfigūracija. Blokai rašomi į `blocks.csv`, pakartojimas – į `blocks.replay.<režimas>.csv` (`sequential` arba `parallel`).

## Modelis ir maiša

Naudojamas **sąskaitų modelis** su sveikosiomis sumomis. Nėra komisinių ar atlygio: balansai neneigiami, bendra lėšų suma nekinta.

| Objektas    | Laukai                                                                                                                         |
|-------------|--------------------------------------------------------------------------------------------------------------------------------|
| Vartotojas  | Vardas, unikalus `PublicKey`, pradinis balansas                                                                                |
| Transakcija | `Sender`, `Receiver`, teigiamas `Amount`, unikalus generatoriaus `Sequence`, `TransactionId`                                   |
| Blokas      | Antraštė: `prev_hash`, `timestamp`, `version`, `tx_root`, `nonce`, `difficulty`; turinys: iki 100 transakcijų nustatyta tvarka |

Naudojama Igno Šimaičio [1-ojo darbo maiša](https://github.com/Boljerr/blockchain_hash), [importuota versija `8d8e49e`](https://github.com/Boljerr/blockchain/blob/8d8e49ef3ab630295f8314a110eec80544d0ba1f/BlockChain/Hash/HashFunc.cs). Pasirinkta dėl determinizmo ir fiksuoto formato: 256 bitai, 64 mažieji hex simboliai su pradiniais nuliais. `v0.3` išlaiko tą patį algoritmą ir maišas.

Serializacija: UTF-8 be eilutės pabaigos, fiksuota laukų tvarka, skirtukas `|`. Skaičiai dešimtainiai, su invariantine kultūra, be tarpų, pliuso ar nereikalingų pradinių nulių. Hex laukuose `|` nepasitaiko; kūrimas ir patikra naudoja tas pačias funkcijas.

```text
transaction_id = H("sender|receiver|amount|sequence")
block_hash     = H("prev_hash|timestamp|version|tx_root|nonce|difficulty")
```

**Merkle:** lapai – transakcijų ID bloko tvarka. Vidinis mazgas – `H(left_bytes || right_bytes)`, hex prieš jungimą dekoduojant į baitus. Neporinis mazgas dubliuojamas, vienas lapas yra šaknis, tuščia šaknis – `H(tuščia)`. `v0.1` maišė sujungtus ID. Turinį su antrašte susieja `tx_root`, blokus tarpusavyje – `prev_hash`. Dubliavimo dviprasmybė `[A,B,C]` / `[A,B,C,C]` patikrinta testu; dubliuoti ID bloke atmetami.

**Genesis:** `prev_hash` – 64 nuliai, `timestamp = 0`, `version = 2`, `nonce = 0`, tuščias turinys, šaknis `H(tuščia)`, grandinės sudėtingumas. Tai PoW išimtis: genesis nekasamas.

## Validacija ir kasimas

1. **Fondas:** tikrinami formatas, skirtingi ir žinomi dalyviai, teigiama suma bei `Sequence`, perskaičiuotas ID ir dublikatai fonde / grandinėje. Netinkami mokėjimai atmetami su priežastimi; lėšos nerezervuojamos.
2. **Kandidatai:** kiekvienam skiriama atskira patvirtintų balansų kopija. Transakcijos sumaišomos ir taikomos eilės tvarka, atrenkant iki 100 įvykdomų. Nepakankant lėšų mokėjimas paliekamas fonde.
3. **PoW:** keičiama tik `nonce`, kol antraštės maiša prasideda `difficulty` hex nulių. Sudėtingumas nemažinamas.
4. **Priėmimas:** perskaičiuojama maiša ir Merkle šaknis, tikrinami PoW, `prev_hash`, versija 2, 1–100 transakcijų ir grandinės sudėtingumas. Transakcijos pakartotinai tikrinamos eilės tvarka, įskaitant balansus, perpildymą ir pakartojimus. Tik visam blokui praėjus patikrą atnaujinama grandinė, balansai ir fondas.

Atmestas blokas ir nelaimėję kandidatai nekeičia patvirtintos būsenos. Nelikus įvykdomų transakcijų sustojama ir paaiškinamas likutis.

**Nuosekliai:** kandidatai kasami 1–5 tvarka, laimi pirmas radęs. Pradinis biudžetas – 1 000 bandymų kandidatui per ratą; po nesėkmingo rato dvigubinamas. Tęsiama nuo netikrinto nonce. Visiems pasiekus 50 000 000 bandymų kandidatui, vykdymas sustabdomas.

**Lygiagrečiai:** penki `Task.Run` darbuotojai kasa atskirus kandidatus su tomis pačiomis ribomis. Laimėtojas užregistruojamas po užraktu, kiti atšaukiami; prieš priėmimą sulaukiama jų pabaigos. Darbuotojo klaida sustabdo kitus. Laimėtojas priklauso nuo gijų vykdymo, todėl atkuriamam palyginimui naudojamas nuoseklus režimas.

Tai vietinė simuliacija, **ne P2P konsensusas**: vienas procesas, bendra būsena, nėra tinklo ar šakų persitvarkymo. `PublicKey` imituojamas, parašų nėra; ID leidžia tikrinti duomenų vientisumą.

## Duomenys ir rezultatai

| Nustatymas                          | Reikšmė                                       |
|-------------------------------------|-----------------------------------------------|
| Vartotojai / transakcijos           | 1 000 / 10 000                                |
| Pradiniai balansai / mokėjimų sumos | 100–1 000 000 / 1–1 000                       |
| Generavimo / atrankos seed          | `676767` / `676768`, `System.Random`, .NET 10 |
| Sudėtingumas                        | 3; demonstracijoje – 2                        |
| Laiko žyma                          | `1793206326 + height`                         |

Numatytas užduoties sudėtingumas – **3** ([SimulationConfig.cs](BlockChain/Models/SimulationConfig.cs)). Nuoseklus vykdymas ir `--replay` toje pačioje aplinkoje duoda identiškus blokų CSV.

`v0.3` pirmo bloko išvestis abiem režimais. Laikai priklauso nuo kompiuterio; lygiagretaus vykdymo laimėtojas ir bandymų skaičius gali skirtis.

**Nuoseklus kasimas** (`--full --seed 676767 --sequential`):

```text
Block 1
  transactions: 100
  prev_hash:    ca90f11d178ab77a782706208c31f4154815a5223b284927aef293f04494579a
  tx_root:      09913e5dde67fb6a5861f396829ab4568383b7e2dd9c0a2612feea337aac4684
  hash:         000a8d6487681b676f04a879591e316594c9304f80928de53c1fad2da126cf9f
  difficulty:   3
  nonce:        993
  attempts:     994
  mining time:  1.5 ms
  deferred:     0
  winning candidate: 1
  total attempts:    994
  candidate 1: 994 attempts, 0.6 ms, Found
  candidate 2: 0 attempts, 0.0 ms, NotStarted
  candidate 3: 0 attempts, 0.0 ms, NotStarted
  candidate 4: 0 attempts, 0.0 ms, NotStarted
  candidate 5: 0 attempts, 0.0 ms, NotStarted
```

**Lygiagretus kasimas** (`--full --seed 676767 --parallel`):

```text
Block 1
  transactions: 100
  prev_hash:    ca90f11d178ab77a782706208c31f4154815a5223b284927aef293f04494579a
  tx_root:      09913e5dde67fb6a5861f396829ab4568383b7e2dd9c0a2612feea337aac4684
  hash:         000a8d6487681b676f04a879591e316594c9304f80928de53c1fad2da126cf9f
  difficulty:   3
  nonce:        993
  attempts:     994
  mining time:  5.9 ms
  deferred:     0
  winning candidate: 1
  total attempts:    2995
  candidate 1: 994 attempts, 0.6 ms, Found
  candidate 2: 1000 attempts, 0.6 ms, Cancelled
  candidate 3: 1001 attempts, 0.2 ms, Cancelled
  candidate 4: 0 attempts, 0.0 ms, Cancelled
  candidate 5: 0 attempts, 0.0 ms, Cancelled
```

| Suvestinė                              | Demonstracija | Visas nuoseklus rinkinys            |
|----------------------------------------|---------------|-------------------------------------|
| Vartotojai / sugeneruotos transakcijos | 3 / 5         | 1 000 / 10 000                      |
| Įtraukta / atmesta / liko              | 2 / 3 / 0     | 9 999 / 0 / 1                       |
| Blokai                                 | 2             | 100; paskutiniame – 99 transakcijos |
| Pradinė → galutinė lėšų suma           | 300 → 300     | 507 275 047 → 507 275 047           |

Demonstracijos galutiniai balansai: A = 30, B = 220, C = 50. Atmetami nežinomas gavėjas, nulinė suma, pakeistas ID ir atskirai pakartotinai pateiktas patvirtintas mokėjimas. Visame rinkinyje likusiam mokėjimui trūksta lėšų.

## Testai ir v0.3 palyginimas

**Patikrinta 2026-11-15, `Release`:** kompiliavimas be klaidų ir įspėjimų; `v0.2` – **101 / 101**, dabartinė `v0.3` – **147 / 147**, praleistų testų nėra. Visi 101 bazinės versijos testų atvejai yra ir dabartiniame rinkinyje.

| Testų grupė                                | `v0.2`    | `v0.3`    |
|--------------------------------------------|-----------|-----------|
| Baziniai scenarijai                        | 101 / 101 | 101 / 101 |
| Greitojo maišavimo atitiktis pilnai maišai | Netaikyta | 18 / 18   |
| Išsaugotų įvesčių įkėlimas                 | Netaikyta | 25 / 25   |
| Fondo podėlio atnaujinimas                 | Netaikyta | 3 / 3     |

[Testai](BlockChain.Tests) tikrina netinkamus dalyvius, sumas ir ID, bendrą lėšų viršijimą, pakartojimus, nepilną bloką, Merkle 1 / 2 / 3 / 5 atvejus ir tvarką, sugadintus blokus, būseną po atmetimo, kandidatų nepriklausomumą ir atšaukimą.

Papildomai abiem versijoms paleista demonstracija ir visas rinkinys abiem režimais. Iš priimtų blokų mokėjimų atskirai atkurti balansai, perskaičiuoti ID, Merkle šaknys ir PoW: **neigiamų balansų ir pakartotinių ID – 0; lėšų suma bei transakcijų apskaita išliko**. Visuose nuosekliuose abiejų versijų matavimuose sutapo blokai ir galutiniai balansai.

Patikrinta 11 CLI scenarijų: demonstracija ir visas rinkinys abiem režimais, nuoseklus `--replay` (kodas 0); nežinomas argumentas (2); trūkstamas aplankas ir kasimo bandymų riba (1); sugadintas JSON, neigiamas pradinis balansas ir nulinė kandidato riba (3). Visi rezultatai atitiko laukiamus.

| v0.3 pakeitimas    | Veikimas ir patikra                                                                                                                                                                                                                                                   |
|--------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Greitesnis kasimas | `NonceHasher` paruošia antraštės prefikso būseną kartą per `SearchRange`; bandymui lieka nonce ir sudėtingumas, nuliai tikrinami bitais. Atitiktis pilnai maišai tikrinama ribiniais nonce ir 1 500 atsitiktinių porų. Bloko tikrintuvas perskaičiuoja visą antraštę. |
| Įvesčių patikra    | Tikrinama konfigūracija, sąrašų dydžiai, raktai, balansai, perpildymas ir `null`. Sugadintas failas grąžina priežastį ir kodą 3.                                                                                                                                      |
| Fondo podėlis      | Fondas rikiuojamas tik jam pasikeitus; kiekvienas kandidatas maišo savo kopiją. Trys testai tikrina pridėjimą, pašalinimą ir pakeitimą išlaikant fondo dydį.                                                                                                          |

**Bendros matavimo sąlygos:** Windows x64 (10.0.26300), Intel i7-13650HX, 20 loginių procesorių, .NET SDK 10.0.302 / runtime 10.0.10, `Release`. Pateikti nauji abiejų versijų matavimai su tuo pačiu atnaujintu benchmark kodu. Kiekvienai versijai ir režimui – po 5 matavimus, režimų tvarką kaitaliojant; versijos matuotos atskiromis serijomis. Lentelėse – vidurkis ± imties standartinis nuokrypis.

### Fiksuoto darbo sparta

Abu režimai apdorojo tas pačias 5 antraštes po 2 mln. bandymų (iš viso **10 mln.**), difficulty **9**. Nuosekliai antraštės apdorojamos viena po kitos; lygiagrečiai – po vieną antraštę penkiuose darbuotojuose. Visi darbuotojai turi baigti savo bandymus. Prieš matavimus abu režimai apšildomi po 100 000 bandymų antraštei. Laikas apima užduočių paleidimą ir sulaukimą, bet ne konsolės išvedimą.

| Matas                                  | `v0.2`             | `v0.3`         |
|----------------------------------------|--------------------|----------------|
| Nuosekli sparta, mln. bandymų/s        | 0,464 ± 0,095      | 19,120 ± 0,321 |
| Lygiagreti sparta, mln. bandymų/s      | 2,063 ± 0,075      | 80,728 ± 6,146 |
| Nuoseklus 10 mln. bandymų laikas, ms   | 22 122,3 ± 3 550,4 | 523,1 ± 8,7    |
| Lygiagretus 10 mln. bandymų laikas, ms | 4 852,4 ± 186,3    | 124,5 ± 9,9    |

**Rezultatas:** pagal vidutinę bandymų spartą v0.3 buvo **×41,2** spartesnė nuosekliai ir **×39,1** lygiagrečiai. Pagal vidutinių laikų santykį lygiagretus režimas prieš nuoseklų pagreitėjo **×4,56 v0.2** ir **×4,20 v0.3**. v0.2 nuoseklių matavimų sklaida didelė: pirmas vykdymas truko 15,8 s, likę – 23,6–23,8 s. Visi penki įtraukti; šie santykiai aprašo pateiktas vykdymų serijas.

### Visos simuliacijos laikai

**1 000 vartotojų, 10 000 transakcijų, difficulty 3**, generavimo / atrankos seed `676767` / `676768`, tie patys kandidatų biudžetai. Duomenys sugeneruojami vieną kartą; kiekvienas vykdymas pradeda nuo pradinės būsenos. Abu režimai apšildomi viename procese prieš penkis matavimus. Kasimo laikas apima sprendimų paiešką ir koordinavimą, visas laikas – `Runner` vykdymą. Generavimas ir konsolės išvedimas neįtraukti; failai nerašomi.

| Matas                          | `v0.2`         | `v0.3`       |
|--------------------------------|----------------|--------------|
| Nuoseklus kasimas, ms          | 832,5 ± 6,3    | 12,6 ± 0,3   |
| Nuoseklus visas vykdymas, ms   | 3 143,6 ± 17,0 | 980,9 ± 20,7 |
| Lygiagretus kasimas, ms        | 239,3 ± 17,3   | 12,2 ± 0,6   |
| Lygiagretus visas vykdymas, ms | 2 553,3 ± 41,5 | 985,5 ± 14,5 |

**Rezultatas:** v0.3 visas vykdymas sutrumpėjo **68,8 % nuosekliai** ir **61,4 % lygiagrečiai**. v0.3 nuoseklus ir lygiagretus bendri laikai artimi: jų vidurkiai skiriasi tik **0,5 %**, mažiau už matavimų sklaidą. Tai nėra įrodymas, kad vienas režimas visada greitesnis.

Visuose pateiktuose simuliacijos vykdymuose įtraukta **9 999** transakcijų, liko **1**; lėšų suma – **507 275 047**. Lygiagrečiai skiriasi laimėtojai ir bandymų kiekis, todėl simuliacijos laikai nematuoja vienodo darbo kiekio spartos. Tam skirta pirmoji lentelė.

**Ribos ir atmesti pasiūlymai:** maišos algoritmas ir blokų formatas išlaikyti palyginamumui; tikrintuve paliktas pilnas maišavimas. Podėliui reikia papildomo masyvo, o dviejų maišavimo kelių atitiktį saugo testai. Transakcijų turinį tikrina fondas, nes demonstracijoje tyčia yra netinkamų mokėjimų.

## DI naudojimas ir indėliai

Įrankiams pateikti reikalavimai ir aktualus kodas; priimti pakeitimai tikrinti testais, pakartotiniais vykdymais ir matavimais.

| Įrankis / autorius                          | Naudojimas                                                             |
|---------------------------------------------|------------------------------------------------------------------------|
| Claude Code, Claude Opus 5.5 / IvarasJoma   | Pirmieji du `v0.3` patobulinimai, matavimai ir README                  |
| OpenAI Codex / GPT-6.1 Sol / Ignas Šimaitis | `v0.1` peržiūra, `v0.2` testai; `v0.3` fondo podėlis ir dokumentacija. |

| Data / etapas           | Reikšminga užklausa                                     | Pritaikymas                                         |
|-------------------------|---------------------------------------------------------|-----------------------------------------------------|
| 2026-11-05, Claude Code | „what improvements could we make for v0.3?“             | Pasirinktas kasimo greitis ir įvesčių patikra       |
| 2026-11-11, Claude Code | „implement the two improvements you suggested for v0.3“ | Pirmieji du patobulinimai                           |
| 2026-11-15, Codex       | „Implement caching for sorted pool.“                    | Podėlis, 3 regresijos ir penkių vykdymų palyginimas |
| 2026-11-15, Codex       | `v0.3` atskaitos patikslinimas.                         | README formatavimas                                 |

| Narys          | Pagrindinis indėlis                                                                                                                                                                                 |
|----------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Ignas Šimaitis | Maišos funkcija, transakcijų modeliai ir paslauga, serializacija, duomenų generavimas ir saugojimas, kandidatų koordinatorius, lygiagretus kasimas, integracija; `v0.3` fondo podėlis ir jo patikra |
| IvarasJoma     | Blokų modeliai, Merkle medis, genesis, kasėjas, blokų validacija ir priėmimas, vykdymo ciklas, ataskaitos; pirmieji du `v0.3` patobulinimai, testai, spartos matavimai ir README                    |
