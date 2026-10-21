// ============================================================
// E-MAIL MEGERŐSÍTÉS ÉS ELFELEJTETT JELSZÓ
// ============================================================
//
// Hogyan működik:
//
//  1. A szerver egy véletlen tokent (32 bájt) készít, és a felhasználónak
//     e-mailben küldi el egy linkben. Az adatbázisba CSAK a token
//     SHA-256 lenyomata kerül (auth_tokens tábla): ha valaki
//     megkapná az adatbázist, a tokenekkel akkor sem tudna mit kezdeni.
//
//  2. A token egyszer használható és lejár (megerősítés: 24 óra,
//     jelszó-visszaállítás: 1 óra). Új token kérésekor a régi érvényét
//     veszti.
//
//  3. A linkre kattintva egy oldal nyílik, ami POST kérésben küldi vissza
//     a tokent. (Direkt nem GET: a levelezők és víruskeresők előre
//     "megnyitják" a linkeket, és így elhasználnák a tokent.)
//
//  4. Az "elfelejtett jelszó" kérésre MINDIG ugyanazt válaszoljuk, akár
//     létezik a cím, akár nem, így a végpont nem használható arra, hogy
//     kiderítsék, kik regisztráltak.
//
//  5. Jelszó-visszaállítás után a felhasználó minden meglévő munkamenete
//     megszűnik (ha valaki idegen be volt lépve, kiesik).

const crypto = require("crypto");
const path = require("path");

const { hashPassword, debug } = require("./auth");
const { sendMail, APP_URL } = require("./mailer");

const VERIFY_TTL_MINUTES = 60 * 24;
const RESET_TTL_MINUTES = 60;

const STRONG_PASSWORD = /^(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{6,}$/;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;


// ---------------- egyszerű próbálkozás-korlátozó ----------------
//
// Memóriában tartja, ki hányszor próbálkozott az utolsó időszakban.
// Újraindításkor nullázódik (egy kisebb oldalnak ez elég).

const attempts = new Map();

function allow(key, max, windowMs) {

    const now = Date.now();

    const recent = (attempts.get(key) || []).filter((time) => now - time < windowMs);

    if (recent.length >= max) {

        attempts.set(key, recent);

        return false;
    }

    recent.push(now);
    attempts.set(key, recent);

    return true;
}

setInterval(() => {

    const now = Date.now();

    for (const [key, times] of attempts) {

        if (times.every((time) => now - time > 60 * 60 * 1000)) {
            attempts.delete(key);
        }
    }

}, 10 * 60 * 1000).unref();


function hashToken(token) {
    return crypto.createHash("sha256").update(token).digest("hex");
}


function setup({ app, db, sessionStore }) {

    const pool = db.promise();


    // ---------------- adatbázis ----------------

    async function ensureSchema() {

        try {

            const [column] = await pool.query(
                "SHOW COLUMNS FROM users LIKE 'email_verified_at'"
            );

            if (column.length === 0) {

                await pool.query(
                    "ALTER TABLE users ADD COLUMN email_verified_at TIMESTAMP NULL DEFAULT NULL"
                );

                // A már meglévő felhasználókat nem zavarjuk: őket megerősítettnek
                // vesszük (a created_at = created_at azért kell, mert az oszlop
                // "ON UPDATE current_timestamp", és nem akarjuk átírni a dátumot).
                await pool.query(
                    "UPDATE users SET email_verified_at = NOW(), created_at = created_at"
                );

                console.log("✅ users.email_verified_at oszlop létrehozva (meglévő fiókok megerősítettnek jelölve)");
            }

            await pool.query(`
                CREATE TABLE IF NOT EXISTS auth_tokens (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    user_id INT NOT NULL,
                    type VARCHAR(10) NOT NULL,
                    token_hash CHAR(64) NOT NULL,
                    email VARCHAR(100) NOT NULL,
                    expires_at DATETIME NOT NULL,
                    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE KEY uq_token_hash (token_hash),
                    KEY idx_user_type (user_id, type),
                    CONSTRAINT fk_auth_tokens_user FOREIGN KEY (user_id)
                        REFERENCES users (id) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            `);

        } catch (err) {

            console.error("❌ SQL hiba (e-mail megerősítés séma):", err);
        }
    }


    // ---------------- tokenek ----------------

    async function createToken(userId, type, email, ttlMinutes) {

        const token = crypto.randomBytes(32).toString("hex");

        // egy felhasználónak egyszerre egy érvényes tokenje legyen típusonként
        await pool.query(
            "DELETE FROM auth_tokens WHERE user_id = ? AND type = ?",
            [userId, type]
        );

        await pool.query(
            `INSERT INTO auth_tokens (user_id, type, token_hash, email, expires_at)
             VALUES (?, ?, ?, ?, DATE_ADD(NOW(), INTERVAL ? MINUTE))`,
            [userId, type, hashToken(token), email, ttlMinutes]
        );

        return token;
    }


    // Érvényes (nem lejárt) token keresése; nem használja el.
    async function findToken(token, type) {

        if (typeof token !== "string" || !/^[0-9a-f]{64}$/.test(token)) {
            return null;
        }

        const [rows] = await pool.query(
            `SELECT t.id, t.user_id, t.email, u.full_name, u.email AS current_email
             FROM auth_tokens t
             JOIN users u ON u.id = t.user_id
             WHERE t.token_hash = ? AND t.type = ? AND t.expires_at > NOW()
               AND u.deleted_at IS NULL
             LIMIT 1`,
            [hashToken(token), type]
        );

        return rows[0] || null;
    }


    // ---------------- e-mail megerősítés ----------------

    async function isVerified(userId) {

        const [rows] = await pool.query(
            "SELECT email_verified_at FROM users WHERE id = ? LIMIT 1",
            [userId]
        );

        return rows.length > 0 && rows[0].email_verified_at !== null;
    }


    async function issueVerification(userId, email, name) {

        const token = await createToken(userId, "VERIFY", email, VERIFY_TTL_MINUTES);

        return sendMail({
            to: email,
            subject: "Erősítsd meg az e-mail címed – TutorConnect",
            title: "Erősítsd meg az e-mail címed",
            intro: `Szia ${name}! Már csak egy lépés: kattints az alábbi gombra, hogy megerősítsd az e-mail címed. A link 24 óráig érvényes.`,
            buttonText: "E-mail cím megerősítése",
            url: `${APP_URL}/verify-email?token=${token}`,
            outro: "Ha nem te regisztráltál a TutorConnecten, nyugodtan hagyd figyelmen kívül ezt a levelet."
        });
    }


    // Új cím esetén (vagy ha a felhasználó újra kéri)
    async function markUnverified(userId) {

        await pool.query(
            "UPDATE users SET email_verified_at = NULL, created_at = created_at WHERE id = ?",
            [userId]
        );
    }


    // A hívás helyén egy sorban használható: elutasítja a megerősítetlen
    // felhasználót (true = mehet tovább, false = a válasz már elment).
    async function requireVerified(req, res) {

        if (!req.session.user) {

            res.status(401).json({ error: "Nem vagy bejelentkezve." });

            return false;
        }

        if (await isVerified(req.session.user.id)) {
            return true;
        }

        res.status(403).json({
            error: "Előbb erősítsd meg az e-mail címed. A levelet a fiókod menüjében újra elküldheted.",
            code: "EMAIL_NOT_VERIFIED"
        });

        return false;
    }


    app.post("/api/verify-email", async (req, res) => {

        if (!allow("verify:" + req.ip, 30, 60 * 60 * 1000)) {
            return res.status(429).json({ error: "Túl sok próbálkozás, kérlek várj egy kicsit." });
        }

        try {

            const row = await findToken(req.body.token, "VERIFY");

            // Ha közben megváltozott az e-mail cím, a régi cím linkje nem érvényes
            if (!row || row.email.toLowerCase() !== row.current_email.toLowerCase()) {
                return res.status(400).json({ error: "A link érvénytelen vagy lejárt." });
            }

            await pool.query(
                "UPDATE users SET email_verified_at = NOW(), created_at = created_at WHERE id = ?",
                [row.user_id]
            );

            await pool.query("DELETE FROM auth_tokens WHERE id = ?", [row.id]);

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/verify-email:", err);

            return res.status(500).json({ error: "Nem sikerült a megerősítés." });
        }
    });


    app.post("/api/resend-verification", async (req, res) => {

        if (!req.session.user) {
            return res.status(401).json({ error: "Nem vagy bejelentkezve." });
        }

        const user = req.session.user;

        if (!allow("resend:" + user.id, 3, 60 * 60 * 1000)) {
            return res.status(429).json({ error: "Egy órán belül legfeljebb 3 levelet kérhetsz. Nézd meg a spam mappát is." });
        }

        try {

            if (await isVerified(user.id)) {
                return res.json({ success: true, alreadyVerified: true });
            }

            const sent = await issueVerification(user.id, user.email, user.name);

            if (!sent) {
                return res.status(500).json({ error: "A levelet most nem sikerült elküldeni." });
            }

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/resend-verification:", err);

            return res.status(500).json({ error: "Nem sikerült elküldeni a levelet." });
        }
    });


    // ---------------- elfelejtett jelszó ----------------

    app.post("/api/forgot-password", (req, res) => {

        const email = String(req.body.email || "").trim();

        // A válasz mindig ugyanaz (lásd a fájl tetején), a korlát is csendes.
        const reply = () => res.json({
            success: true,
            message: "Ha ez az e-mail cím regisztrálva van, elküldtük a jelszó-visszaállító linket. Nézd meg a spam mappát is."
        });

        if (!EMAIL_PATTERN.test(email) || email.length > 100) {
            return reply();
        }

        if (
            !allow("forgot-ip:" + req.ip, 10, 60 * 60 * 1000) ||
            !allow("forgot-mail:" + email.toLowerCase(), 3, 60 * 60 * 1000)
        ) {
            return reply();
        }

        // Először válaszolunk, a keresés és a levélküldés utána fut: így a
        // válaszidő sem árulja el, hogy létezik-e a cím.
        reply();

        (async () => {

            try {

                const [rows] = await pool.query(
                    "SELECT id, full_name, email FROM users WHERE email = ? AND deleted_at IS NULL LIMIT 1",
                    [email]
                );

                if (rows.length === 0) {
                    return;
                }

                const user = rows[0];

                const token = await createToken(user.id, "RESET", user.email, RESET_TTL_MINUTES);

                await sendMail({
                    to: user.email,
                    subject: "Jelszó-visszaállítás – TutorConnect",
                    title: "Új jelszót szeretnél?",
                    intro: `Szia ${user.full_name}! Jelszó-visszaállítást kértél a fiókodhoz. Az alábbi gombra kattintva adhatsz meg új jelszót. A link 1 óráig érvényes.`,
                    buttonText: "Új jelszó megadása",
                    url: `${APP_URL}/reset-password?token=${token}`,
                    outro: "Ha nem te kérted, hagyd figyelmen kívül ezt a levelet: a jelszavad nem változik."
                });

            } catch (err) {

                console.error("❌ POST /api/forgot-password:", err);
            }
        })();
    });


    // A visszaállító oldal ezzel ellenőrzi, él-e még a link
    app.get("/api/reset-password/check", async (req, res) => {

        try {

            const row = await findToken(String(req.query.token || ""), "RESET");

            return res.json({ valid: Boolean(row) });

        } catch (err) {

            console.error("❌ GET /api/reset-password/check:", err);

            return res.status(500).json({ valid: false });
        }
    });


    app.post("/api/reset-password", async (req, res) => {

        if (!allow("reset:" + req.ip, 15, 60 * 60 * 1000)) {
            return res.status(429).json({ error: "Túl sok próbálkozás, kérlek várj egy kicsit." });
        }

        const password = String(req.body.password || "");
        const confirm = String(req.body.password_confirm || "");

        if (password !== confirm) {
            return res.status(400).json({ error: "A két jelszó nem egyezik." });
        }

        if (!STRONG_PASSWORD.test(password)) {
            return res.status(400).json({
                error: "A jelszó legalább 6 karakter legyen, és tartalmazzon számot és speciális karaktert."
            });
        }

        try {

            const row = await findToken(String(req.body.token || ""), "RESET");

            if (!row) {
                return res.status(400).json({ error: "A link érvénytelen vagy lejárt. Kérj egy újat." });
            }

            const hash = await hashPassword(password);

            // A levél megérkezése bizonyítja, hogy a cím az övé: ezt
            // megerősítésnek is vesszük.
            await pool.query(
                `UPDATE users
                 SET password_hash = ?,
                     email_verified_at = COALESCE(email_verified_at, NOW()),
                     created_at = created_at
                 WHERE id = ?`,
                [hash, row.user_id]
            );

            await pool.query("DELETE FROM auth_tokens WHERE user_id = ?", [row.user_id]);

            endSessionsOf(row.user_id);

            debug("🔑 Jelszó visszaállítva, user:", row.user_id);

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/reset-password:", err);

            return res.status(500).json({ error: "Nem sikerült megváltoztatni a jelszót." });
        }
    });


    // A felhasználó összes bejelentkezett munkamenetének megszüntetése
    function endSessionsOf(userId) {

        if (!sessionStore || typeof sessionStore.all !== "function") {
            return;
        }

        sessionStore.all((error, sessions) => {

            if (error || !sessions) {
                return;
            }

            Object.entries(sessions).forEach(([sid, data]) => {

                if (data && data.user && Number(data.user.id) === Number(userId)) {
                    sessionStore.destroy(sid, () => {});
                }
            });
        });
    }


    // ---------------- oldalak ----------------

    const page = (name) => (req, res) =>
        res.sendFile(path.join(__dirname, "../html/" + name));

    app.get("/forgot-password", page("forgot.html"));
    app.get("/reset-password", page("reset.html"));
    app.get("/verify-email", page("verify.html"));


    return {
        ensureSchema,
        issueVerification,
        markUnverified,
        isVerified,
        requireVerified,
        allow,
        endSessionsOf
    };
}


module.exports = { setup };
