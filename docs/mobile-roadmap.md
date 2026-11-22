# Mobilos fejlesztési lépések

Minden lépés önálló, a friss `develop` ágból induló branch. Egy következő lépés csak az előfeltételek beolvasztása után induljon.

## 1. Ionic Android alkalmazás

Branch: `feature/ionic-android-app`.

- Külön `mobile/` Angular + Ionic projekt, Capacitor Android-csomagolással.
- Meglévő fiók használata, regisztráció és kijelentkezés; meglévő session/CSRF API.
- Havi áttekintés, kiadások és termék szerinti keresés.
- Kamera vagy galéria → helyi képelőnézet → feltöltés a meglévő `/api/import` végpontra → adatok javítása → mentés → visszaolvasás.
- Az OCR-algoritmus és a szerveroldali fájlmegőrzés ebben a lépésben változatlan.
- Elfogadás: mobil/web build, validációs tesztek, backendtesztek, szintetikus feltöltés/javítás/mentés, Android APK build; valódi telefonos kamera/galéria próba külön, ha van elérhető eszköz.

## 2. Beállítások menü

Tervezett branch: `feature/account-settings`.

- Felhasználónkénti beállítások az Application DTO/service, Domain és EF Core réteghatárok szerint.
- Beállítások menü a webes és mobilos felületen.
- Szükséges migráció, alapértékek és fiókelkülönítési tesztek.
- Az eredeti fájl megőrzésének kapcsolója csak a tényleges tárolás elkészültével legyen elérhető.

## 3. Eredeti feltöltések opcionális megőrzése

Tervezett branch: `feature/optional-upload-retention`.

- Alapértelmezés szerint maradjon kikapcsolva. Bekapcsolva a feltöltött képek/PDF-ek/táblázatok privát tárolóba kerüljenek.
- A tárolás külön Infrastructure szolgáltatás legyen, Application szerződéssel. A controller maradjon vékony.
- Fájlmetaadat, tulajdonos és import/kiadás kapcsolat EF Core migrációval; sikeres és megszakadt importok életciklusa legyen meghatározva.
- Ne kerüljenek a publikus `wwwroot` alá vagy Gitbe; letöltés csak a saját fióknak, fájltípus- és méretellenőrzéssel.
- Elfogadás: kapcsoló működése, hibás/megszakadt feltöltés utáni takarítás, adatmegőrzés, fiókelkülönítés.

## 4. Mentett bizonylatok kezelése

Tervezett branch: `feature/receipt-attachments`.

- Az eredeti dokumentum megnyitása a kiadás mellett weben és mobilon.
- Egyértelmű törlés és megőrzési szabály; a kiadás és a fájl törlésének kapcsolata legyen látható.
- Árva fájlok takarítása, letöltési/törlési jogosultság és mobilos megnyitás tesztelése.

## Jelenlegi tárolás

A szerver ideiglenes könyvtárba másolja a feltöltést. A `DocumentReader` a feldolgozás után, hiba esetén is törli ezt a könyvtárat. Az import csak előnézetet ad: az adatbázisba külön mentéssel kerülnek a jóváhagyott kiadások és terméksorok. Az eredeti fájl és a nyers OCR-szöveg jelenleg nem kerül tartósan az adatbázisba. A készülék kameraalkalmazásának saját ideiglenes fájljai ettől függetlenek.
