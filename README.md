# Supaprastinta blokų grandinė (BGT, 2 užduotis)

Vietinė, viename procese veikianti blokų grandinės simuliacija. Programa sugeneruoja vartotojus ir mokėjimus, sudaro kandidatinius blokus, kiekvienam ieško darbo įrodymo (Proof-of-Work) naudodama mūsų 1-ojo darbo maišos funkciją, patikrina laimėjusį bloką ir tik tada atnaujina grandinę bei balansus. Tai mokomoji simuliacija, o ne Bitcoin protokolo realizacija: nėra tinklo, tikrų monetų ar skaitmeninių parašų.

```
vartotojai ir pradinė būsena → transakcijų fondas → 5 kandidatiniai blokai → PoW → patikra → grandinės ir balansų atnaujinimas
```

| Versija | Turinys |
|---|---|
| `v0.1` | Veikianti vieno proceso grandinė: savo maiša, PoW, konsolės išvestis |
| `v0.2` | Merkle medis, išplėsta validacija, penki kandidatai kiekvienam aukščiui, teisingumo testai; papildomai lygiagretus kasimas (+0,5) |
| `v0.3` | Du DI pasiūlyti patobulinimai: greitesnis kasimas (tos pačios maišos) ir išsaugotų įvesčių patikra įkeliant; palyginimas su užfiksuota `v0.2` (žr. [v0.3 palyginimas su v0.2](#v03-palyginimas-su-v02)) |

## Paleidimas

**Reikalavimai:** .NET 10 SDK (kurta su 10.0.203). Kalba: C# 14. Programa neturi išorinių priklausomybių; testai naudoja xUnit 2.9.3, kurį `dotnet` automatiškai atsisiunčia iš NuGet.

Visos komandos vykdomos iš repozitorijos šakninio aplanko.

| Veiksmas | Komanda |
|---|---|
| Kompiliavimas | `dotnet build BlockChain.slnx` |
| Testai | `dotnet test BlockChain.slnx` |
| Mažas pavyzdys (sąskaitų modelis) | `dotnet run --project BlockChain -- --demo` |
| Visas rinkinys, nuoseklus kasimas | `dotnet run --project BlockChain -c Release -- --full --seed 676767 --sequential` |
| Visas rinkinys, lygiagretus kasimas | `dotnet run --project BlockChain -c Release -- --full --seed 676767 --parallel` |
| Išsaugoto vykdymo pakartojimas | `dotnet run --project BlockChain -c Release -- --replay runs/full-seed-676767 --sequential` |
| Kasimo greičio matavimas | `dotnet run --project BlockChain.Bench -c Release -- 5` |

- `--sequential` arba `--parallel` rašomas paskutinis. Be jo naudojama konfigūracijos reikšmė (numatyta – nuoseklus kasimas; pakartojant – išsaugota reikšmė).
- `--full` be `--seed` naudoja seed 676767. Kitas seed duoda kitokį, bet lygiai taip pat atkuriamą vykdymą.
- Sąskaitų modelio vykdymai rašo į `runs/`: `demo`, `demo-parallel`, `full-seed-<N>`, `full-seed-<N>-parallel`. Aplanke yra `inputs.json` (vartotojai, transakcijos ir konfigūracija, išsaugomi **prieš** kasimą) ir `blocks.csv` (aukštis, maiša, nonce, transakcijų skaičius).
- `--replay <aplankas>` įkelia `inputs.json` ir vykdo iš naujo nieko negeneruodamas. Rezultatas rašomas į `blocks.replay.sequential.csv` arba `blocks.replay.parallel.csv` šalia originalo, todėl nuoseklaus vykdymo identiškumą galima patikrinti `diff runs/full-seed-676767/blocks.csv runs/full-seed-676767/blocks.replay.sequential.csv`. Lygiagretus kasimas nėra deterministinis (žr. [Lygiagretus kasimas](#lygiagretus-kasimas-05)), todėl jo pakartojimas gali duoti kitą grandinę.
- Įkeliamas `inputs.json` tikrinamas prieš vykdymą (žr. [Išsaugotų įvesčių patikra](#2-išsaugotų-įvesčių-patikra-įkeliant)). Sugadintas ar prieštaringas failas sustabdo programą su sąrašu, kas ir kurioje vietoje netinka.
- Išėjimo kodai: `0` sėkmė, `1` aptikta vidinė klaida, kasimas pasiekė ribą (žr. [Ciklo pabaiga ir patikros](#ciklo-pabaiga-ir-patikros)) arba nepavyko perskaityti ar įrašyti aplanko, `2` nežinomi arba netinkami argumentai (išvedama naudojimo instrukcija), `3` netinkamas išsaugotas `inputs.json`.

## Modelis

### Vartotojai

| Laukas | Aprašymas |
|---|---|
| `Name` | `User0001`, `User0002`, … (demonstracijoje: `A`, `B`, `C`) |
| `PublicKey` | 64 mažieji hex simboliai, `H("user\|<seed>\|<index>")`. Tik imituojamas identifikatorius; jis neįrodo rakto nuosavybės. |
| `InitialBalance` | Sveikasis skaičius nuo 100 iki 1 000 000 |

Pagrindinis modelis – **sąskaitų modelis**: būsena susieja kiekvieną viešąjį raktą su sveikuoju balansu. Komisinių ir kasimo atlygių nėra, todėl bendra lėšų suma nekinta.

### Transakcijos

| Laukas | Aprašymas |
|---|---|
| `Sender`, `Receiver` | Esamų, skirtingų vartotojų vieši raktai |
| `Amount` | Teigiamas sveikasis skaičius (generuojama nuo 1 iki 1 000) |
| `Sequence` | Unikalus generatoriaus numeris nuo 1 iki 10 000. Leidžia atskirti du vienodus mokėjimus tarp tų pačių vartotojų. |
| `TransactionId` | Kitų keturių laukų maiša |

**ID serializacija:** laukai sujungiami fiksuota tvarka skirtuku `|`, skaičiai rašomi dešimtaine forma su invariantine kultūra (be tarpų, nereikalingų pradinių nulių ir pliuso), tekstas koduojamas UTF-8 be eilutės pabaigos, o gauti baitai maišomi:

```
TransactionId = H( UTF8("sender|receiver|amount|sequence") )
```

Vieši raktai sudaryti tik iš `0-9a-f`, todėl `|` lauko reikšmėje pasitaikyti negali. Ta pati funkcija ([TransactionCodec.cs](BlockChain/Transactions/TransactionCodec.cs)) naudojama ir ID kūrimui, ir tikrinimui.

### Blokai

| Dalis | Laukas | Aprašymas |
|---|---|---|
| Antraštė | `prev_hash` | Ankstesnio priimto bloko antraštės maiša |
| Antraštė | `timestamp` | `TimestampBase + height` (`TimestampBase = 1793206326`). Gaunama iš aukščio, o ne iš laikrodžio, kad vykdymai būtų atkuriami. |
| Antraštė | `version` | Duomenų formato versija: `2` (`v0.1` naudojo `1`) |
| Antraštė | `tx_root` | Transakcijų ID Merkle šaknis |
| Antraštė | `nonce` | Paieškos skaitiklis |
| Antraštė | `difficulty` | Reikalaujamas pradinių hex nulių skaičius bloko maišoje |
| Turinys | transakcijos | Iki 100 transakcijų, tikslia tvarka |

**Bloko maiša:** šeši antraštės laukai serializuojami tuo pačiu būdu ir maišomi ([BlockHasher.cs](BlockChain/Blocks/BlockHasher.cs)):

```
block_hash = H( UTF8("prev_hash|timestamp|version|tx_root|nonce|difficulty") )
```

Turinys su antrašte susiejamas per `tx_root`. Pakeitus mokėjimą pasikeičia jo ID, todėl pasikeičia `tx_root`, todėl pasikeičia bloko maiša, ir nutrūksta kito bloko `prev_hash` nuoroda.

### Merkle medis

`tx_root` yra Merkle šaknis ([TxRoot.cs](BlockChain/Blocks/TxRoot.cs)):

- Lapai – transakcijų ID bloko tvarka; prieš jungimą hex ID dekoduojamas į 32 baitus.
- Vidinis mazgas – `H(kairiojo vaiko baitai || dešiniojo vaiko baitai)`; gauta maiša kitam lygiui vėl dekoduojama į baitus.
- Neporinis mazgas kiekviename lygyje poruojamas su savo kopija.
- Vienas lapas – šaknis yra jo ID; tuščias turinys – `H(tuščia baitų seka)`.
- ID, kuris nėra 64 mažieji hex simboliai, į medį nepatenka: bloko tikrintojas tokį bloką atmeta priežastimi `MalformedTransactionId`.

**Žinoma kolizija.** Dėl neporinio mazgo dubliavimo `[T1, T2, T3]` ir `[T1, T2, T3, T3]` turi tą pačią šaknį (testas `Compute_LastTransactionRepeated_GivesSameRoot_KnownCollision`). Jos išnaudoti negalima: `PrepareApply` atmeta bloką su pasikartojančiu ID (`DuplicateInBlock`), ką patikrina `TryAccept_LastTransactionRepeatedWithSameMerkleRoot_ChangesNothing`.

### Genesis blokas

Grandinė prasideda fiksuotu genesis bloku ([Genesis.cs](BlockChain/Blocks/Genesis.cs)):

| Laukas | Reikšmė |
|---|---|
| `prev_hash` | 64 nuliai |
| `timestamp` | `0` |
| `version` | `2` (ta pati konstanta `BlockValidator.SupportedVersion`, kaip ir tikrinant) |
| `tx_root` | `H(tuščia baitų seka)` = `677035964b6f2deb97f3b7e5be8b78960d951b21e8a1eb5cf5179ff3b4619a22` |
| `nonce` | `0` |
| `difficulty` | grandinės sudėtingumas |
| transakcijos | nėra |

Jo maiša skaičiuojama įprastai: `0fa026b689742bc9ab1945ff65467c74fab08c604034618ebde29d57b2e9b9e3`, kai sudėtingumas 5, ir `253ca5d2e4ac01f7145e4e0c68a1ec7a90b4164307713fcaa32d40f6cc308b39` demonstracijoje (sudėtingumas 2). Genesis yra pirmasis grandinės elementas, jis niekada nekasamas ir netikrinamas. Nuo kitų blokų jis skiriasi dviem dokumentuotomis išimtimis:

- **Be darbo įrodymo.** Jo maiša neprivalo prasidėti nuliais.
- **Laiko žyma `0`** vietoje `TimestampBase + height`.

Pradiniai balansai saugomi atskirai, faile `inputs.json`, todėl pradinę būseną visada galima atkurti. Dėl naujos versijos ir Merkle šaknies visos `v0.2` blokų maišos skiriasi nuo `v0.1`; `v0.1` maišos atkuriamos iš žymos `v0.1`.

### Darbo įrodymas

Kandidato paieška ([Miner.cs](BlockChain/Blocks/Miner.cs), `SearchRange`) visus antraštės laukus laiko fiksuotus ir didina nonce, kol antraštės maiša prasideda `difficulty` nuliais. Maiša niekada nekeičiama, kad atitiktų sąlygą. Numatytas sudėtingumas – 5, todėl tikėtinas bandymų skaičius vienam blokui yra 16⁵ ≈ 1 048 576.

`SearchRange(template, transactions, startNonce, maxAttempts)` grąžina radinį arba jo nebuvimą, atliktų bandymų skaičių, kitą netikrintą nonce ir laiką. Nerastas sprendimas nėra klaida: paiešką galima tęsti nuo kito nonce nekartojant jau patikrintų.

Nuo `v0.3` paieška nekuria kiekvienam bandymui naujos antraštės ir 64 simbolių eilutės ([NonceHasher.cs](BlockChain/Blocks/NonceHasher.cs)). Antraštės dalis iki nonce (`prev_hash|timestamp|version|tx_root|`) kandidatui įmaišoma vieną kartą, o kiekvienam nonce įmaišomi tik nonce skaitmenys ir `|difficulty`. Nuliai tikrinami tiesiai 64 bitų būsenos žodžiuose (4 bitai vienam hex nuliui). Maišos funkcija ir antraštės serializacija nepakeistos, todėl kiekvieno nonce maiša lygiai tokia pati kaip `v0.2` (žr. [v0.3 palyginimas su v0.2](#v03-palyginimas-su-v02)). Radus sprendimą, bloko maiša dar kartą apskaičiuojama pilnu būdu.

Darbo įrodymo patikra ([BlockValidator.cs](BlockChain/Blocks/BlockValidator.cs)) greitojo kelio nenaudoja: ji visada **iš naujo apskaičiuoja** visą maišą iš antraštės ir tik tada tikrina prefiksą; išsaugota maiša niekada nelaikoma patikima.

### Vykdymo eiga

Vienas ciklas kartojamas, kol nebelieka ką įtraukti ([Runner.cs](BlockChain/Simulation/Runner.cs)):

1. **Priėmimas į fondą** (`AddToPool`, vieną kartą kiekvienam pateikimui). Tikrinama: identifikatorių formatas (64 mažieji hex simboliai), siuntėjas ir gavėjas egzistuoja, siuntėjas ≠ gavėjas, suma > 0, sequence > 0, perskaičiuotas ID sutampa, ID dar nėra patvirtintas ar laukiantis fonde. Patikros nepraėjusi transakcija **atmetama** visam laikui su priežasties kodu. Patekimas į fondą nerezervuoja siuntėjo lėšų.
2. **Penki kandidatai.** Tam pačiam aukščiui `SelectCandidate` kviečiamas penkis kartus su ta pačia patvirtinta būsena ir vienu viso vykdymo atrankos `Random` (seed), todėl kiekvienas kandidatas gauna kitą fondo sumaišymą. Kiekvienas kandidatas atrenkamas savo laikinoje balansų kopijoje: transakcijos taikomos po vieną ta tvarka, kuria bus bloke, atrenkama iki 100 nuosekliai įvykdomų. Transakcija, kurios siuntėjas tuo metu neturi pakankamai lėšų, **atidedama**: ji lieka fonde. Kandidatų turinys gali persidengti ar sutapti. Visi kandidatai turi tą patį `prev_hash`, laiko žymą ir sudėtingumą.
3. **Kasimas** ([MiningCoordinator.cs](BlockChain/Blocks/MiningCoordinator.cs)) – žr. [Kandidatų kasimas](#kandidatų-kasimas).
4. **Priėmimas** (`Chain.TryAccept`). Niekas nekeičiama, kol nepraeitos visos patikros:
   1. Bloko patikros ([BlockValidator.cs](BlockChain/Blocks/BlockValidator.cs)), pigiausios pirmiausia: `prev_hash` lygus paskutinio bloko maišai, versija 2, nuo 1 iki 100 transakcijų, ID formatas, perskaičiuota `tx_root` sutampa, antraštės sudėtingumas lygus grandinės (blokas negali pats sumažinti reikalavimo), perskaičiuota maiša lygi deklaruotai, maiša turi reikiamą nulių prefiksą.
   2. Transakcijų patikros (`PrepareApply`): transakcijos iš naujo tikrinamos bloko tvarka su patvirtintos būsenos kopija – visos priėmimo į fondą patikros, taip pat pasikartojimai bloke, balanso pakankamumas, sveikųjų skaičių perpildymas ir nepakitusi bendra suma.
   3. Tik tada vienu veiksmu (`CommitAccepted`) nauji balansai pakeičia senuosius, įtrauktos transakcijos pašalinamos iš fondo, ir blokas pridedamas prie grandinės.

   Atmestas blokas nekeičia nei grandinės, nei balansų, nei patvirtintų ID, nei fondo. Nelaimėjusių kandidatų mokėjimai lieka fonde (išskyrus tuos, kurie pateko ir į laimėjusį bloką), o kitam aukščiui visi penki kandidatai sudaromi iš naujo.
5. **Sustojama**, kai visi kandidatai tušti: fondas tuščias arba visos likusios transakcijos atidėtos.

Po ciklo kiekviena fonde likusi transakcija išvardijama su priežastimi, kodėl jos negalima įvykdyti, o demonstracijos pakartotiniai pateikimai dar kartą siunčiami per priėmimo į fondą patikrą.

Pavyzdys (iš užduoties): A turi 100, fonde yra T1: A→B 70 ir T2: A→C 60. Jei kandidate pirmiausia yra T1, laikinas A balansas tampa 30, ir T2 (60 > 30) atidedama. Abi kartu niekada nepriimamos.

### Kandidatų kasimas

Nuoseklus režimas (`MiningCoordinator.Mine`):

- Kandidatai tikrinami tvarka 1 → 2 → 3 → 4 → 5.
- Pradinis biudžetas – 1 000 bandymų kandidatui per ratą. Visam ratui nepavykus, biudžetas dvigubinamas; kiekvieno kandidato paieška tęsiama nuo jo kito netikrinto nonce.
- Riba – 50 000 000 bandymų kandidatui, t. y. daugiausia 250 000 000 vienam aukščiui. Sudėtingumas niekada nemažinamas.
- Laimi pirmas pagal šią vykdymo tvarką sprendimą radęs kandidatas; likusių paieška nebevykdoma. Ataskaitoje matomas kiekvieno kandidato bandymų skaičius, laikas ir būsena (`Found`, `BudgetExhausted`, `NotStarted`, `AttemptLimitReached`, lygiagrečiame režime ir `Cancelled`).
- Visiems kandidatams pasiekus ribą, vykdymas sustabdomas kodu 1, išvedama kiekvieno kandidato suvestinė, ir to aukščio kandidatai nepritaikomi.

### Lygiagretus kasimas (+0,5)

`--parallel` režime (`MiningCoordinator.MineParallelAsync`) kiekvienas iš penkių kandidatų kasamas atskiroje užduotyje (`Task.Run`) su tuo pačiu biudžeto dvigubinimu ir ribomis:

- Paieškos nepriklausomos: kiekviena kasa savo kandidato antraštę (visos pradeda nuo nonce 0, bet antraštės paprastai skiriasi `tx_root`; jei dviejų kandidatų turinys sutampa, sutampa ir jų paieška) ir tęsia nuo savo kito netikrinto nonce.
- Pirmas radęs darbuotojas laimėtoją užregistruoja po užraktu ir atšaukia kitus (`CancellationToken`); antras radinys nebepriimamas.
- Prieš priimant bloką sulaukiama, kol sustos visi darbuotojai; tik tada laimėtojas perduodamas `Chain.TryAccept`.
- Darbuotojo klaida atšaukia ir kitus.

Laimėtojas priklauso nuo to, kuri gija sprendimą randa pirma, todėl lygiagretus režimas **nėra deterministinis**: tas pats seed gali duoti kitą grandinę. Atkuriamumui ir pakartojimui naudojamas nuoseklus režimas.

Nuoseklaus ir lygiagretaus kasimo palyginimas: `--full --seed 676767`, po 5 vykdymus kiekvienam režimui, vidurkis ± standartinis nuokrypis. Sąlygos aprašytos skyriuje [Matavimo sąlygos](#matavimo-sąlygos).

| Versija | Nuoseklus kasimas, s | Lygiagretus kasimas, s | Pagreitėjimas | Skirtingų grandinių 5 vykdymuose (nuoseklus / lygiagretus) |
|---|---|---|---|---|
| `v0.2` | 107,91 ± 1,98 | 23,93 ± 0,60 | ×4,5 | 1 / 2 |
| `v0.3` | 3,30 ± 0,05 | 0,71 ± 0,06 | ×4,6 | 1 / 5 |

- **Pagreitėjimas:** su penkiais kandidatais artimas penkiems, nes kiekvienas kandidatas turi savo branduolį (5 našumo branduoliai).
- **Bandymų skaičius:** lygiagretus režimas iš viso atlieka vidutiniškai 103 mln. bandymų, nuoseklus – 110,6 mln. Kadangi laimi kitas kandidatas, gaunama ir kita grandinė.
- **Determinizmas:** nuoseklus režimas visada davė tą patį `blocks.csv`, lygiagretus – ne.

### Kodėl tai dar nėra P2P konsensusas

Penki kandidatai tik imituoja konkurenciją dėl kito bloko:

- Visi kandidatai veikia viename procese, mato tą pačią atmintį, tą patį fondą ir tą pačią patvirtintą būseną; jie nėra nepriklausomi mazgai.
- Nėra tinklo: blokai nesiunčiami, nėra vėlavimų, praradimų ar skirtingų mazgų požiūrių į grandinę.
- Laimėtoją parenka vienas koordinatorius pagal vietinę taisyklę (vykdymo tvarką arba gijų lenktynes), o ne mazgų sutarimas.
- Nėra šakų ir jų persitvarkymo, „ilgiausios grandinės“ taisyklės, nesąžiningų mazgų, skaitmeninių parašų ar ekonominių paskatų.
- Lygiagretus kasimas viename kompiuteryje taip pat nėra decentralizuotas tinklas.

### Ciklo pabaiga ir patikros

- Kiekvienas priimtas blokas pašalina iš fondo bent vieną transakciją, todėl ciklas visada baigiasi.
- Jei kada nors būtų atmestas mūsų pačių iškastas blokas, tai galėtų reikšti tik klaidą programoje: programa išveda priežastį ir baigiasi kodu 1. Ji nebando iš naujo, todėl negali užsiciklinti.
- **Lėšos:** suvestinėje išvedama pradinė suma (pradinių balansų suma) ir galutinė suma (pagal būseną), su `conserved: yes/no`.
- **Apskaita:** sugeneruota = įtraukta + atmesta + likusi, kur atidėtos transakcijos skaičiuojamos kaip likusios. Jei lygybė netenkinama, programa išveda įspėjimą ir baigiasi kodu 1.

## Maišos funkcija

Projekte naudojama Igno Šimaičio 1-ojo darbo maišos funkcija ([HashFunc.cs](BlockChain/Hash/HashFunc.cs)), į šią repozitoriją perkelta commit `8d8e49e`. Algoritmas nuo tada nekeistas. `v0.3` tik išskaidė `ComputeHash` į tris žingsnius (`Absorb` – baitų įmaišymas nuo nurodytos pozicijos, `Finish` – baigiamasis maišymas, `Format` – hex užrašas), kad kasėjas galėtų pratęsti maišymą nuo išsaugotos būsenos. Konstantos, operacijos ir jų tvarka tos pačios; tai patvirtina testas su žinomomis `v0.2` maišomis ir identiški visų vykdymų `blocks.csv`.

- Deterministinė, fiksuoto ilgio išvestis: 256 bitai, visada užrašomi 64 mažaisiais hex simboliais, pradiniai nuliai išsaugomi.
- Keturi 64 bitų būsenos žodžiai su fiksuotomis pradinėmis konstantomis. Kiekvienas įvesties baitas kartu su savo pozicija įmaišomas į būseną daugybos, sudėties, XOR ir rotacijos operacijomis, o pabaigoje atliekamas baigiamasis maišymo etapas.

> TODO: nuoroda į 1-ojo darbo repozitoriją ir tiksli naudota versija (žyma arba commit), ir vienas ar du sakiniai, kodėl pasirinktas šis algoritmas, o ne kito nario.

Visame rinkinyje (sudėtingumas 5, nuoseklus kasimas) per 100 blokų atlikta 110 564 100 bandymų, vidutiniškai 1,11 mln. bloke (tikėtina 1,05 mln.), todėl pradiniai išvesties hex simboliai darbo įrodymui elgiasi taip, kaip tikimasi.

## Atkuriamumas

| Nustatymas | Reikšmė |
|---|---|
| Generavimo seed | `676767` (vartotojai, balansai, mokėjimai) |
| Atrankos seed | `676768` (generavimo seed + 1; vienas `Random` visam vykdymui, naudojamas kandidatų maišymui) |
| Atsitiktinių skaičių generatorius | `System.Random(seed)`, .NET 10 |
| Vartotojai / transakcijos | 1 000 / 10 000 |
| Pradiniai balansai | nuo 100 iki 1 000 000 |
| Sumos | nuo 1 iki 1 000 |
| Sudėtingumas | 5 (demonstracijose – 2) |
| Kandidatai | 5 vienam aukščiui, iki 100 transakcijų kiekviename |
| Kandidato biudžetas | pradžioje 1 000 bandymų, dvigubinamas po kiekvieno nesėkmingo rato; riba 50 000 000 kandidatui (250 000 000 aukščiui) |
| `TimestampBase` | `1793206326` |
| Išsaugoti įvesties duomenys | `runs/full-seed-676767/inputs.json`, įrašomi prieš kasimą |
| Matavimo aplinka | Apple M3 Pro (5 našumo + 6 efektyvumo branduoliai), macOS 27, .NET SDK 10.0.203, `Release` |

Nuoseklus `--full --seed 676767` ir po jo `--replay runs/full-seed-676767 --sequential` duoda baitas į baitą identiškus `blocks.csv` failus. Demonstracijos duomenys apibrėžti kode (`TransactionService.CreateDemo`); `runs/demo/inputs.json` yra jų išsaugota kopija.

## Rezultatai

### Demonstracija (sąskaitų modelis)

Vartotojai A, B ir C pradeda turėdami po 100 (iš viso 300). Pateikimai:

| # | Transakcija | Laukiamas rezultatas |
|---|---|---|
| 1 | C → A, 50 | įtraukiama |
| 2 | A → B, 120 | iš pradžių atidedama (A turi tik 100), įtraukiama, kai A gauna 50 |
| 3 | A → nežinomas vartotojas, 10 | atmetama: `UnknownReceiver` |
| 4 | A → B, 0 | atmetama: `InvalidAmount` |
| 5 | A → B, 1, po ID apskaičiavimo suma pakeista į 2 | atmetama: `TransactionIdMismatch` |
| po vykdymo | #1 pateikiama dar kartą | atmetama: `AlreadyConfirmed` |

Laukiami galutiniai balansai: A = 100 + 50 − 120 = 30, B = 100 + 120 = 220, C = 100 − 50 = 50, iš viso 300.

Komandos `dotnet run --project BlockChain -- --demo --sequential` išvestis (`v0.3`; `v0.2` išvestis skiriasi tik laikais):

```
Mining mode: sequential
Block 1
  transactions: 1
  prev_hash:    253ca5d2e4ac01f7145e4e0c68a1ec7a90b4164307713fcaa32d40f6cc308b39
  tx_root:      e529e76e4f5f07024da3bf767be7b9f16efd2eb3d640a1e1990a33e4d2462950
  hash:         004f90c40bcfb6a7598895043c4a6c3d5db7bf9185a9ddddc4d6b85562b0867d
  difficulty:   2
  nonce:        819
  attempts:     820
  mining time:  0.6 ms
  deferred:     1
  winning candidate: 1
  total attempts:    820
  candidate 1: 820 attempts, 0.2 ms, Found
  candidate 2: 0 attempts, 0.0 ms, NotStarted
  candidate 3: 0 attempts, 0.0 ms, NotStarted
  candidate 4: 0 attempts, 0.0 ms, NotStarted
  candidate 5: 0 attempts, 0.0 ms, NotStarted

Block 2
  transactions: 1
  prev_hash:    004f90c40bcfb6a7598895043c4a6c3d5db7bf9185a9ddddc4d6b85562b0867d
  tx_root:      10b321b85761bd51e1443b5c441145b7ae5fecbcdf5827db5c4b2b912009ee16
  hash:         0082969d390fe6731185da007f9b64f99d87c9699bacdf8d6ccc64c7663d204b
  difficulty:   2
  nonce:        310
  attempts:     311
  mining time:  0.0 ms
  deferred:     0
  winning candidate: 1
  total attempts:    311
  candidate 1: 311 attempts, 0.0 ms, Found
  candidate 2: 0 attempts, 0.0 ms, NotStarted
  candidate 3: 0 attempts, 0.0 ms, NotStarted
  candidate 4: 0 attempts, 0.0 ms, NotStarted
  candidate 5: 0 attempts, 0.0 ms, NotStarted

Rejected at submission (3)
  #3 3bfd1e0257b8083ec6df734976daa97478c32c7a772d49942f4ba238303b4e3a: UnknownReceiver - Receiver doesn't exist.
  #4 b7ab7613bcaced14da3c67389f28853629621fd4e23a79fab764b89ef9f66618: InvalidAmount - Amount must be positive.
  #5 531354960b0a02c86b30b207b66607b3ca2fc396793b4e13b7795288a8a95c07: TransactionIdMismatch - Transaction ID does not match its contents.

Remaining in pool (0)

Post-run submissions (1)
  e529e76e4f5f07024da3bf767be7b9f16efd2eb3d640a1e1990a33e4d2462950: rejected, AlreadyConfirmed - Transaction is already confirmed.

Summary
  users:                  3
  generated transactions: 5
  submissions:            6
  included:               2
  rejected:               3
  remaining:              0
  accepted blocks:        2
  initial funds:          300
  final funds:            300
  conserved:              yes
  generation seed:        0
  selection seed:         0
  difficulty:             2
  initial candidate budget: 1000
  candidate attempt limit:  50000000
  mining time:            1 ms
  total time:             12 ms
```

### Visas rinkinys (sąskaitų modelis)

Komandos `dotnet run --project BlockChain -c Release -- --full --seed 676767 --sequential` išvestis (`v0.3`; pirmasis blokas, paskutinis blokas ir suvestinė). `v0.2` išvestis skiriasi tik laikais: jos kasimas trunka apie 108 s.

```
Mining mode: sequential
Block 1
  transactions: 100
  prev_hash:    0fa026b689742bc9ab1945ff65467c74fab08c604034618ebde29d57b2e9b9e3
  tx_root:      09913e5dde67fb6a5861f396829ab4568383b7e2dd9c0a2612feea337aac4684
  hash:         0000053b4ce3fa247d3b6a9d1186cf73b5470303ca6120b3543a6cf4e6206a44
  difficulty:   5
  nonce:        281709
  attempts:     281710
  mining time:  46.0 ms
  deferred:     0
  winning candidate: 1
  total attempts:    1301710
  candidate 1: 281710 attempts, 9.6 ms, Found
  candidate 2: 255000 attempts, 8.5 ms, BudgetExhausted
  candidate 3: 255000 attempts, 10.4 ms, BudgetExhausted
  candidate 4: 255000 attempts, 8.4 ms, BudgetExhausted
  candidate 5: 255000 attempts, 8.5 ms, BudgetExhausted

...

Block 100
  transactions: 99
  prev_hash:    00000dfe850ec6f5eb0f9d1d7e7cb198a72ac819be5bf818a03d490f56a357ca
  tx_root:      2eb82b0b9f095775e7a5d9b5fbd98afde22deffc5307aecd3650f67b9f4aac67
  hash:         000008875b00a2fbd28b249d6500008b116cf4c17f906e8b479faf304e4b13bd
  difficulty:   5
  nonce:        201627
  attempts:     201628
  mining time:  36.2 ms
  deferred:     1
  winning candidate: 5
  total attempts:    1221628
  candidate 1: 255000 attempts, 7.6 ms, BudgetExhausted
  candidate 2: 255000 attempts, 7.5 ms, BudgetExhausted
  candidate 3: 255000 attempts, 7.6 ms, BudgetExhausted
  candidate 4: 255000 attempts, 7.5 ms, BudgetExhausted
  candidate 5: 201628 attempts, 5.9 ms, Found

Rejected at submission (0)

Remaining in pool (1)
  db8a4b5e07a9617d883a4960790eb648e51a7bae2cdf0b58d3c3114621d2453f: InsufficientBalance - Sender has insufficient funds at this point in the block.

Post-run submissions (0)

Summary
  users:                  1000
  generated transactions: 10000
  submissions:            10000
  included:               9999
  rejected:               0
  remaining:              1
  accepted blocks:        100
  initial funds:          507275047
  final funds:            507275047
  conserved:              yes
  generation seed:        676767
  selection seed:         676768
  difficulty:             5
  initial candidate budget: 1000
  candidate attempt limit:  50000000
  mining time:            3300 ms
  total time:             4279 ms
```

Paskutinis blokas nepilnas (99 transakcijos). Vienintelė likusi transakcija – mokėjimas, kuriam jo siuntėjas niekada neturi pakankamai lėšų; ji lieka fonde ir yra paaiškinama, o ne bandoma be galo.

## Testai

`dotnet test BlockChain.slnx` vykdo 144 testus; visi praeina. Iš jų 101 yra `v0.2` testai, kurie `v0.3` vykdomi nepakeisti, o 43 nauji tikrina `v0.3` patobulinimus.

| Sritis | Testų | Ką tikrina |
|---|---|---|
| Antraštės maiša ([BlockHasherTests.cs](BlockChain.Tests/Blocks/BlockHasherTests.cs)) | 8 | Tikslus žinomos antraštės serializuotas tekstas; ta pati antraštė → ta pati maiša; pakeitus bet kurį iš šešių laukų maiša pasikeičia |
| Maišos formatas ([HashFormatTests.cs](BlockChain.Tests/Blocks/HashFormatTests.cs)) | 1 | Kiekviena maiša ir šaknis yra 64 mažieji hex simboliai, įskaitant maišas su pradiniais nuliais |
| Merkle medis ([TxRootTests.cs](BlockChain.Tests/Blocks/TxRootTests.cs)) | 14 | Atvejai su 0, 1, 2, 3 ir 5 transakcijomis apskaičiuojami rankomis pagal formulę; pakeitus turinį ir perskaičiavus ID arba sukeitus tvarką šaknis pasikeičia; dokumentuota kolizija; netinkamas ID |
| Genesis ([GenesisTests.cs](BlockChain.Tests/Blocks/GenesisTests.cs)) | 1 | Du genesis blokai identiški |
| Kasimas ([MinerTests.cs](BlockChain.Tests/Blocks/MinerTests.cs), [MiningSearchTests.cs](BlockChain.Tests/Blocks/MiningSearchTests.cs)) | 9 | Teisingas prefiksas keičiant tik nonce; paieška pradedama nuo nurodyto nonce, tęsiama nekartojant, sustoja atšaukus ir ties paskutiniu nonce |
| Kandidatai ([MiningCoordinatorTests.cs](BlockChain.Tests/Blocks/MiningCoordinatorTests.cs)) | 9 | Pirmam kandidatui radus kiti nekasami; biudžetas dvigubinamas ir paieška tęsiama; visiems pasiekus ribą grąžinama nesėkmė; balansus ir fondą keičia tik priimtas laimėtojas |
| Lygiagretus kasimas ([ParallelMiningTests.cs](BlockChain.Tests/Blocks/ParallelMiningTests.cs)) | 6 | Vienas laimėtojas; ribos ir atšaukimas sustabdo darbuotojus; darbuotojo klaida sustabdo kitus; balansus keičia tik laimėjęs mokėjimas |
| Bloko patikra ([BlockValidatorTests.cs](BlockChain.Tests/Blocks/BlockValidatorTests.cs)) | 14 | Iškastas blokas tinkamas; po atmetimo testą kiekvienai priežasčiai: `prev_hash`, versijos 1 ir 3, 0 ir 101 transakcija, netinkami ID, sukeistos transakcijos, sumažintas sudėtingumas, pakeista maiša, nėra darbo įrodymo |
| Priėmimas į grandinę ([ChainTests.cs](BlockChain.Tests/Blocks/ChainTests.cs)) | 7 | Tinkamas blokas atnaujina grandinę, balansus ir fondą kartu; neteisingas `prev_hash`, nėra PoW, pakeista suma su senu ID, perviršis, pasikartojusi transakcija, patvirtinto mokėjimo pakartojimas – atmetami, o būsena, fondas, patvirtinti ID, aukštis ir paskutinė maiša nepasikeičia |
| Priėmimas į fondą ([PoolAdmissionTests.cs](BlockChain.Tests/Transactions/PoolAdmissionTests.cs)) | 12 | Nežinomas siuntėjas ar gavėjas, mokėjimas sau, suma 0 ar neigiama, sequence 0, pakeistas ID, didžiosios raidės ID; dublikatas; patvirtinto pakartojimas; lėšos nerezervuojamos |
| Kandidato atranka ([CandidateSelectionTests.cs](BlockChain.Tests/Transactions/CandidateSelectionTests.cs)) | 5 | 70/60 pavyzdys; mokėjimas atrenkamas tik po jo finansavimo; atrinkti mokėjimai niekada neviršija balanso; riba; tas pats seed → ta pati atranka |
| Bloko taikymas ([BlockApplyTests.cs](BlockChain.Tests/Transactions/BlockApplyTests.cs)) | 7 | Tvarka svarbi; perviršis, dublikatas, pakartojimas, pakeistas ID, perpildymas |
| Vykdymo ciklas ([RunnerTests.cs](BlockChain.Tests/Simulation/RunnerTests.cs)) | 4 | Blokai susieti; kiekviena transakcija apskaityta; ta pati įvestis → tie patys blokai; atmetus savo bloką vykdymas sustabdomas |
| Viso proceso ([EndToEndTests.cs](BlockChain.Tests/EndToEnd/EndToEndTests.cs)) | 4 | Tikslus demonstracijos rezultatas; išsaugoti → vykdyti → įkelti → vykdyti duoda identiškus rezultatus; paskutinis blokas ir lėšų suma; atidėtas mokėjimas įtraukiamas vėliau, neįvykdomas lieka su priežastimi |
| Greitasis kasimo kelias, `v0.3` ([NonceHasherTests.cs](BlockChain.Tests/Blocks/NonceHasherTests.cs)) | 18 | Žinomos `v0.2` maišos nepasikeitė; įvestį padalijus bet kurioje vietoje maiša ta pati; kiekvieno nonce (0, 9, 10, 99, 100, …, `ulong.MaxValue`) ir 300 atsitiktinių antraščių po 5 nonce maiša lygi pilnai; nulių skaičiavimas sutampa su hex nuliais; `SearchRange` randa tą patį nonce kaip pilnas perrinkimas |
| Išsaugotų įvesčių įkėlimas, `v0.3` ([LoadInputsTests.cs](BlockChain.Tests/Transactions/LoadInputsTests.cs)) | 25 | Demonstracija įkeliama nepakitusi (įskaitant fondo atmetamus mokėjimus); 21 sugadinimo atvejis duoda `InvalidDataException` su konkrečia priežastimi; trūkstamas failas – `IOException`; ilgas problemų sąrašas sutrumpinamas; sugeneruoti duomenys patikrą praeina |

Kaip įvykdytos užduoties v0.2 teisingumo patikros:

| Užduoties scenarijus | Testai |
|---|---|
| Galiojantis mokėjimas; paskutinis blokas < 100 | `TryAccept_ValidBlock_UpdatesChainLedgerAndPoolTogether`, `SmallRun_LastBlockWithinLimitAndFundsConserved` |
| Nežinomas gavėjas; suma 0 ar neigiama; pakeistas ID | `AddToPool_InvalidSubmission_IsRejectedWithVisibleReason` (8 atvejai) |
| Du to paties siuntėjo mokėjimai viršija balansą | `SelectCandidate_TwoPaymentsTogetherAboveBalance_SelectsOneAndDefersTheOther`, `PrepareApply_TwoPaymentsTogetherAboveBalance_IsRejected`, `PrepareApply_PaymentBeforeFunding_IsRejected`, `TryAccept_TwoPaymentsTogetherAboveBalance_ChangesNothing` |
| Dubliuotas ID arba patvirtinto mokėjimo pakartojimas | `AddToPool_SameTransactionTwice_SecondIsAlreadyPending`, `AddToPool_ConfirmedTransactionAgain_IsAlreadyConfirmed`, `PrepareApply_SameTransactionTwice_IsRejected`, `TryAccept_ReplayOfConfirmedPaymentInNextBlock_ChangesNothing` |
| Pakeistas bloko turinys, `prev_hash` arba netinkamas PoW | `TryAccept_AlteredAmountWithOldId_ChangesNothing`, `TryAccept_WrongPrevHash_ChangesNothing`, `TryAccept_InvalidProofOfWork_ChangesNothing`, `BlockValidatorTests` |
| Merkle atvejai 1 / 2 / 3 / 5 | `TxRootTests` |
| Nelaimėję kandidatai | `Mine_OnlyAcceptedWinnerChangesBalancesAndPool`, `RunAsync_OnlyWinningPaymentChangesBalances`, `Mine_FirstCandidateSucceeds_OtherCandidatesAreNotSearched` |

## v0.3 palyginimas su v0.2

**Atskaita** – užfiksuota `v0.2`: žyma `v0.2`, commit `5dc8f61` (`main` po PR #5).

**Patobulintos sritys** – dvi iš užduoties siūlomų: kasimo ciklo efektyvumas ir validacijos klaidos.

**Duomenys** – abiem versijoms tie patys: seed 676767, 1 000 vartotojų, 10 000 transakcijų, sudėtingumas 5, tie patys kandidatų biudžetai.

Pasiūlymus pateikė DI įrankis (žr. [DI naudojimas](#di-naudojimas)). Priimtas tik tas pasiūlymas, kurio rezultatą patikrinome patys; patikra aprašyta prie kiekvieno patobulinimo.

### 1. Greitesnis kasimas

**Problema.** `v0.2` kiekvienam bandymui sukurdavo:
- naują antraštės kopiją,
- ~150 simbolių antraštės eilutę ir jos UTF-8 baitų masyvą,
- 64 simbolių hex eilutę, iš kurios tikrinami tik pirmieji simboliai.

Maišos funkcija kiekvieną kartą apdorodavo visus ~150 baitų, nors tarp bandymų keičiasi tik nonce. Funkcija baitus apdoroja iš eilės, todėl būsena po nekintančios pradžios (`prev_hash|timestamp|version|tx_root|`) visiems nonce ta pati. Taigi apie 93 % darbo būdavo kartojama veltui.

**Pakeitimas** (išsamiau – [Darbo įrodymas](#darbo-įrodymas)):
- [HashFunc.cs](BlockChain/Hash/HashFunc.cs) išskaidyta į `Absorb`, `Finish` ir `Format` nekeičiant algoritmo.
- [NonceHasher.cs](BlockChain/Blocks/NonceHasher.cs) būseną po nekintančios pradžios apskaičiuoja vieną kartą kandidatui, o kiekvienam nonce įmaišo tik nonce ir `|difficulty`.
- [Miner.cs](BlockChain/Blocks/Miner.cs) nulius tikrina būsenos bitais. Antraštė, blokas ir pilna maiša kuriami tik radus sprendimą.
- [BlockValidator.cs](BlockChain/Blocks/BlockValidator.cs) nepakeistas: patikra skaičiuoja visą maišą įprastu būdu.

**Kaip patikrinta, kad rezultatai nepasikeitė:**

| Patikra | Rezultatas |
|---|---|
| `v0.2` testai (101, nepakeisti) su `v0.3` kodu | 101 / 101 praeina |
| Žinomos `v0.2` maišos: `H(tuščia)` ir genesis blokai su sudėtingumu 5 ir 2 | sutampa |
| Greitasis kelias lyginamas su pilnu ([NonceHasherTests.cs](BlockChain.Tests/Blocks/NonceHasherTests.cs)) | Sutampa: įvesties padalijimas bet kurioje vietoje, ribiniai nonce, 1 500 atsitiktinių antraščių ir nonce porų, nulių skaičiavimas, `SearchRange` lyginant su pilnu perrinkimu |
| Ar testai pagauna klaidas: į kopiją tyčia įvestos dvi klaidos | Maišą pratęsiant nuo pozicijos 0 vietoje prefikso ilgio, nepraeina 30 testų; `>=` pakeitus į `>`, nepraeina 7 testai |
| Visas rinkinys, nuoseklus kasimas, po 5 vykdymus kiekvienai versijai | Visų 10 `blocks.csv` SHA-256 tas pats (`59de33a8d770b7cd…f4d9d7c`): 100 blokų, 110 564 100 bandymų, paskutinė maiša `000008875b00a2fb…4e4b13bd` |
| Demonstracija | `blocks.csv` identiški; konsolės išvestis skiriasi tik laikais |
| `v0.3` `--replay runs/full-seed-676767 --sequential` | `blocks.replay.sequential.csv` identiškas `blocks.csv` |

**Greitis su fiksuotu bandymų skaičiumi** ([BlockChain.Bench](BlockChain.Bench/Program.cs)). Kiekvienas matavimas – 5 fiksuotos antraštės po 2 000 000 bandymų, t. y. 10 mln. bandymų. Prieš 5 matavimus atliekamas apšilimas.

| Matas | `v0.2` | `v0.3` | Santykis |
|---|---|---|---|
| Bandymų per sekundę, mln. | 1,065 ± 0,002 | 29,73 ± 1,11 | ×27,9 |
| Vienas bandymas, ns | 938,7 ± 2,2 | 33,7 ± 1,3 | |

**Visas rinkinys** (`--full --seed 676767`, po 5 vykdymus):

| Vykdymas | `v0.2`, s | `v0.3`, s | Santykis |
|---|---|---|---|
| Nuoseklus, kasimo laikas | 107,91 ± 1,98 | 3,30 ± 0,05 | ×32,7 |
| Nuoseklus, visas laikas | 108,88 ± 1,99 | 4,28 ± 0,06 | ×25,4 |
| Lygiagretus, kasimo laikas | 23,93 ± 0,60 | 0,71 ± 0,06 | ×33,7 |
| Lygiagretus, visas laikas | 24,89 ± 0,61 | 1,92 ± 0,10 | ×13,0 |

Pagrindinis greičio rodiklis – matavimas su fiksuotu bandymų skaičiumi, kaip rekomenduoja užduotis. Viso rinkinio laikai pateikti papildomai. Visas laikas pagreitėjo mažiau nei kasimas, nes ne kasimo darbas abiejose versijose trunka apie 1 s. Šis darbas – fondo užpildymas, kandidatų atranka, Merkle šaknys ir bloko patikra.

#### Matavimo sąlygos

- **Aplinka:** Apple M3 Pro (5 našumo + 6 efektyvumo branduoliai), macOS 27, .NET SDK 10.0.203, `Release`. Abi versijos sukompiliuotos ir vykdytos tame pačiame kompiuteryje tą pačią dieną (2026-11-11).
- **Maišos funkcija ir antraštės formatas** abiejose versijose tie patys, todėl palyginime nėra maišos funkcijos pakeitimo.
- **Fiksuotos antraštės:**
  - `prev_hash` – 64 vienodi skaitmenys (nuo `1` iki `5`), `tx_root` – 64 vienodos raidės (nuo `b` iki `f`), versija 2;
  - sudėtingumas 9: su juo per 2 mln. bandymų sprendimas nerandamas, todėl kiekvienas matavimas atlieka lygiai 10 000 000 bandymų (įrankis tai patikrina);
  - laikas matuojamas `Stopwatch`, o rezultatai išvedami tik pasibaigus matavimui.
- **Viso rinkinio laikai** – ataskaitos `mining time` ir `total time`:
  - `Runner` laikmatis apima fondo užpildymą, kandidatų sudarymą, kasimą ir priėmimą;
  - neapima generavimo, `inputs.json` įrašymo ir konsolės išvesties, nes ataskaita spausdinama jau baigus matuoti.
- **Vykdymų tvarka:** `v0.2` ir `v0.3` vykdymai kaitalioti, kad kintančios sąlygos (įkaitimas, foniniai procesai) abi versijas paveiktų vienodai.
- **Statistika:** standartinis nuokrypis skaičiuotas kaip imties (n − 1).

Matavimą galima pakartoti:

```
dotnet run --project BlockChain.Bench -c Release -- 5
git worktree add ../blockchain-v0.2 v0.2
cp -R BlockChain.Bench ../blockchain-v0.2/
dotnet run --project ../blockchain-v0.2/BlockChain.Bench -c Release -- 5
```

### 2. Išsaugotų įvesčių patikra įkeliant

**Problema.** `--replay` įkelia `inputs.json`, tačiau `v0.2` tikrino tik JSON sintaksę. Be to, `Program` gaudė tik `IOException`, o `InvalidDataException` nėra jos poklasis. Todėl net sintaksės klaida nutraukdavo programą neapdorota išimtimi: išėjimo kodas 134 ir steko išrašas. Prieštaringi duomenys išryškėdavo tik vykdymo viduryje arba visai nebūdavo pastebimi.

**Pakeitimas.** Naujas [SimulationInputValidator.cs](BlockChain/Transactions/SimulationInputValidator.cs) tikrina:
- **konfigūraciją:** sudėtingumas 1–64, kandidato riba 1–100, teigiami bandymų biudžetai, `TimestampBase` nuo 0 iki 9999 metų;
- **atitikimą sąrašams:** `UserCount` ir `TransactionCount` turi sutapti su sąrašų ilgiais;
- **vartotojus:** raktas – 64 mažieji hex simboliai ir unikalus, balansas ≥ 0, balansų suma neperpildoma;
- **įrašus:** sąrašuose nėra `null`.

Kaip veikia įkėlimas:
- `LoadInputs` pirmiausia nurodo JSON klaidos vietą faile, tada išvardija iki 10 rastų problemų.
- `Program` netinkamą failą praneša išėjimo kodu 3.
- Transakcijų turinys įkeliant netikrinamas. Netinkamus mokėjimus (nežinomas gavėjas, suma 0, pakeistas ID) atmeta priėmimas į fondą ir nurodo priežastį – taip suprojektuota demonstracija.

**Rezultatas.** Vykdyta `--replay <aplankas> --sequential` su demonstracijos `inputs.json`, kuriame pakeistas vienas dalykas:

| Pakeitimas faile | `v0.2`: kodas ir pranešimas | `v0.3`: kodas ir pranešimas |
|---|---|---|
| Nepakeistas | 0 | 0, `blocks.csv` identiškas `v0.2` |
| Aplanko nėra | 1, `Run folder error: …` | 1, tas pats |
| Failas nukirptas | 134, `Unhandled exception. System.IO.InvalidDataException: Invalid simulation input in …` | 3, `… is not valid simulation input JSON: '2' is an invalid end of a number. … Path: $.Transactions[1].Amount \| LineNumber: 45` |
| `"Difficulty": "five"` | 134, ta pati neapdorota išimtis | 3, `… could not be converted to System.Int32. Path: $.Configuration.Difficulty …` |
| Neigiamas balansas | 134, `ArgumentException: Initial balance cannot be negative` | 3, `Users[0].InitialBalance is -1; it cannot be negative.` |
| Pasikartojantis raktas | 134, `ArgumentException: Users cannot share the same public key.` | 3, `Users[1].PublicKey repeats the key of an earlier user.` |
| `null` vartotojas | 134, `ArgumentNullException` | 3, `Users[1] is null.` |
| `null` transakcija | 134, `ArgumentNullException` | 3, `Transactions[0] is null.` |
| `"CandidateLimit": 0` | 134, `ArgumentOutOfRangeException` | 3, `Configuration.CandidateLimit is 0; expected 1 to 100.` |
| `"CandidateLimit": 101` | demonstracijoje 0 (transakcijų per mažai); su pilnu rinkiniu 134 | 3, `Configuration.CandidateLimit is 101; expected 1 to 100.` |
| `"Difficulty": 65` | 134, `ArgumentOutOfRangeException` | 3, `Configuration.Difficulty is 65; expected 1 to 64.` |
| `"InitialMiningAttempts": 0` | 134, `ArgumentOutOfRangeException` | 3, `Configuration.InitialMiningAttempts must be positive.` |
| `"UserCount": 4` (sąraše 3) | 0, neatitikimas nepastebimas | 3, `Configuration.UserCount is 4, but the file lists 3 users.` |

[LoadInputsTests.cs](BlockChain.Tests/Transactions/LoadInputsTests.cs) su `v0.3` kodu praeina 25 iš 25. Tie patys testai su `v0.2` kodu (be vieno, kuris naudoja naują klasę) praeina 2 iš 24:
- **Praeina:** demonstracijos įkėlimas ir trūkstamas failas.
- **Nepraeina 16 duomenų atvejų:** `v0.2` įkeliant jų nepastebi.
- **Nepraeina 5 JSON atvejai:** `v0.2` išmeta `InvalidDataException`, bet pranešime nėra priežasties.
- **Nepraeina** „daug problemų“ testas.

### Testų rezultatai

| Testų rinkinys | `v0.2` kodas | `v0.3` kodas |
|---|---|---|
| `v0.2` testai (101, nepakeisti) | 101 / 101 | 101 / 101 |
| `NonceHasherTests` (18) | nekompiliuojami: nėra `NonceHasher` | 18 / 18 |
| `LoadInputsTests` (25) | 2 / 24 | 25 / 25 |

### Atmesti pasiūlymai

1. **Pataisyti maišos funkcijos rotacijas.** Rotacijos per 73 ir 67 bitus 64 bitų žodyje iš tikrųjų yra rotacijos per 9 ir 3. Atmesta, nes pasikeistų visos maišos, ID ir blokai. Užduotis taip pat draudžia maišos funkcijos pakeitimą maišyti su kodo optimizavimo palyginimu. Funkcija paliekama tokia, kokia buvo 1-ajame darbe.
2. **Greitąjį kelią naudoti ir `BlockValidator`.** Atmesta, nes darbo įrodymo patikra turi perskaičiuoti maišą iš visos antraštės. Be to, nepriklausomas pilnas skaičiavimas tikrintojuje kartu tikrina ir kasėją: jei greitasis kelias suklystų, blokas būtų atmestas.
3. **Įkeliant tikrinti ir transakcijų turinį.** Atmesta, nes tokie mokėjimai yra teisėti įvesties duomenys, kuriuos priėmimas į fondą atmeta su priežastimi. Demonstracija tokių turi tyčia, ir jos pakartojimas nebeveiktų.
4. **Suvienodinti nuoseklaus ir lygiagretaus koordinatoriaus biudžeto ciklus** (kodo struktūra). Atmesta: išmatuojamos naudos nėra, o keičiamas veikiantis ir testais padengtas kodas.
5. **Testų aprėpties matavimas (`coverlet`).** Neatlikta dėl laiko, todėl jokio teiginio apie aprėptį nedarome.

### Išvada

- **Pagerėjo:**
  - kasimas su fiksuotu bandymų skaičiumi greitesnis apie 28 kartus;
  - visas nuoseklus rinkinys sutrumpėjo nuo 108,9 s iki 4,3 s;
  - sugadintas `inputs.json` nebenutraukia programos: visais 11 sugadinto failo atvejų išvedamas aiškus pranešimas su vieta faile ir kodu 3 (`v0.2` – 10 neapdorotų išimčių, įskaitant `CandidateLimit: 101` su pilnu rinkiniu, ir 1 nepastebėta klaida).
- **Nepasikeitė:**
  - maišos funkcija ir blokų formatas;
  - visos maišos, nonce, kandidatai ir galutiniai balansai;
  - `BlockValidator`, `v0.2` testų rezultatai;
  - lygiagretaus kasimo pagreitėjimas (×4,5–4,6).
- **Kaina:**
  - Maiša dabar skaičiuojama dviem keliais: greituoju kasėjui ir pilnu tikrintojui. Jų atitiktį saugo testai, o `SerializeHeader` sudaryta iš tų pačių dalių, kurias naudoja kasėjas.
  - Lygiagretus režimas tapo dar mažiau nuspėjamas: `v0.3` visi 5 vykdymai davė skirtingas grandines (`v0.2` – 2 skirtingas).
- **Likusios ribos:**
  - ne kasimo darbas (~1 s) dabar sudaro 23 % nuoseklaus ir ~60 % lygiagretaus vykdymo laiko ir nebuvo optimizuotas;
  - kasimo bandymo kaina priklauso nuo nonce ilgio.

## DI naudojimas

- **Claude Code** (modelis Claude Opus 5.5, Anthropic), nuo 2026-10-28, naudojo IvarasJoma. Įrankiui kaip užklausos buvo pateiktos žingsninės specifikacijos; jis parašė blokų modelius, antraštės maišą, `tx_root` ir Merkle medį, genesis bloką, kasėją, bloko tikrintoją, priėmimą į grandinę, vykdymo ciklą, ataskaitą ir jos formatavimą, paleidimo režimus ir `blocks.csv`, v0.2 validacijos testus, `v0.3` patobulinimus ir šį README. Kiekvienas žingsnis prieš commit buvo sukompiliuotas ir patikrintas testais; kasimas papildomai patikrintas išmatavus bandymų pasiskirstymą, o pakartojimai – palyginus `blocks.csv`.

**`v0.3` užklausų žurnalas** (Claude Code, Claude Opus 5.5; įrankis turėjo prieigą prie viso repozitorijos kodo ir užduoties PDF):

| Data | Užklausa (originalo kalba) | Kas gauta ir kaip panaudota |
|---|---|---|
| 2026-11-05 | „what could i do for v0.3“ | Trys kryptys su įrodymų planu: (1) kasimo ciklo greitis, (2) išsaugotų įvesčių patikra, (3) testų aprėptis. Įrankis pats paleido `--replay` su sugadintu `inputs.json` ir parodė, kad programa nulūžta kodu 134. Pasirinktos 1 ir 2 kryptys. |
| 2026-11-11 | „what is required from v0.3“ | Užduoties 5 ir 6 skyrių reikalavimų santrauka (užfiksuota `v0.2`, bent du patobulinimai, žurnalas, atskiri commit, ≥ 5 matavimai su sklaida). |
| 2026-11-11 | „sugeneruok readme ir pridek utxo modeli“ | Šio README pagrindas. Sugeneruotas UTXO modelis į projektą neįtrauktas – nuspręsta laiką skirti `v0.3`. |
| 2026-11-11 | „revert the utxo, implement v0.3 instead“ | Abiejų krypčių kodas ir testai, `BlockChain.Bench` matavimo įrankis, `v0.2`/`v0.3` matavimai ir [palyginimo skyrius](#v03-palyginimas-su-v02). Priimti ir atmesti pasiūlymai bei jų patikra aprašyti ten. |

> TODO: nurodyti DI įrankius, naudotus transakcijų servisui, kandidatų koordinatoriui, lygiagrečiam kasimui ir maišos funkcijai, ir pridėti užklausų žurnalą (arba glaustą sąveikos žurnalą) su datomis.

## Narių indėliai

| Narys | Darbai |
|---|---|
| Ignas Šimaitis | Repozitorijos paruošimas; maišos funkcija (1-asis darbas); bendri transakcijų modeliai ir `ITransactionService` kontraktas; `TransactionCodec`; `TransactionService` (generavimas, demonstracija, įvesties išsaugojimas ir įkėlimas, priėmimas į fondą, kandidatų atranka, `PrepareApply` / `CommitAccepted`, likusių transakcijų paaiškinimai); v0.2 penki kandidatai, `SearchRange`, kandidatų koordinatorius ir jo testai; lygiagretus kasimas ir jo testai; integracija |
| IvarasJoma | Blokų modeliai; antraštės maiša, `tx_root` ir genesis; kasėjas; bloko tikrintojas; priėmimas į grandinę; vykdymo ciklas, ataskaitos išvestis, paleidimo režimai ir `blocks.csv`; testų projektas; v0.2 Merkle medis, bloko formato versija ir trijų patikros etapų testai; `v0.3` patobulinimai (greitasis kasimo kelias, išsaugotų įvesčių patikra), jų testai, matavimo įrankis ir `v0.2`/`v0.3` palyginimas; README |
