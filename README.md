# Ada – saját menedzser

Egyfelhasználós, ingyenesen hosztolt app. Androidon PWA-ként fut, weben böngészőből.

- **Eisenhower mátrix**: kézi fontos/sürgős jelölés, opcionális határidő. 2×2 panel kártyákkal: desktopon drag & drop,
  telefonon tömör mini-kártyák, hosszú nyomásos húzással. A „Kész” visszavonható; a kész feladatok kereshetők.
  Minden feladathoz mini teendők (pipálható lista) és időbélyeges „történet” bejegyzések vehetők fel; ezek azonnal
  mentődnek, és az utolsó teendő kipipálása sem zárja le a feladatot. Kész feladatnál csak olvashatók.
- **Ritka ismétlődő feladatok** (olajcsere, kazán): „N havonta”, fix ütemezés horgonydátumból. Esedékesség előtt
  `LeadDays` nappal (alapból 14) automatikusan bekerül a mátrixba. Teljesítési naplót vezet (dátum + megjegyzés).
  Generált feladat törlése = az alkalom kihagyása.
- **Születésnapok**: kézi felvitel, állítható előrejelzés (alapból 30 nap). Feb 29 → nem szökőévben Feb 28.
- **Időjárás**: Open-Meteo (kulcs nélkül), egy beállítható helyszín. A kezdőképen ma + 2 nap; kattintásra heti
  előrejelzés, naponként lenyíló órás bontással.
- **Szakik**: megbízható szakemberek (foglalkozás, név, telefon, e-mail, weboldal, megjegyzés) kártyákon,
  foglalkozás szerinti sorrendben. Keresés foglalkozásra (ékezet nélkül is); a szaki ablakából az adatai
  „Címke: érték” soronként a vágólapra másolhatók.
- **Versenyek** (az egykori Adaminator): egyenes kiesés (bronzmeccs opcionális), dupla kiesés, körmérkőzés,
  csoportkör + rájátszás (körmérkőzéses csoportok vagy svájci rendszer; egyenes vagy dupla kieséses rájátszás).
  Résztvevők emojival, sorsolt és átrendezhető kiemelés, erőnyerők. Meccsformátum 1 játszmától 7-ből 4-ig
  (2 játszmánál döntetlen is lehet), játszmás vagy pontos számolás. Eredményrögzítés játszmánként, feladás, az utolsó
  eldőlt meccs visszavonása, holtverseny-meccsek, végeredmény, tabellák.
  Minden versenynek van **nyilvános, csak olvasható nézői linkje** (`/nezo/{token}`, bejelentkezés nélkül).
- **Unmatched** (`/unmatched`): a házi Unmatched pontozótábla képregényborítóként, Fiúk – Lányok.
  Bárki megnézheti a linkkel; a szerkesztő a VS-jelvény 5 gyors koppintására nyílik, menteni csak bejelentkezve lehet.
- **Témák**: Piros és Kék (mozgó háttér), Sötét és Világos (statikus). Eszközönként tárolódik.

## Felépítés

| Projekt | Szerep |
|---|---|
| `src/AdaManagement.Server` | ASP.NET Core minimal API, EF Core (SQL Server), BFF Google login. Ez szolgálja ki a WASM klienst is. |
| `src/AdaManagement.Client` | Blazor WebAssembly PWA, MudBlazor |
| `src/AdaManagement.Shared` | DTO-k, közös születésnap-számolás, a versenyág rajzának számítása (`Tournaments/BracketLayout.cs`) |
| `src/AdaManagement.Server/Tournaments` | A versenymodul: Domain (ágak, sorsolás, tabella), Application (szolgáltatások, validátorok), EF-leképezés a `tournament` sémában |
| `tests/AdaManagement.Tests` | xUnit: doménlogika (ismétlődés, generálás, születésnap, versenyágak), és végpont-tesztek valódi SQL Serveren (Testcontainers, Docker kell hozzá) |
| `infra/main.bicep` | Azure: App Service F1 + Azure SQL free offer + 1 €-s budget riasztás |
| `.github/workflows/deploy.yml` | teszt → publish → EF migration bundle → deploy |

Fontosabb döntések:

- **Gyors indulás:** a PWA a service worker cache-ből indul. Az utolsó dashboard a localStorage-ból azonnal látszik
  (csak olvasható), a friss adat a háttérben jön. Az F1 és a serverless SQL alvás után 10–60 mp-ig ébred.
- **Nincs keep-alive ping:** a DB ébrentartása elégetné az SQL free offer havi keretét.
- **Auth:** a szerver intézi a Google logint, a böngésző csak HttpOnly cookie-t kap (180 nap, csúszó lejárat).
  Csak az `Auth:AllowedEmails` címei léphetnek be. A cookie-kulcsok a DB-ben vannak, így az újraindulás nem léptet ki.
  CSRF ellen: SameSite=Lax cookie, és minden nem-GET `/api` hívásnak `X-Ada-Client` fejlécet kell küldenie;
  a kijelentkezés csak saját oldalról (Origin-ellenőrzés). Az app nem ágyazható be idegen oldalba (`frame-ancestors 'none'`).
- **Időzóna:** a „ma” mindig Europe/Budapest szerint számolódik.
- **Versenyek:** az Adaminator Domain és Application rétege lényegében változatlanul jött át (saját névtérben, magyar
  hibaüzenetekkel), a felület Blazorban újra lett írva. Nincs időzített frissítés: az állás oldalbetöltéskor vagy a
  Frissítés gombra töltődik, így a nézői oldal sem tartja ébren az SQL-t.
- **Nyilvános végpontok:** az `/api/public` csoport bejelentkezés nélkül, csak olvasva érhető el (nézői oldal a titkos
  tokennel, Unmatched tábla). Minden írás az `/api` alatt marad, bejelentkezéshez kötve.
- **Karakterképek:** az Unmatched 32 képe (`wwwroot/characters/`) nincs a service worker előtöltésében, csak az oldal
  megnyitásakor töltődik.
- **Hideg indulás:** F1-en nincs Always On, az SQL 60 perc után szünetel, és keep-alive pinget szándékosan nem
  használunk. Helyette a szerver ReadyToRun módban fordul (gyorsabb indulás), és induláskor rögtön felébreszti az
  adatbázist, hogy a két ébredés párhuzamosan menjen.
- **Frissítés:** az új service worker azonnal átveszi az irányítást (`skipWaiting`), az oldal egyszer újratölt, így
  egy push után nem marad a régi, gyorsítótárazott verzió.

## Helyi futtatás

Követelmény: .NET 10 SDK, SQL Server LocalDB (Visual Studióval települ).

```bash
dotnet tool restore
```

```bash
dotnet dotnet-ef database update --project src/AdaManagement.Server
```

```bash
dotnet run --project src/AdaManagement.Server --launch-profile http
```

Megnyitás: http://localhost:5146. Fejlesztéskor, ha nincs Google kliens beállítva, automatikusan belép a
`dev@localhost` címmel (`/account/dev-login`, ez csak Development környezetben létezik).

Tesztek:

```bash
dotnet test
```

Valódi Google loginnal, helyben (user-secrets, nem kerül a repóba):

```bash
dotnet user-secrets --project src/AdaManagement.Server set "Auth:Google:ClientId" "<client-id>"
```

```bash
dotnet user-secrets --project src/AdaManagement.Server set "Auth:Google:ClientSecret" "<secret>"
```

```bash
dotnet user-secrets --project src/AdaManagement.Server set "Auth:AllowedEmails:0" "<te@gmail.com>"
```

## Élesítés (egyszer kell végigcsinálni)

### 1. Google OAuth kliens (ingyenes, számlázás nélkül)

1. https://console.cloud.google.com → új projekt.
2. *APIs & Services → OAuth consent screen*: External, Testing mód, tesztfelhasználónak add hozzá a saját címed.
3. *Credentials → Create credentials → OAuth client ID → Web application*.
   Authorized redirect URI: `https://<namePrefix>.azurewebsites.net/signin-google` (helyi teszthez még: `http://localhost:5146/signin-google`).
4. Jegyezd fel a Client ID-t és a Client secretet.

### 2. Azure erőforrások

Azure CLI: `winget install Microsoft.AzureCLI`, majd `az login`.

```bash
az group create -n ada-rg -l westeurope
```

Másold le az `infra/main.parameters.example.json` fájlt `infra/main.parameters.json` néven, töltsd ki
(a `.gitignore` kizárja), majd:

```bash
az deployment group create -g ada-rg -f infra/main.bicep -p infra/main.parameters.json -p sqlAdminPassword="<erős jelszó>" googleClientSecret="<secret>"
```

**Ellenőrizd a portálon**, hogy az SQL adatbázisnál a *Free offer* aktív, és a „when free limit is reached” beállítás
**Auto-pause the database until next month**. Ez garantálja, hogy az SQL nem számláz.

### 3. GitHub Actions

1. Privát repó, push.
2. Entra ID → *App registrations* → új app → *Certificates & secrets → Federated credentials* → GitHub Actions,
   a repóra, *Environment: production*.
3. Ennek az appnak adj **Contributor** szerepet az `ada-rg` erőforráscsoportra.
4. A repó *Settings → Secrets and variables → Actions* menüjében:
   - Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SQL_CONNECTION_STRING`
     (ugyanaz, mint a Bicepben, `Connect Timeout=60`-nal)
   - Variables: `AZURE_RESOURCE_GROUP` (`ada-rg`), `AZURE_SQL_SERVER` (`<namePrefix>-sql`, vagy a `sqlServerName` paraméter, ha megadtad), `AZURE_WEBAPP_NAME` (`<namePrefix>`)
5. Hozz létre egy `production` environmentet. Ezután minden push a mainre tesztel, migrál és deployol.

### 4. Telefonra

Androidon Chrome-ban nyisd meg a `https://<namePrefix>.azurewebsites.net` címet, menü → **Alkalmazás telepítése**
(vagy „Hozzáadás a kezdőképernyőhöz”).

## Költségvédelem – ellenőrzőlista

- App Service plan: **F1 (Free)**. Ne válts B1-re, mert az fizetős.
- SQL: **Free offer**, kimerüléskor **Auto-pause**. Ezt nem lehet visszaállítani, ha egyszer „continue using with charges”-re váltasz.
- Budget riasztás: 1 €, már az első centnél e-mailt küld.
- Havonta egyszer: *Cost Management → Cost analysis* → 0,00 €.
