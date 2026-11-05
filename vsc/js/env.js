// ============================================================
// KÖRNYEZETI BEÁLLÍTÁSOK (.env) ÉS TITKOK
// ============================================================
//
// Ezt a fájlt a main.js ELSŐKÉNT tölti be, hogy a titkok (adatbázis jelszó,
// session titok) ne a forráskódban legyenek.
//
// A beállításokat egy ".env" nevű fájlba kell tenni a PROJEKT FŐMAPPÁJÁBAN
// (ugyanoda, ahol a package.json van), soronként  NÉV=érték  formában.
// A fájl titkokat tartalmaz, ezért a .gitignore-ban van: SOHA ne kerüljön
// a GitHubra. Mintának lásd a .env.example fájlt.
//
// Ha egy változó már be van állítva a terminálban, az erősebb, mint a fájl.
//
// FEJLESZTÉS (NODE_ENV nincs beállítva): minden marad a régi, nem kell semmit
// beállítani. Az adatbázis alapértelmezetten root / üres jelszó, a session
// titok pedig minden indításkor újra generálódik.
//
// ÉLES (NODE_ENV=production): a szerver NEM indul el, ha hiányzik a
// SESSION_SECRET (legalább 32 karakter), a DB_USER vagy a DB_PASSWORD.
// Ez szándékos: így egy elfelejtett beállítás nem marad észrevétlen.

const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

(function loadEnvFile() {

    let text;

    try {
        text = fs.readFileSync(path.join(__dirname, "../../.env"), "utf8");
    } catch (error) {
        return;     // nincs .env fájl: semmi baj
    }

    text.split(/\r?\n/).forEach((line) => {

        if (line.trim().startsWith("#")) {
            return;
        }

        const match = line.match(/^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$/);

        if (!match) {
            return;
        }

        let value = match[2];

        const quoted =
            (value.startsWith('"') && value.endsWith('"')) ||
            (value.startsWith("'") && value.endsWith("'"));

        if (quoted && value.length >= 2) {
            value = value.slice(1, -1);
        }

        if (process.env[match[1]] === undefined) {
            process.env[match[1]] = value;
        }
    });

})();


const IS_PRODUCTION = process.env.NODE_ENV === "production";

const MIN_SECRET_LENGTH = 32;

const missing = [];


// ---------------- adatbázis ----------------

if (IS_PRODUCTION) {

    if (!process.env.DB_USER) missing.push("DB_USER");
    if (!process.env.DB_PASSWORD) missing.push("DB_PASSWORD");
}

const dbConfig = {
    host: process.env.DB_HOST || "localhost",
    user: process.env.DB_USER || "root",
    password: process.env.DB_PASSWORD !== undefined ? process.env.DB_PASSWORD : "",
    database: process.env.DB_NAME || "zsamo"
};


// ---------------- session titok ----------------

let sessionSecret = process.env.SESSION_SECRET;

if (IS_PRODUCTION) {

    if (!sessionSecret || sessionSecret.length < MIN_SECRET_LENGTH) {
        missing.push(`SESSION_SECRET (legalább ${MIN_SECRET_LENGTH} karakter)`);
    }

} else if (!sessionSecret) {

    // Fejlesztésben a session úgyis memóriában van (újraindításkor elvész),
    // ezért a véletlen titok semmit sem rontana el. Nincs többé a kódban
    // egy mindenki által ismert alapértelmezett érték.
    sessionSecret = crypto.randomBytes(32).toString("hex");
}

if (missing.length > 0) {

    console.error("❌ Hiányzó kötelező beállítás(ok) éles módban: " + missing.join(", "));
    console.error("   Állítsd be őket a .env fájlban vagy környezeti változóként (lásd .env.example).");

    throw new Error("Hiányzó környezeti beállítás: " + missing.join(", "));
}


// A süti csak HTTPS-en menjen? Élesben alapból igen; fejlesztésben (http://localhost)
// nem. A COOKIE_SECURE=true/false felülbírálja.
const cookieSecure =
    process.env.COOKIE_SECURE !== undefined
        ? process.env.COOKIE_SECURE === "true"
        : IS_PRODUCTION;


module.exports = {
    IS_PRODUCTION,
    dbConfig,
    sessionSecret,
    cookieSecure
};
