// ============================================================
// E-MAIL KÜLDÉS
// ============================================================
//
// Három küldési mód van, ebben a sorrendben (az első beállított nyer):
//
//  1) MAILTRAP API  (ajánlott)
//       MAILTRAP_TOKEN   a Mailtrap "API token" (Sending > API)
//       MAIL_FROM        a feladó, pl.  TutorConnect <hello@demomailtrap.co>
//                        (alapértelmezett: a Mailtrap demó címe)
//
//  2) SMTP  (bármilyen levelezőszerver, pl. Gmail, Brevo)
//       SMTP_HOST, SMTP_PORT (587), SMTP_USER, SMTP_PASS, MAIL_FROM
//
//  3) Semmi sincs beállítva: a levél NEM megy ki, a szerver a konzolra
//     írja a tartalmát és a linket. Fejlesztéskor ez elég.
//
// Mindkét esetben:  APP_URL  az oldal címe a levelekben
// (alapértelmezett: http://localhost:3000)
//
// FONTOS (Mailtrap demó domain): a "demomailtrap.co" feladóról csak a
// Mailtrap-fiók saját e-mail címére lehet küldeni. Más címzetthez saját,
// megerősített domain kell a Mailtrapben.
//
// A titkokat (token, jelszó) soha ne írd a kódba vagy a gitbe: a terminálban
// állítsd be környezeti változóként.
//
// Élesben (NODE_ENV=production) beállítás nélkül hibát naplóz.

// ------------------------------------------------------------
// .env FÁJL BETÖLTÉSE
// ------------------------------------------------------------
//
// A beállításokat (MAILTRAP_TOKEN, SMTP_HOST, ...) nem kell minden
// terminálban kézzel beírni: elég egy ".env" nevű fájlt tenni a PROJEKT
// FŐMAPPÁJÁBA (ugyanoda, ahol a package.json van), soronként
//
//     MAILTRAP_TOKEN=ide_a_token
//     MAIL_FROM=TutorConnect <hello@demomailtrap.co>
//
// A fájl titkokat tartalmaz, ezért a .gitignore-ban kell lennie, hogy ne
// kerüljön fel a GitHubra! Ha egy változó már be van állítva a
// terminálban, az erősebb, mint a fájl.

const fs = require("fs");
const path = require("path");

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


const APP_URL = (process.env.APP_URL || "http://localhost:3000").replace(/\/+$/, "");

const MAIL_FROM = process.env.MAIL_FROM || "TutorConnect <noreply@tutorconnect.local>";

// "Név <cím@pelda.hu>" vagy csak "cím@pelda.hu"  ->  { name, email }
function parseSender(value) {

    const match = String(value).match(/^\s*(.*?)\s*<([^>]+)>\s*$/);

    if (match) {
        return { name: match[1].replace(/^"|"$/g, ""), email: match[2].trim() };
    }

    return { name: "TutorConnect", email: String(value).trim() };
}

const DEFAULT_MAILTRAP_FROM = "TutorConnect <hello@demomailtrap.co>";

let mailtrapClient = null;
let mailtrapTried = false;

function getMailtrap() {

    if (mailtrapTried) {
        return mailtrapClient;
    }

    mailtrapTried = true;

    if (!process.env.MAILTRAP_TOKEN) {
        return null;
    }

    try {

        const { MailtrapClient } = require("mailtrap");

        mailtrapClient = new MailtrapClient({ token: process.env.MAILTRAP_TOKEN });

    } catch (error) {

        console.error("❌ A mailtrap csomag nem tölthető be (npm install):", error.message);
    }

    return mailtrapClient;
}

let transporter = null;
let transporterTried = false;

function getTransporter() {

    if (transporterTried) {
        return transporter;
    }

    transporterTried = true;

    if (!process.env.SMTP_HOST) {
        return null;
    }

    try {

        const nodemailer = require("nodemailer");

        const port = Number(process.env.SMTP_PORT) || 587;

        transporter = nodemailer.createTransport({
            host: process.env.SMTP_HOST,
            port,
            secure: port === 465,
            auth: process.env.SMTP_USER
                ? { user: process.env.SMTP_USER, pass: process.env.SMTP_PASS || "" }
                : undefined
        });

    } catch (error) {

        console.error("❌ A nodemailer nem tölthető be (npm install nodemailer):", error.message);
    }

    return transporter;
}


function escapeHtml(value) {

    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}


// Egységes, egyszerű levélsablon (gombbal és sima linkkel is)
function layout({ title, intro, buttonText, url, outro }) {

    const text =
        `${title}\n\n${intro}\n\n${buttonText}: ${url}\n\n${outro}\n\n— TutorConnect`;

    const html = `
<div style="background:#f4f6ff;padding:32px 12px;font-family:Segoe UI,Arial,sans-serif;">
  <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:16px;padding:32px;border:1px solid #e3e6f5;">
    <div style="font-size:13px;font-weight:700;letter-spacing:2px;color:#4c6ef5;">TUTORCONNECT</div>
    <h1 style="margin:10px 0 14px;font-size:22px;color:#17172e;">${escapeHtml(title)}</h1>
    <p style="margin:0 0 22px;font-size:15px;line-height:1.6;color:#3b3f5c;">${escapeHtml(intro)}</p>
    <a href="${escapeHtml(url)}" style="display:inline-block;padding:13px 24px;background:#4c6ef5;color:#ffffff;text-decoration:none;font-weight:700;border-radius:10px;">${escapeHtml(buttonText)}</a>
    <p style="margin:24px 0 6px;font-size:12px;color:#7b7d9d;">Ha a gomb nem működik, másold be ezt a címet a böngészőbe:</p>
    <p style="margin:0 0 20px;font-size:12px;word-break:break-all;"><a href="${escapeHtml(url)}" style="color:#4c6ef5;">${escapeHtml(url)}</a></p>
    <p style="margin:0;font-size:13px;line-height:1.5;color:#7b7d9d;">${escapeHtml(outro)}</p>
  </div>
</div>`;

    return { text, html };
}


// A levél tartalmát (és a benne lévő linket) a konzolra is kiírjuk. Így
// fejlesztéskor akkor is megvan a link, ha a tényleges küldés nem sikerül
// vagy nincs is beállítva. Élesben (NODE_ENV=production) ez kikapcsol,
// mert a link egy belépésre jogosító titok.
function logMail({ to, subject, buttonText, url }, status) {

    if (process.env.NODE_ENV === "production") {
        return;
    }

    console.log("\n📧 ==========  E-MAIL  ==========");
    console.log("Címzett:", to);
    console.log("Tárgy:  ", subject);
    console.log(`${buttonText}: ${url}`);
    console.log("Állapot:", status);
    console.log("================================\n");
}


// Visszatérés: true, ha a levél kiment, vagy (küldés nélküli fejlesztői
// módban) legalább a konzolra kiíródott a link.
async function sendMail(mail) {

    const { to, subject, title, intro, buttonText, url, outro } = mail;

    const { text, html } = layout({ title, intro, buttonText, url, outro });

    // 1) Mailtrap API
    const mailtrap = getMailtrap();

    if (mailtrap) {

        try {

            await mailtrap.send({
                from: parseSender(process.env.MAIL_FROM || DEFAULT_MAILTRAP_FROM),
                to: [{ email: to }],
                subject,
                text,
                html,
                category: "TutorConnect"
            });

            logMail(mail, "elküldve (Mailtrap API)");

            return true;

        } catch (error) {

            console.error("❌ Mailtrap küldési hiba:", error.message);

            logMail(mail, "NEM ment ki: " + error.message);

            return false;
        }
    }

    // 2) SMTP
    const smtp = getTransporter();

    // 3) nincs semmi beállítva
    if (!smtp) {

        if (process.env.NODE_ENV === "production") {
            console.error("❌ E-mail nem küldhető: nincs beállítva sem MAILTRAP_TOKEN, sem SMTP_HOST.");
            return false;
        }

        logMail(mail, "nincs küldés beállítva, csak a konzolra íródott");

        return true;
    }

    try {

        await smtp.sendMail({ from: MAIL_FROM, to, subject, text, html });

        logMail(mail, "elküldve (SMTP)");

        return true;

    } catch (error) {

        console.error("❌ E-mail küldési hiba:", error.message);

        logMail(mail, "NEM ment ki: " + error.message);

        return false;
    }
}


// Induláskor kiírjuk, melyik mód aktív (hibakereséshez: ebből látszik, hogy
// a szerver megkapta-e a környezeti változókat)
if (process.env.MAILTRAP_TOKEN) {
    console.log("📧 E-mail küldés: Mailtrap API");
} else if (process.env.SMTP_HOST) {
    console.log(`📧 E-mail küldés: SMTP (${process.env.SMTP_HOST}:${process.env.SMTP_PORT || 587}, felhasználó: ${process.env.SMTP_USER || "nincs"})`);
} else {
    console.log("📧 E-mail küldés: NINCS beállítva, a levelek csak a konzolra íródnak");
}


module.exports = { sendMail, APP_URL };
