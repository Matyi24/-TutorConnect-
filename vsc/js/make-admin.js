// ============================================================
// ADMIN JOGOSULTSÁG ADÁSA / ELVÉTELE
// ============================================================
//
// Admin felhasználót a weboldalon keresztül nem lehet készíteni (direkt
// így van: különben bárki admin lehetne). Egy már regisztrált fiókot
// kell ezzel a szkripttel előléptetni:
//
//     node vsc/js/make-admin.js valaki@pelda.hu           (admin lesz)
//     node vsc/js/make-admin.js valaki@pelda.hu STUDENT   (vissza diák)
//     node vsc/js/make-admin.js valaki@pelda.hu TUTOR     (vissza oktató)
//
// Utána a felhasználónak ki kell lépnie és újra be kell lépnie.

const mysql = require("mysql2/promise");

const email = process.argv[2];
const role = (process.argv[3] || "ADMIN").toUpperCase();

if (!email || !["ADMIN", "STUDENT", "TUTOR"].includes(role)) {
    console.log("Használat: node vsc/js/make-admin.js <e-mail> [ADMIN|STUDENT|TUTOR]");
    process.exit(1);
}

(async () => {

    const db = await mysql.createConnection({
        host: "localhost",
        user: "root",
        password: "",
        database: "zsamo"
    });

    const [rows] = await db.query(
        "SELECT id, full_name, role FROM users WHERE email = ? AND deleted_at IS NULL LIMIT 1",
        [email]
    );

    if (rows.length === 0) {
        console.log("❌ Nincs ilyen felhasználó:", email);
        await db.end();
        process.exit(1);
    }

    const user = rows[0];

    // Ha az utolsó admint fosztanánk meg a jogától, szólunk
    if (user.role === "ADMIN" && role !== "ADMIN") {

        const [[count]] = await db.query("SELECT COUNT(*) AS n FROM users WHERE role = 'ADMIN' AND deleted_at IS NULL");

        if (count.n <= 1) {
            console.log("⚠️  Ő az egyetlen admin. A jogosultság elvétele után nem lesz admin felület.");
        }
    }

    await db.query(
        "UPDATE users SET role = ?, email_verified_at = COALESCE(email_verified_at, NOW()) WHERE id = ?",
        [role, user.id]
    );

    console.log(`✅ ${user.full_name} (${email}) szerepköre: ${user.role} → ${role}`);
    console.log("   A változás a következő bejelentkezéstől érvényes.");

    await db.end();

})().catch((error) => {
    console.error("❌ Hiba:", error.message);
    process.exit(1);
});
