# Számlázó

Magyar nyelvű, helyben futtatható háztartási kiadásnapló. A webes felület Angular 22, az API ASP.NET Core 10, az adattárolás SQLite. Az alkalmazás saját gépen, külső AI API és előfizetés nélkül használható.

## Indítás

Docker Engine és Docker Compose szükséges. A projekt gyökérkönyvtárában futtasd:

```powershell
docker compose up --build -d
```

Nyisd meg a [Számlázót](http://localhost:8087), és hozz létre egy helyi fiókot. Az **Áttekintés** lapon beállíthatod az adott hónap keretét. A **Kiadások** lapon megkeresheted és módosíthatod a mentett bejegyzéseket. Az **Elemzés** lapon a kategóriák, a naptári napok és a hét napjai szerint láthatod a költéseket. A **Termékek** lapon tejet, kenyeret vagy más blokktételt kereshetsz, boltra szűrhetsz, és láthatod a talált tételek összegét.

A csak helyben elérhető konténer a 8087-es porton figyel. Az SQLite-adatbázis és a munkamenet kulcsai a `szamlazo_szamlazo-data` Docker-kötetben maradnak leállítás és új build után is. Leállítás a kötet megtartásával:

```powershell
docker compose down
```

Naplók megtekintése:

```powershell
docker compose logs -f app
```

## Importálás

A helyi valódi tesztblokk a `test-images/spar-real-receipt.jfif` fájlban található, ha a mintát bemásoltad. A könyvtár Gitből és Docker-buildből kizárt. A blokktételekhez az import mennyiséget, mértékegységet és egységárat is átadhat, a tranzakcióhoz fizetési módot és időpontot. Ezeket új EF Core-migráció tárolja, a korábbi rekordoknál opcionális mezőként.

A fotóhoz perspektíva- és kontrasztjavított OCR-jelölt is készül. A felismerést mentés előtt ellenőrizni kell, különösen a ferde, hajtott vagy halvány blokkoknál.

A mellékelt valódi SPAR-fotó automatikus felismerése még pontatlan. A tételértelmező javított szövegből kezeli a négy terméket, a súlyokat és a Mastercard fizetést, de a fényképből kiolvasott eredményt jelenleg kézzel kell javítani.

A Kiadások keresője terméknévre a hozzá tartozó tételsorokat és azok összegét mutatja. A Termékek lapon ezen belül konkrét boltra is szűrhetsz.

- **Fénykép**. JPG, PNG, WEBP és TIFF. A konténer helyi Tesseract OCR-t futtat magyar és angol nyelvi modellel.
- **PDF**. Oldalanként szövegkinyerés, ennek hiányában OCR. Egy fájlban egy számla vagy blokk, legfeljebb öt oldal.
- **Táblázat**. XLS, XLSX és CSV. Az első munkalap vagy CSV első sorában a `Név`, `Dátum`, `Összeg` oszlopok szerepeljenek. A `Kategória` oszlop opcionális. Egyszerre legfeljebb 200 kiadás importálható.
- **Kézi rögzítés**. Név, dátum, forintösszeg és kategória, igény szerint a blokk tételeivel.

Minta CSV:

```csv
Név;Dátum;Összeg;Kategória
SPAR;2026-11-05;5290;Élelmiszer
BKK;2026-11-06;8950;Közlekedés
```

Az import előnézetet készít. Mentés előtt javíthatod a kereskedő nevét, a dátumot, az összeget és a felismert tételeket. Az app jelzi, ha a tételsorok és a végösszeg eltérnek. A mentett adatok a saját fiókodhoz tartoznak. A feltöltött eredeti fájlt és a nyers OCR-szöveget az alkalmazás feldolgozás után elengedi.

## Az első verzió határai

A költési kimutatás a jóváhagyott, forintban megadott kiadásokat a bejegyzés kategóriájában összesíti. A blokktételek szerkeszthetők és kereshetők. A tételkinyerés szabályalapú, így a bolti formátum, a fotó minősége és a kedvezmények miatt ellenőrzést igényelhet. A regisztráció helyi fiókot hoz létre e-mail-ellenőrzés nélkül. A build letöltéseket igényel, a számlafeldolgozás a futó konténerben történik.

## Backend felépítés

- `backend/Domain` – kiadások, blokk­tételek, költségkeretek és a költési típusok enumjai.
- `backend/Application` – kiadási és kimutatási szolgáltatás, DTO-k, illetve az adattárolás és dokumentumolvasás interfészei.
- `backend/Infrastructure` – EF Core SQLite tároló és migrációk, Identity, PDF-feldolgozás, Tesseract és Excel-olvasás.
- `api/Controllers` – munkamenet, kiadások és import HTTP-végpontjai. Az `api/Program.cs` állítja össze a rétegeket.

Visual Studióban a `Szamlazo.slnx` megoldást, VS Code-ban a projekt gyökérmappáját nyisd meg.

### EF Core-migrációk

Az alkalmazás induláskor futtatja a függő migrációkat. A korábbi, migrációk nélkül létrejött adatbázist akkor veszi át, ha a sémája egyezik az első migrációval. Új migráció felvétele a projekt gyökeréből:

```powershell
dotnet tool restore
dotnet ef migrations add ValtozasNeve --project backend/Infrastructure --startup-project api --output-dir Persistence/Migrations
```

## Build ellenőrzése

```powershell
dotnet build Szamlazo.slnx
npm.cmd --prefix web ci
npm.cmd --prefix web run build
```
