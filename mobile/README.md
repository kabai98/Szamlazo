# Számlázó mobil

Külön Ionic 9 + Angular 22 frontend, Capacitor 8 Android projekttel. A meglévő ASP.NET Core API-t használja; nem futtat külön OCR-t vagy adatbázist a telefonon.

## Böngészős fejlesztés

A repository gyökerében:

```powershell
docker compose up -d
npm.cmd --prefix mobile ci
npm.cmd --prefix mobile start
```

Nyisd meg a `http://localhost:8100` címet. Az `/api` proxy a Dockerben futó `http://127.0.0.1:8087` API-ra mutat. A belépéshez ugyanaz a fiók használható, mint a weben. A meglévő helyi bemutató-fiók adatai a Gitből kizárt `local-config/development.json` fájlban találhatók, ha ezen a gépen létrehoztuk. Más gépen regisztrálj új fiókot.

Böngészőben a kamera gomb képfájlválasztót nyit; mobilböngésző támogathatja a `capture` kérést. A natív Android app a Capacitor kamera- és galériaválasztóját használja.

## Android

Android Studio, Android SDK 36 és JDK 21 szükséges. A natív projekt a `mobile/android/` mappában van.

```powershell
npm.cmd --prefix mobile run android:sync
npm.cmd --prefix mobile run android:open
```

A `android:sync` alapértelmezett API-címe `http://10.0.2.2:8087/api`, ez az Android emulátorból a gépedet jelenti. A forráskód és az eszközön futó app helyi adatai nem kerülnek a backend helyére.

USB-n csatlakoztatott saját Android-telefonhoz:

```powershell
adb reverse tcp:8087 tcp:8087
$env:MOBILE_API_URL = 'http://localhost:8087/api'
npm.cmd --prefix mobile run android:sync
```

Ezután Android Studióból indítsd az appot a telefonon, vagy telepítsd az elkészült debug APK-t. A Docker maradhat a gép `127.0.0.1:8087` címén; nem szükséges hálózati portot megnyitni. A telefon `localhost` címe csak az `adb reverse` mellett éri el a gépet. USB-kapcsolat nélkül valódi, a telefonról elérhető HTTPS API-cím szükséges.

```powershell
$env:JAVA_HOME = 'C:\path\to\jdk-21'
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
Set-Location mobile/android
.\gradlew.bat assembleDebug
```

APK: `mobile/android/app/build/outputs/apk/debug/app-debug.apk`. A sima HTTP csak a debug Android-manifestben engedélyezett. Éles API-hoz állíts be HTTPS-címet, és a sync előtt `$env:MOBILE_RELEASE = 'true'` értéket; a release buildhez külön aláírás és Android Studio/Gradle release konfiguráció szükséges. A `MOBILE_RELEASE` a címellenőrzést szigorítja, önmagában nem készít release APK-t.

A generált `environment.native.ts`, az Android SDK helyét tartalmazó `local.properties` és az aláíró kulcsok Gitből kizártak. Az API-cím buildbeállítás, nem a későbbi felhasználói Beállítások menü része.

## Felépítés és működés

- `src/app/core/`: DTO-k, API/session/CSRF kliens, natív fotókezelés és validáció.
- `src/app/auth/`: belépés/regisztráció.
- `src/app/scan/`: kiválasztás, előnézet, feldolgozás, javítás és mentés.
- `app.component.*`: havi összesítés, navigáció, kiadások és termék szerinti keresés.

A natív app `CapacitorHttp`-val továbbítja a `fetch` kéréseket és multipart feltöltéseket. A backend sütis belépése és CSRF-ellenőrzése változatlan; nincs jelszó vagy session token a böngészős localStorage-ban. A böngészős fejlesztés az azonos eredetű proxyt használja. Az OCR-előnézet mentése új kiadást hoz létre; egy sikertelen válasz utáni újrapróbálás ugyanazokat az azonosítókat használja.

## Ellenőrzés

```powershell
npm.cmd --prefix mobile test
npm.cmd --prefix mobile run build
```

Telefonos ellenőrzés: belépés → kamera és galéria → előnézet → feldolgozás → hiányzó dátum/hibás ár javítása → mentés → visszaolvasás ugyanazon fiókkal weben is. Próbáld a kamera megszakítását, a túl nagy fájlt, a hálózati hibát és az explicit kedvezmény melletti negatív termékár elutasítását is. Egy másik fiókban ne látszódjanak a mentett adatok.

A [további lépések](../docs/mobile-roadmap.md) külön brancheken készülnek. Az eredeti fájlok megőrzése és a Beállítások menü ebben a lépésben nem változik.

Hivatalos referenciák: [Ionic Angular](https://ionicframework.com/docs/angular/overview), [Capacitor Camera](https://capacitorjs.com/docs/apis/camera), [Capacitor HTTP](https://capacitorjs.com/docs/apis/http).
