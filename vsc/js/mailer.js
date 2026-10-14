// ============================================================
// E-MAIL KÜLDÉS
// ============================================================
//
// Beállítás környezeti változókkal (mind opcionális fejlesztéskor):
//
//   SMTP_HOST   a levelezőszerver (pl. smtp.gmail.com)
//   SMTP_PORT   port (alapértelmezett: 587; 465 esetén titkosított)
//   SMTP_USER   felhasználónév
//   SMTP_PASS   jelszó (Gmailnél "alkalmazásjelszó")
//   MAIL_FROM   a feladó, pl.  TutorConnect <noreply@pelda.hu>
//   APP_URL     az oldal címe a levelekben (alapértelmezett: http://localhost:3000)
//
// Ha az SMTP_HOST nincs megadva, a levél NEM megy ki: a szerver a
// konzolra írja a teljes tartalmát (és így a megerősítő / jelszó-
// visszaállító linket is). Fejlesztéskor ez elég, nem kell levelező
// fiók. Élesben (NODE_ENV=production) SMTP nélkül hibát naplóz.

const APP_URL = (process.env.APP_URL || "http://localhost:3000").replace(/\/+$/, "");

const MAIL_FROM = process.env.MAIL_FROM || "TutorConnect <noreply@tutorconnect.local>";

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


// Visszatérés: true, ha a levél kiment (vagy fejlesztői módban kiíródott)
async function sendMail({ to, subject, title, intro, buttonText, url, outro }) {

    const { text, html } = layout({ title, intro, buttonText, url, outro });

    const smtp = getTransporter();

    if (!smtp) {

        if (process.env.NODE_ENV === "production") {
            console.error("❌ E-mail nem küldhető: nincs beállítva az SMTP_HOST.");
            return false;
        }

        console.log("\n📧 ==========  E-MAIL (fejlesztői mód, nem ment ki)  ==========");
        console.log("Címzett:", to);
        console.log("Tárgy:  ", subject);
        console.log(`${buttonText}: ${url}`);
        console.log("=============================================================\n");

        return true;
    }

    try {

        await smtp.sendMail({ from: MAIL_FROM, to, subject, text, html });

        return true;

    } catch (error) {

        console.error("❌ E-mail küldési hiba:", error.message);

        return false;
    }
}


module.exports = { sendMail, APP_URL };
