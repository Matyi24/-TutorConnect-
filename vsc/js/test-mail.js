// ============================================================
// LEVÉLKÜLDÉS TESZT
// ============================================================
//
// Egyetlen tesztlevelet küld a beállított módon (Mailtrap API vagy SMTP),
// és pontosan kiírja, mi a hiba, ha nem megy.
//
// Használat (a környezeti változókat előtte be kell állítani, ugyanabban
// a terminálban, ahol a parancsot futtatod):
//
//     node vsc/js/test-mail.js
//     node vsc/js/test-mail.js valaki@pelda.hu     (más címzett)

const to = process.argv[2] || "teszt@pelda.hu";

console.log("\n--- Beállítások ---");
console.log("MAILTRAP_TOKEN:", process.env.MAILTRAP_TOKEN ? "megvan" : "nincs");
console.log("SMTP_HOST:     ", process.env.SMTP_HOST || "nincs");
console.log("SMTP_PORT:     ", process.env.SMTP_PORT || "nincs (587 lenne)");
console.log("SMTP_USER:     ", process.env.SMTP_USER || "nincs");
console.log("SMTP_PASS:     ", process.env.SMTP_PASS ? `megvan (${process.env.SMTP_PASS.length} karakter)` : "nincs");
console.log("MAIL_FROM:     ", process.env.MAIL_FROM || "nincs");
console.log("Címzett:       ", to);

async function main() {

    // 1) Eléri-e egyáltalán a szervert + jó-e a felhasználónév/jelszó?
    if (!process.env.MAILTRAP_TOKEN && process.env.SMTP_HOST) {

        const nodemailer = require("nodemailer");

        const port = Number(process.env.SMTP_PORT) || 587;

        const transport = nodemailer.createTransport({
            host: process.env.SMTP_HOST,
            port,
            secure: port === 465,
            auth: process.env.SMTP_USER
                ? { user: process.env.SMTP_USER, pass: process.env.SMTP_PASS || "" }
                : undefined,
            connectionTimeout: 10000,
            greetingTimeout: 10000
        });

        console.log("\n--- 1. Kapcsolat és bejelentkezés ---");

        try {

            await transport.verify();

            console.log("✅ A szerver elérhető, a felhasználónév és jelszó elfogadva.");

        } catch (error) {

            console.log("❌ HIBA:", error.message);
            console.log("   kód:", error.code, "| válasz:", error.response || "-");

            if (error.code === "ETIMEDOUT" || error.code === "ECONNREFUSED" || error.code === "ESOCKET") {
                console.log("   → A kapcsolat nem jön létre. Valószínűleg a port blokkolva van a hálózaton.");
                console.log("     Próbáld a SMTP_PORT értékét 2525 helyett 587-re, 25-re vagy 465-re állítani.");
            }

            if (error.code === "EAUTH") {
                console.log("   → Rossz felhasználónév vagy jelszó. Másold ki újra a Mailtrapből");
                console.log("     (ha a Reset Credentials-t megnyomtad, a régi jelszó már nem jó).");
            }

            return;
        }
    }

    // 2) Tesztlevél a valódi küldővel
    console.log("\n--- 2. Tesztlevél küldése ---");

    const { sendMail } = require("./mailer");

    const ok = await sendMail({
        to,
        subject: "TutorConnect tesztlevél",
        title: "Ez egy tesztlevél",
        intro: "Ha ezt látod a Mailtrap postaládában, a levélküldés működik.",
        buttonText: "Megnyitás",
        url: "http://localhost:3000/",
        outro: "Ez csak egy teszt."
    });

    console.log(ok
        ? "\n✅ Kész. Nézd meg a Mailtrap My Sandbox nézetét (frissítsd az oldalt)."
        : "\n❌ A küldés nem sikerült, a fenti hibaüzenet az ok.");
}

main().catch((error) => console.error("❌ Váratlan hiba:", error));
