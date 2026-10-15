# TutorConnect – dokumentáció: kitöltött üres pontok

A szövegek a PDF (`TutorConnect_dokumentacio.pdf`) szerkezetét követik, és a repository tényleges kódjából készültek (`vsc/js/main.js`, `auth.js`, `creator.js`, `sql/zsamo.sql`, `vsc/test`, `Desktop/`).
A `[ELLENŐRIZD]` jelzésű helyeken olyan dolgot írtam, amit a kódból nem tudhatok biztosan (pl. ki mit csinált, képernyőképek) – ezeket javítsd.
A képeket (ER-diagram, képernyőképek) nem tudom elkészíteni, ezekhez megadtam, mit érdemes beletenni.

---

## 1. Bevezetés

### 1.1 A projekt célja
A TutorConnect egy webalkalmazás, amely diákokat és korrepetálókat kapcsol össze. A diák megtalálhatja azt az oktatót, aki abból a tantárgyból tud segíteni, amiben ő bizonytalanabb; az oktató pedig megjelenhet, megadhatja a tantárgyait, óradíját és szabad időpontjait. A rendszer a teljes folyamatot egy helyen kezeli: keresés, kapcsolatfelvétel (chat), időpontfoglalás és az óra utáni értékelés.

### 1.2 A megoldandó probléma
Sok korrepetáló nehezen hirdeti magát, a hirdetéseit alig veszik észre, a diákok pedig nem tudják, merre keressenek megbízható, elérhető és megfizethető oktatót. A keresés ma szétszórt (közösségi oldalak, ismerősök, apróhirdetések), nincs egységes ár- és időpont-információ, és nincs visszajelzés a korábbi órák minőségéről. A TutorConnect egy közös felületen gyűjti az oktatókat tantárgy és ár szerint szűrhetően, látható értékelésekkel és foglalható időpontokkal, így mindkét fél gyorsabban talál egymásra.

### 1.3 Célcsoport / felhasználók
- **Diákok:** általános iskolások, középiskolások és egyetemi hallgatók, akik egy vagy több tantárgyból segítséget keresnek.
- **Oktatók (korrepetálók):** segítőkész, jól teljesítő diákok, egyetemi hallgatók, tanárok és egyetemi oktatók, akik órákat szeretnének adni.
- **Adminisztrátorok:** a rendszer üzemeltetői, akik minden oldalt látnak.

### 1.4 A rendszer fő funkciói
- Regisztráció (diák vagy korrepetáló szerepkörrel) és bejelentkezés; a belépés süti alapján 7 napig megmarad.
- Tantárgyak böngészése és szűrése kategóriák szerint (Reál, Humán, Nyelvek, Természettudomány, Művészet, Gazdaság, Egyéb).
- Oktatók keresése és szűrése (tantárgy, óradíj), az oktatók értékeléseinek megtekintése.
- Oktatók értékelése 1–5 csillaggal és szöveges véleménnyel (a lezajlott óra után).
- Időpontfoglalás: a diák kér, az oktató elfogadja vagy elutasítja, mindkét fél lemondhatja.
- Valós idejű chat diák és oktató között (WebSocket), fájl- és képcsatolással, üzenet szerkesztéssel és visszavonással.
- Profil szerkesztése, fiók törlése, világos/sötét téma.
- Főoldali statisztikák, GYIK oldal.

---

## 2. Követelmények

### 2.1 Funkcionális követelmények
1. Működő regisztráció és bejelentkezés, kijelentkezés.
2. Tantárgyak listázása és szűrése kategóriák szerint.
3. Oktatók keresése, ár alapú szűrés (óradíj tantárgyanként).
4. Oktató és diák közötti chat (szöveg, képek, dokumentumok, olvasottság jelzés).
5. Foglalási menü: szabad időpont kiválasztása, foglalási kérelem, elfogadás/elutasítás/lemondás, közelgő foglalások listája.
6. Oktatók értékelése az óra után.
7. Az oktató szabad időpontjainak kezelése.
8. Profil szerkesztése (oktató: bemutatkozás, tantárgyak, óradíj; diák: iskolatípus, osztály, tantárgyak, amiből segítséget kér).
9. Gyakran ismételt kérdések (GYIK) oldal.

### 2.2 Nem funkcionális követelmények
- **Biztonság:** a jelszavakat Argon2id-vel hash-eljük (memóriaköltség 2^15, időköltség 4, párhuzamosság 1); a munkamenet-süti `httpOnly` és `sameSite=lax`; a szerver forráskódja nem érhető el a böngészőből (fehérlistás `/js` kiszolgálás); a feltöltések véletlen fájlnévvel, engedélyezett kiterjesztésekkel, 10 MB-os korláttal kerülnek mentésre.
- **Munkamenet:** süti alapú bejelentkezve maradás, 7 nap.
- **Használhatóság:** animációk, sötét/világos téma.
- **Statisztikák** a főoldalon (pl. oktatók száma).
- **Reszponzivitás:** telefonon és közepes monitoron is megfelelően működik.
- **Megbízhatóság:** hibás bemenetre érthető magyar hibaüzenet, adatbázis-hiba esetén sem omlik össze a szerver.

### 2.3 Felhasználói szerepkörök
- **Vendég (nincs bejelentkezve):** tantárgyakat és oktatókat böngészhet, olvashatja a GYIK-et és az értékeléseket; foglalni és csevegni nem tud.
- **Tanuló (STUDENT):** böngészés, csevegés az oktatókkal, időpontfoglalás, saját foglalások megtekintése és lemondása, oktató értékelése, profilszerkesztés.
- **Korrepetáló (TUTOR):** csevegés a diákokkal, szabad időpontok kezelése, foglalási kérelmek elfogadása/elutasítása, a beszélgetőpartner diák profiljának megtekintése, saját profil (tantárgyak, óradíj) szerkesztése.
- **Admin (ADMIN):** minden oldalt lát. Admin fiók regisztrációval nem hozható létre, csak az adatbázisban.

---

## 3. Rendszerterv

### 3.1 Architektúra
Háromrétegű, kliens–szerver felépítésű webalkalmazás:
1. **Megjelenítési réteg:** HTML + CSS + JavaScript (framework nélkül), a böngészőben fut.
2. **Alkalmazás réteg:** Node.js + Express szerver (`vsc/js/main.js`), amely kiszolgálja az oldalakat, REST API-t biztosít, a munkameneteket kezeli, és egy WebSocket szervert futtat a chathez.
3. **Adatréteg:** MySQL adatbázis (`zsamo`).

A szerver a 3000-es porton fut (`http://localhost:3000`). Emellett készült egy Windows Forms asztali alkalmazás váza is (`Desktop/TutorConnectApp`, .NET), amely egyelőre csak a navigációs keretet (Profil, Beállítások) tartalmazza.

### 3.2 A rendszer komponensei
**Szerveroldal** (`vsc/js`):
- `main.js` – az Express szerver: oldalak, REST API, munkamenet, fájlfeltöltés (multer), WebSocket, adatbázis-hozzáférés.
- `auth.js` – jelszó hash-elés és ellenőrzés Argon2id-vel (`hashPassword`, `verifyPassword`).

**Kliensoldal** (`vsc/js`, `vsc/html`, `vsc/css`):
- `creator.js` – minden oldalon felépíti a navigációs sávot (a szerepkör szerint), a profil-menüt, a téma kapcsolót és a fiókbeállítások ablakot.
- `home.js` – a főoldal (statisztikák, diavetítés).
- `index.js` – a tantárgyak oldal (`subs.html`) logikája.
- `dropdown.js`, `selectmenu.js` – egyedi legördülő menük.
- `oktatok.js` – oktatók listázása, szűrés, értékelések.
- `booking.js`, `foglalas.js`, `foglalasutan.js` – időpontfoglalás, foglalási oldal, foglalások utáni oldal.
- `chat.js` – a chat oldal, WebSocket kliens, csatolmányok, képnézegető.

### 3.3 Kommunikáció a komponensek között
- **Böngésző → szerver:** `fetch` hívások a REST API-ra JSON-nal; a böngésző minden kéréssel automatikusan küldi a `connect.sid` munkamenet-sütit.
- **Bejelentkezés állapota:** a `creator.js` minden oldalbetöltéskor lekérdezi a `/api/me` végpontot, és ez alapján építi fel a navigációt.
- **Valós idejű értesítések:** a kliens WebSocket kapcsolatot nyit a szerverhez (a belépéskor kapott süti azonosítja). A szerver `new_message` (új üzenet) és `booking_update` (foglalás változott) eseményeket küld a másik félnek.
- **Szerver → adatbázis:** `mysql2` kapcsolat, paraméterezett SQL lekérdezésekkel.
- Az üzenetek és az oldalak kiszolgálását elsősorban a `main.js` kezeli.

### 3.4 REST API
A kliens és a szerver között a kommunikáció egy REST API-n keresztül zajlik, amelyet az Express szerver biztosít. A böngésző HTTP-kérésekkel (GET, POST, PUT, PATCH, DELETE) éri el a végpontokat, az adatok pedig JSON formátumban közlekednek. Az API erőforrások köré szerveződik (pl. `/api/tutors`, `/api/bookings`, `/api/conversations`), a végpontok teljes listája a 6.2 pontban található.

A kérések feldolgozása egységes módon történik: a szerver ellenőrzi a bejelentkezést és a szerepkört, érvényesíti a bemenetet, majd az adatbázisból kiolvassa vagy abba elmenti az adatot. Sikeres kérésnél JSON választ kapunk. Hiba esetén a megfelelő HTTP státuszkód (400, 401, 403, 404, 409 vagy 500) és egy `{ "error": "..." }` törzs érkezik, benne érthető magyar hibaüzenettel. A védett végpontok bejelentkezés nélkül 401-es választ adnak.

### 3.5 Alkalmazott technológiák
| Terület | Technológia |
|---|---|
| Backend | Node.js, Express 5 |
| Munkamenet | express-session (süti, 7 nap) |
| Valós idejű kommunikáció | ws (WebSocket) |
| Fájlfeltöltés | multer |
| Adatbázis | MySQL (mysql2 kliens), XAMPP-pal futtatva |
| Frontend | HTML, CSS, JavaScript |
| Jelszó hash-elés | Argon2id (argon2 csomag) |
| Tesztelés | Node.js beépített tesztfuttató (`node --test`) |
| Asztali alkalmazás (váz) | C# / .NET Windows Forms |
| Fejlesztőeszközök | XAMPP, Visual Studio Code |
| Verziókezelés | Git, GitHub |

---

## 4. Adatbázis

### 4.1 Adatmodell
Az adatokat MySQL adatbázisban (`zsamo`, utf8mb4, magyar rendezés) tároljuk. Tárolt adatok:
- felhasználó neve, e-mail címe, Argon2id hash-elt jelszava, azonosítója, szerepköre, bemutatkozása; diákoknál iskolatípus és osztály;
- tantárgyak és azok kategóriái;
- az oktatók tantárgyai és tantárgyankénti óradíja;
- a diákok által megjelölt tantárgyak, amiből segítséget kérnek;
- az oktatók szabad időpontjai;
- foglalások és azok állapota;
- értékelések (csillag + vélemény);
- beszélgetések, chat üzenetek, csatolmányok adatai.

A törölt fiókokat nem töröljük fizikailag, hanem anonimizáljuk (`deleted_at`), hogy a korábbi foglalások és üzenetek ne sérüljenek.

### 4.2 ER-diagram
*(Ide kell képként beilleszteni – phpMyAdmin → Tervező nézet, vagy dbdiagram.io.)* Az ábrán a `users` tábla áll a középpontban; hozzá kapcsolódik 1:N kapcsolattal az `availabilities`, `bookings`, `conversations`, `messages`, `tutor_subjects` és `student_subjects`; a `subjects` az oktató/diák–tantárgy összekötő táblákon és a `bookings`-on keresztül kapcsolódik; a `reviews` a `bookings`-hoz tartozik 1:1 kapcsolattal.

### 4.3 Táblák részletes leírása
| Tábla | Leírás |
|---|---|
| `users` | A felhasználók adatai: id, teljes név, e-mail (egyedi), hash-elt jelszó, szerepkör (STUDENT / TUTOR / ADMIN), bemutatkozás, óradíj, iskolatípus, osztály, létrehozás és törlés ideje. |
| `subjects` | Az összes tantárgy (42 db): id, név (egyedi), kategória. |
| `tutor_subjects` | Az oktatókhoz rendeli az általuk tanított tantárgyat és annak óradíját (`hourly_rate`). Elsődleges kulcs: (tutor_id, subject_id). |
| `student_subjects` | A diákokhoz rendeli azokat a tantárgyakat, amelyekből segítséget kérnek. Elsődleges kulcs: (student_id, subject_id). |
| `availabilities` | Az oktatók szabad időpontjai: oktató, kezdés, befejezés, `is_booked` (le van-e foglalva). |
| `bookings` | A foglalások: diák, oktató, tantárgy, kezdés, befejezés, ár, megjegyzés (max. 300 karakter), létrehozás ideje és állapot (PENDING, CONFIRMED, REJECTED, COMPLETED, CANCELLED). |
| `reviews` | Az értékelések: melyik foglaláshoz tartozik (egy foglaláshoz egy értékelés), csillagszám (1–5) és szöveges vélemény. Ebből derül ki, hogy ki lett értékelve. |
| `conversations` | Eltárolja, hogy kik között jött létre beszélgetés (diák + oktató; ugyanaz a pár csak egyszer szerepelhet). |
| `messages` | Az üzenetek: melyik beszélgetésbe, ki küldte, szöveg, mikor, elolvasta-e a címzett; csatolmány neve, útvonala, típusa, mérete; szerkesztés és visszavonás ideje. |

### 4.4 Kapcsolatok
- `users` 1–N `availabilities` (oktató → szabad időpontok)
- `users` 1–N `bookings` (diákként és oktatóként is); `subjects` 1–N `bookings`
- `bookings` 1–1 `reviews`
- `users` N–M `subjects` a `tutor_subjects` és a `student_subjects` táblákon keresztül
- `users` (diák) és `users` (oktató) között `conversations`; `conversations` 1–N `messages`; `users` 1–N `messages` (feladó)
- A `conversations`, `messages` és `tutor_subjects` kapcsolatai törlésnél kaszkádolnak (`ON DELETE CASCADE`); a többi idegen kulcs sima hivatkozási kényszer.

### 4.5 Adatbázis létrehozása / dump
A teljes séma és a mintaadatok a `sql/zsamo.sql` fájlban vannak. Létrehozás: XAMPP-ban indítsd el a MySQL-t, phpMyAdminban hozz létre egy `zsamo` nevű adatbázist (utf8mb4_hungarian_ci), majd importáld a fájlt (Importálás → `zsamo.sql`). Parancssorból: `mysql -u root zsamo < sql/zsamo.sql`.
Megjegyzés: a mintafelhasználók jelszava nem valódi hash, velük nem lehet belépni; új fiókot regisztrálj.

---

## 5. Felhasználói felület

### 5.1 Képernyők
- **Főoldal:** figyelemfelkeltő nyitóoldal, regisztrációra/bejelentkezésre visz, és statisztikát mutat (pl. hány oktató van a platformon).
- **Tantárgyak:** tantárgyak böngészése kategóriák szerint.
- **Oktatók:** oktatók megjelenítése szűrhetően; mutatja az óradíjat, az értékeléseket, és innen indítható a foglalás vagy a chat.
- **Chat:** itt beszélget a diák az oktatóval (szöveg, kép, dokumentum).
- **Foglalás / Foglalás után:** időpont kiválasztása, majd a foglalások áttekintése.
- **GYIK:** a gyakori kérdésekre ad választ.
- **Bejelentkezés:** belépés meglévő fiókba.
- **Regisztráció:** fiók létrehozása; itt választható ki, hogy diákként vagy korrepetálóként használod az oldalt.

*(Ide érdemes egy-egy képernyőképet tenni.)*

### 5.2 Navigáció
A navigációs sávot minden oldalra a `creator.js` készíti el. A tartalma a bejelentkezési állapottól és a szerepkörtől függ: a vendégnek, a diáknak és a korrepetálónak külön menüje van. Bejelentkezve megjelenik a profil-menü (monogram, fiókbeállítások, kijelentkezés) és a világos/sötét téma kapcsoló.

### 5.3 Reszponzív működés
A reszponzivitást a CSS kezeli. Az oldal megfelelően jelenik meg telefonokon és közepes monitorokon. Nem tesztelt eszközök: régebbi (4:3-as) monitorok és okosórák.

### 5.4 Felhasználói szerepkörök
- **Nem bejelentkezett:** tantárgyakat és oktatókat böngészhet, olvashatja a GYIK-et és az oktatók értékeléseit, de foglalni és csevegni nem tud.
- **Diák:** böngészés, csevegés az oktatókkal, időpontfoglalás, közelgő foglalások megtekintése és lemondása, értékelés, profilszerkesztés.
- **Korrepetáló:** csevegés a diákokkal, a foglalási kérelem elfogadása vagy elutasítása, szabad időpontok kezelése, profilszerkesztés (tantárgyak és óradíj).
- **Admin:** minden oldalt lát.

---

## 6. Backend

### 6.1 Szerver felépítése
A szerver egyetlen Node.js folyamat (`vsc/js/main.js`), amely az indulásakor:
1. beállítja az Express-t (JSON és űrlap törzs feldolgozás);
2. beállítja az `express-session` munkamenetet (süti: `connect.sid`, `httpOnly`, `sameSite=lax`, 7 nap; titok: `SESSION_SECRET` környezeti változó, vagy fejlesztői alapérték);
3. kiszolgálja a statikus fájlokat (`/css`, `/html`, `/images`, valamint a `/js` mappából csak a fehérlistán lévő kliens fájlokat – a `main.js` és `auth.js` soha nem érhető el);
4. csatlakozik a MySQL-hez (`localhost`, felhasználó `root`, jelszó nélkül, adatbázis `zsamo`);
5. regisztrálja az oldal- és API-végpontokat;
6. létrehozza a WebSocket szervert ugyanazon a HTTP szerveren;
7. a 3000-es porton figyel.

A törölt fiókok azonosítói memóriában vannak, így az ő még élő munkameneteik a következő kérésnél kijelentkeztetésre kerülnek.

### 6.2 API végpontok
| Metódus és útvonal | Jogosultság | Leírás |
|---|---|---|
| `GET /`, `/index`, `/subs`, `/login`, `/register`, `/gyik`, `/chat`, `/oktatok`, `/foglalas`, `/foglalasutan` | nyilvános | Oldalak kiszolgálása |
| `GET /api/me` | nyilvános | Ki van bejelentkezve |
| `POST /register` | nyilvános | Regisztráció (STUDENT vagy TUTOR) |
| `POST /login` | nyilvános | Bejelentkezés |
| `POST /logout` | – | Kijelentkezés |
| `GET /api/subjects` | nyilvános | Tantárgyak listája |
| `GET /api/tutors` | nyilvános | Oktatók listája (szűréssel) |
| `GET /api/tutors/:id/reviews` | nyilvános | Egy oktató értékelései |
| `GET /api/tutors/:id/availability` | nyilvános | Egy oktató szabad időpontjai |
| `GET /api/stats` | nyilvános | Főoldali statisztikák |
| `GET`, `POST /api/conversations` | bejelentkezett | Beszélgetések listája / új beszélgetés indítása |
| `POST /api/conversations/:id/read` | résztvevő | Üzenetek olvasottnak jelölése |
| `GET`, `POST /api/conversations/:id/messages` | résztvevő | Üzenetek lekérése / küldése |
| `POST /api/conversations/:id/attachments` | résztvevő | Üzenet csatolmánnyal (max. 10 MB) |
| `GET /api/messages/:id/attachment` | résztvevő | Csatolmány letöltése/megjelenítése |
| `PATCH`, `DELETE /api/messages/:id` | az üzenet feladója | Üzenet szerkesztése / visszavonása |
| `GET`, `PUT`, `DELETE /api/account` | bejelentkezett | Saját fiók lekérése, módosítása, törlése (anonimizálás) |
| `GET`, `POST /api/availability`, `DELETE /api/availability/:id` | oktató | Szabad időpontok kezelése |
| `POST /api/bookings` | diák | Foglalási kérelem |
| `GET /api/bookings`, `GET /api/bookings/summary` | bejelentkezett | Saját foglalások / összegzés |
| `POST /api/bookings/:id/confirm`, `/reject` | oktató | Foglalás elfogadása / elutasítása |
| `POST /api/bookings/:id/cancel` | diák vagy oktató | Foglalás lemondása |
| `POST /api/bookings/:id/review` | diák | Értékelés leadása |
| `GET /api/students/:id/profile` | oktató (akivel a diák beszélget) | Diák profilja (e-mail nélkül) |
| WebSocket (`/`) | bejelentkezett (süti) | `new_message`, `booking_update` események |

### 6.3 Hitelesítés / jogosultságkezelés
- **Regisztráció:** minden mező kötelező, a két jelszónak egyeznie kell, a név legfeljebb 60, az e-mail legfeljebb 100 karakter, a szerepkör csak STUDENT vagy TUTOR lehet; az e-mail egyedi. A jelszó Argon2id-del hash-elve kerül az adatbázisba.
- **Bejelentkezés:** az e-mail alapján megkeresett felhasználó hash-ét Argon2-vel ellenőrizzük; siker esetén az azonosító, név, e-mail és szerepkör a szerveroldali munkamenetbe (`req.session.user`) kerül, a böngésző csak a munkamenet-azonosítót tartalmazó sütit kapja. A jelszó-hash soha nem hagyja el a szervert.
- **Jogosultság:** a védett végpontok munkamenet nélkül 401-et adnak (`requireLogin`); rossz szerepkör esetén 403-at (pl. foglalni csak diák, szabad időpontot kezelni csak oktató tud); idegen beszélgetéshez/foglaláshoz nem lehet hozzáférni.
- **WebSocket:** csatlakozáskor a munkamenet-süti alapján azonosítjuk a felhasználót; azonosítatlan vagy törölt fiók 401-et kap.
- **Fájlvédelem:** a csatolmányok véletlen nevű fájlként, csak engedélyezett kiterjesztéssel (jpg, png, gif, webp, pdf, doc(x), xls(x), ppt(x), txt) kerülnek mentésre, és csak a beszélgetés résztvevői érhetik el őket.

### 6.4 Hibakezelés
- Hiányzó vagy hibás mezőknél (pl. üres bejelentkezési mező) 400-as válasz érthető magyar üzenettel.
- Nem bejelentkezett felhasználó: 401; nem megfelelő szerepkör: 403; nem létező elem: 404; ütköző állapot (pl. már foglalt időpont, már elfogadott foglalás): 409.
- Ha az adatbázisból nem sikerül lekérni vagy eltárolni az adatot, a szerver naplózza a hibát, és 500-as státusszal általános hibaüzenetet ad, a részleteket nem fedi fel.
- Nem engedélyezett fájltípus vagy túl nagy fájl feltöltésekor a felhasználó hibaüzenetet kap.
- Jelszó-ellenőrzési hibánál a rendszer „sikertelen belépésként" viselkedik, nem omlik össze.
- A kliens oldalon a `fetch` hívások hibáit a lap elkapja és kiírja a felhasználónak.

---

## 7. Frontend

### 7.1 Felépítés
Framework nélküli, oldalanként szervezett frontend:
- `vsc/html` – az oldalak (index, subs, oktatok, chat, gyik, login, register, foglalas, foglalasutan);
- `vsc/css` – oldalankénti stíluslapok és a közös `style.css` (színek, sötét téma);
- `vsc/js` – oldalankénti szkriptek, a közös `creator.js` (navigáció) minden oldalba be van építve;
- `vsc/images` – képek.

### 7.2 Fontosabb funkciók
- **Regisztráció:** űrlap szerepkör-választással, kliens- és szerveroldali ellenőrzéssel.
- **Bejelentkezés:** űrlap, hibaüzenetek, sikeres belépés után átirányítás.
- **Tantárgyak és oktatók:** kategória, tantárgy és ár szerinti szűrés, egyedi legördülő menük, oktatói értékelések.
- **Chat:** valós idejű üzenetküldés WebSocketen, olvasottság jelzés, képek és dokumentumok küldése, képnézegető, üzenet szerkesztése és visszavonása.
- **Időpont foglalás:** szabad időpontok kiválasztása, megjegyzés, a foglalások állapotának követése, elfogadás/elutasítás/lemondás, értékelés.
- **Profil szerkesztés:** a fiókbeállítások ablakban (név, e-mail, bemutatkozás, tantárgyak, óradíj / iskolatípus, osztály), fiók törlése.
- **Téma:** világos/sötét mód, amely minden oldalon érvényes.

### 7.3 API használata
A frontend `fetch`-csel hívja a 6.2 pontban felsorolt végpontokat, a `credentials: "include"` beállítással, hogy a munkamenet-süti elmenjen. A válaszokat JSON-ként dolgozza fel; hiba esetén (`error` mező) kiírja a szerver üzenetét. A `/api/me` hívás minden oldalbetöltésnél lefut a navigáció felépítéséhez. A chat emellett WebSocket kapcsolatot tart fenn az új üzenetek és foglalási változások azonnali megjelenítéséhez.

---

## 8. Telepítés és üzemeltetés

### 8.1 Szükséges környezet
- Node.js (LTS) és npm
- XAMPP (MySQL/MariaDB) vagy bármely MySQL szerver
- Webböngésző (Chrome, Firefox, Edge)
- Windows esetén az `argon2` csomag natív modulja miatt a telepítőnek jól működő build-környezet / előfordított csomag kell (a legtöbb esetben automatikus).
- Opcionális: Visual Studio 2022+ / .NET 10 az asztali alkalmazás vázhoz.

### 8.2 Telepítés
1. A repository letöltése: `git clone https://github.com/Matyi24/-TutorConnect-.git`
2. Függőségek telepítése a mappában: dupla kattintás az `installer.bat`-ra, vagy `npm install`.
   A telepítő a következőket rakja fel: argon2, express, express-session, multer, mysql2, ws.

### 8.3 Konfiguráció
- Adatbázis-kapcsolat: a `vsc/js/main.js`-ben (`host: localhost`, `user: root`, jelszó nélkül, `database: zsamo`) – más környezetben itt kell módosítani.
- `SESSION_SECRET` környezeti változó: éles használatnál kötelezően állítsd be; ennek hiányában fejlesztői alapértéket használ.
- `NODE_ENV=production`: kikapcsolja a teszt-naplózást, amely fejlesztéskor a jelszavakat is kiírja a konzolra. (Külön is kikapcsolható: `DEBUG_LOGS=false`.)
- Port: 3000.
- Feltöltött chat-fájlok: a szerver tárolja (`uploads`), ez nincs verziókezelve.
- HTTPS használatánál a munkamenet-süti `secure` beállítását `true`-ra kell állítani.

### 8.4 Adatbázis létrehozása
1. XAMPP Control Panelen indítsd el a MySQL-t.
2. phpMyAdminban hozz létre egy `zsamo` adatbázist (utf8mb4_hungarian_ci).
3. Importáld a `sql/zsamo.sql` fájlt.
A szerver induláskor szükség esetén kiegészíti a sémát (pl. üzenet-csatolmány oszlopok, e-mail mező hossza).

### 8.5 Indítás
- Fejlesztői mód (automatikus újraindítással): `npm run dev`
- Közvetlenül: `node vsc/js/main.js`
Ezután az oldal a `http://localhost:3000` címen érhető el. Újraindításkor a munkamenetek elvesznek (memóriában vannak), a felhasználóknak újra be kell lépniük.

---

## 9. Tesztelés

### 9.1 Tesztelési stratégia
Kétféle tesztelést alkalmaztunk: **automatizált** szerveroldali teszteket (Node.js beépített tesztfuttatójával) és **kézi** tesztelést a böngészőben (funkciók, reszponzivitás). Az automatizált tesztek valódi MySQL nélkül futnak: a `mysql2` modult egy memóriabeli hamis adatbázis helyettesíti, a valódi szerver pedig véletlen porton indul, így a végpontok tényleges HTTP- és WebSocket-viselkedése tesztelhető.

### 9.2 Tesztesetek
Néhány jellemző eset (kézi és automatizált):
| Eset | Várt eredmény |
|---|---|
| Regisztráció üres mezővel | 400, hibaüzenet |
| Regisztráció eltérő jelszavakkal | 400, „A két jelszó nem egyezik." |
| Belépés rossz jelszóval | sikertelen belépés, nincs munkamenet |
| Védett végpont belépés nélkül | 401 |
| Diák szabad időpontot próbál létrehozni | 403 |
| Foglalás már foglalt időpontra | elutasítás |
| Nem résztvevő olvas egy beszélgetést | elutasítás |
| Túl hosszú üzenet (>2000 karakter) | elutasítás |
| Nem engedélyezett fájltípus feltöltése | elutasítás |
| Értékelés 1–5 csillagon kívül | 400 |

### 9.3 Automatizált tesztek
A tesztek a `vsc/test` mappában vannak (`auth.test.js`, `main.test.js`, `helpers/server.js`), összesen 141 teszt, 22 csoportban. Lefedik: a jelszó hash-elést/ellenőrzést, az oldalakat és statikus fájlokat, `/api/me`, regisztráció, bejelentkezés, kijelentkezés, tantárgyak, oktatók, értékelések, statisztika, beszélgetések, üzenetek (küldés, szerkesztés, visszavonás), csatolmányok, WebSocket, fiókkezelés.
Futtatás: `npm test`.
**Fontos:** a `package.json` a `test/*.test.js` útvonalat futtatja, de a tesztek jelenleg a `vsc/test` mappában vannak, és a belső útvonalaik (`../../vsc/js/main.js`) a repository gyökerében lévő `test` mappát feltételezik. Így a jelenlegi helyükről az `npm test` nem találja őket – vagy mozgasd vissza a `test` mappát a gyökérbe, vagy javítsd az útvonalakat.

### 9.4 Teszteredmények
A `vsc/test` mappát a repository gyökerébe másolva az `npm test` eredménye a dokumentáció írásakor: **141 teszt, ebből 116 sikeres, 13 sikertelen, 8 megszakadt**. A sikertelen tesztek a chat csatolmányokhoz, az üzenetküldéshez/-törléshez és a fiók lekéréséhez/módosításához tartoznak, két esetet a tesztek maguk is TODO-ként jelölnek (a csatolmányok közvetlen elérése a statikus útvonalon; a WebSocket hibakezelő hiánya túl nagy üzenetnél). `[ELLENŐRIZD]` – futtasd le újra, mielőtt leadod, és írd át a számokat az aktuális eredményre.

### 9.5 Hibák és javításuk
| Hiba | Javítás |
|---|---|
| A szerver forráskódja (jelszavak, session titok) elérhető lehetett a statikus `/js` útvonalon | Fehérlistás kiszolgálás, csak a kliens fájlok érhetők el |
| A chat csatolmányok közvetlenül elérhetők lehettek | Véletlen fájlnevek, jogosultság-ellenőrzéssel kiszolgált letöltő végpont |
| Az oktatók e-mail címe kiszivároghatott | Az e-mail cím nem kerül ki az API válaszokba |
| A multer elrontotta az ékezetes fájlneveket (ő, ű) | A fájlnév újrakódolása UTF-8-ra, veszélyes karakterek eltávolítása |
| Lejárt függő/elfogadott foglalások beragadtak | Minden foglalás-lekérés előtt a lejártak automatikusan COMPLETED / CANCELLED állapotba kerülnek |
| Lemondott/elutasított foglalás után az időpont foglalt maradt | Az időpont újra szabaddá válik |
| Törölt fiók munkamenete tovább élt | Törölt fiók azonosítók nyilvántartása, a kérés kijelentkeztet, a WebSocket kapcsolat bezárul |
| Oktatás/fizetés hibás kiírása, tantárgyak oldal CSS | Javítva (lásd `npm_list` teendők) `[ELLENŐRIZD]` |

---

## 10. Forráskód és projektstruktúra

### 10.1 Repository
GitHub: https://github.com/Matyi24/-TutorConnect- (főág: `main`).

### 10.2 Könyvtárstruktúra
```
-TutorConnect-/
├── installer.bat          # függőségek telepítése Windowson
├── package.json           # függőségek, npm scriptek (dev, test)
├── sql/
│   └── zsamo.sql          # adatbázis séma + mintaadatok
├── vsc/                   # a webalkalmazás
│   ├── html/              # oldalak
│   ├── css/               # stíluslapok
│   ├── js/                # main.js, auth.js (szerver) + kliens szkriptek
│   ├── images/            # képek
│   └── test/              # automatizált tesztek
├── Desktop/
│   └── TutorConnectApp/   # Windows Forms asztali alkalmazás váza (.NET)
└── dokumentácsó/          # a projekt dokumentációja (PDF)
```

### 10.3 Fontosabb modulok
- `vsc/js/main.js` – a teljes szerveroldal: végpontok, munkamenet, WebSocket, feltöltés.
- `vsc/js/auth.js` – Argon2id jelszókezelés.
- `vsc/js/creator.js` – navigáció, profil-menü, téma, fiókbeállítások.
- `vsc/js/chat.js` – a chat kliens.
- `vsc/js/oktatok.js` – oktatók listája, szűrés, értékelések.
- `vsc/js/booking.js`, `foglalas.js`, `foglalasutan.js` – foglalási folyamat.
- `vsc/test/helpers/server.js` – a tesztkörnyezet (hamis adatbázis + szerverindítás).
- `Desktop/TutorConnectApp` – `Form1.cs` (főablak), `NavBar.cs` (oldalsáv), `Theme.cs` (színek).

---

## 11. Csapatmunka

### 11.1 Csapattagok
- Porkoláb Mátyás Attila
- Stickel Zsombor
- Marián Máté

### 11.2 Feladatmegosztás
`[ELLENŐRIZD – a git előzményekből csak a GitHub-fiókok látszanak (Aggrroo, Matt, Pelion0), nem tudom, melyik név melyik fiók. Javítsd a neveket.]`
- **Szerveroldal és biztonság** (Aggrroo): API végpontok, profil bővítés, foglalás és értékelés, biztonsági javítások (forráskód- és feltöltésvédelem, e-mail elrejtése).
- **Felület, sötét mód, dokumentáció, tesztek** (Matt): sötét mód az oldalakon, a profil-menü, tesztek, telepítő (`installer.bat`), dokumentáció.
- **Alap stílus, asztali alkalmazás** (Pelion0): alap stílusok, a beállítások/profil menü alapja, a Windows Forms alkalmazás váza.

### 11.3 Használt projektmenedzsment eszközök
- GitHub (verziókezelés, közös repository, ágak egyesítése)
- Közös teendőlista a kódban (`vsc/js/npm_list`)

### 11.4 Saját feladat / hozzájárulás
`[ELLENŐRIZD – ezt a részt csak te tudod pontosan megírni. Vázlat, töltsd ki a sajátoddal:]`
„A projektben az alábbiakat készítettem: …(pl. a chat oldal és a WebSocket kapcsolat / az adatbázis terve / a foglalási folyamat / a dokumentáció)… Ehhez használt technológiák: …. Legnagyobb kihívás: …, amit úgy oldottam meg, hogy …"

---

## 12. Összegzés
A TutorConnect célja egy olyan platform létrehozása volt, ahol a diákok és a korrepetálók gyorsan egymásra találnak. A megvalósított rendszer lefedi a teljes folyamatot: regisztráció és biztonságos bejelentkezés, tantárgy- és oktatókeresés ár szerinti szűréssel, valós idejű chat csatolmányokkal, időpontfoglalás elfogadással/elutasítással, valamint értékelés. Az adatokat MySQL adatbázis tárolja, a jelszavak Argon2id-vel védettek.

Tanulságok: a csapatmunka és a verziókezelés fontossága, a biztonság (jogosultságok, fájlkezelés) tudatos tervezése, illetve az automatizált tesztek értéke.

Továbbfejlesztési lehetőségek: tartós munkamenet-tár (újraindítás után is bejelentkezve maradás), HTTPS és környezeti változókból olvasott adatbázis-beállítások, online fizetés, admin felület, az asztali alkalmazás befejezése, a hibás tesztek javítása és a teszt-útvonalak rendbetétele.
