// ============================================================
// MODERÁCIÓ: JELENTÉSEK ÉS ADMIN FELÜLET (szerveroldal)
// ============================================================
//
// 1) Jelentés:  a chatben bármelyik fél jelentheti a másikat (vagy egy
//    konkrét üzenetét). A jelentés elmentésekor MÁSOLATOT készítünk a
//    jelentett üzenetről és az előtte lévő néhány üzenetről, mert a
//    jelentett fél utólag visszavonhatja vagy szerkesztheti az üzenetét,
//    és akkor nem maradna bizonyíték. Az admin CSAK ezt a kis kivágatot
//    látja, a teljes beszélgetést nem.
//
// 2) Admin felület: csak ADMIN szerepkörű felhasználónak.
//    Minden /api/admin/... kérésnél az adatbázisból újra megnézzük a
//    szerepkört (nem csak a munkamenetből), így egy elvett jogosultság
//    azonnal érvényes.
//
// 3) Függesztés: a felfüggesztett felhasználó nem tud belépni, a
//    meglévő munkamenetei és websocket kapcsolatai megszűnnek, az
//    oktatói listából eltűnik. Visszavonható.
//
// 4) Napló: minden admin művelet bekerül az admin_log táblába.

const path = require("path");

const REASONS = ["SPAM", "HARASSMENT", "SCAM", "INAPPROPRIATE", "OTHER"];
const MAX_DETAILS = 500;
const MAX_NOTE = 500;
const MAX_SUSPEND_REASON = 200;
const PAGE_SIZE = 20;
const CONTEXT_MESSAGES = 6;

const REPORT_STATUSES = ["OPEN", "RESOLVED", "DISMISSED"];
const ROLES = ["STUDENT", "TUTOR", "ADMIN"];
const BOOKING_STATUSES = ["PENDING", "CONFIRMED", "REJECTED", "COMPLETED", "CANCELLED"];


function setup({ app, db, suspendedUserIds, clients, endSessionsOf, allow }) {

    const pool = db.promise();


    // ---------------- adatbázis ----------------

    async function ensureSchema() {

        try {

            const columns = [
                ["suspended_at", "TIMESTAMP NULL DEFAULT NULL"],
                ["suspended_reason", "VARCHAR(200) NULL DEFAULT NULL"]
            ];

            for (const [column, definition] of columns) {

                const [found] = await pool.query("SHOW COLUMNS FROM users LIKE ?", [column]);

                if (found.length === 0) {

                    await pool.query(`ALTER TABLE users ADD COLUMN ${column} ${definition}`);

                    console.log(`✅ users.${column} oszlop létrehozva`);
                }
            }

            // A created_at "ON UPDATE current_timestamp" volt: a regisztráció
            // dátuma átíródott a felhasználó bármilyen módosításánál (pl. profil
            // mentése). A statisztikához és a listához megbízható dátum kell.
            const [created] = await pool.query("SHOW COLUMNS FROM users LIKE 'created_at'");

            if (created.length > 0 && /on update/i.test(created[0].Extra || "")) {

                await pool.query(
                    "ALTER TABLE users MODIFY created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP"
                );

                console.log("✅ users.created_at: az automatikus frissülés kikapcsolva");
            }

            await pool.query(`
                CREATE TABLE IF NOT EXISTS reports (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    reporter_id INT NOT NULL,
                    reported_user_id INT NOT NULL,
                    conversation_id INT NULL,
                    message_id INT NULL,
                    reason VARCHAR(20) NOT NULL,
                    details VARCHAR(500) NULL,
                    context_json MEDIUMTEXT NULL,
                    status VARCHAR(12) NOT NULL DEFAULT 'OPEN',
                    admin_note VARCHAR(500) NULL,
                    handled_by INT NULL,
                    handled_at DATETIME NULL,
                    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    KEY idx_reports_status (status, created_at),
                    KEY idx_reports_reported (reported_user_id),
                    CONSTRAINT fk_reports_reporter FOREIGN KEY (reporter_id)
                        REFERENCES users (id) ON DELETE CASCADE,
                    CONSTRAINT fk_reports_reported FOREIGN KEY (reported_user_id)
                        REFERENCES users (id) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            `);

            await pool.query(`
                CREATE TABLE IF NOT EXISTS admin_log (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    admin_id INT NOT NULL,
                    action VARCHAR(40) NOT NULL,
                    target_type VARCHAR(20) NULL,
                    target_id INT NULL,
                    details VARCHAR(500) NULL,
                    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    KEY idx_admin_log_created (created_at),
                    CONSTRAINT fk_admin_log_admin FOREIGN KEY (admin_id)
                        REFERENCES users (id) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            `);

            const [suspended] = await pool.query(
                "SELECT id FROM users WHERE suspended_at IS NOT NULL"
            );

            suspended.forEach((row) => suspendedUserIds.add(Number(row.id)));

        } catch (err) {

            console.error("❌ SQL hiba (moderáció séma):", err);
        }
    }


    async function logAction(adminId, action, targetType, targetId, details) {

        try {

            await pool.query(
                "INSERT INTO admin_log (admin_id, action, target_type, target_id, details) VALUES (?, ?, ?, ?, ?)",
                [adminId, action, targetType || null, targetId || null, details ? String(details).slice(0, 500) : null]
            );

        } catch (err) {

            console.error("❌ admin_log:", err);
        }
    }


    function positiveInt(value) {

        const number = Number(value);

        return Number.isInteger(number) && number > 0 ? number : null;
    }


    function pageParams(query) {

        const page = Math.max(1, positiveInt(query.page) || 1);

        return { page, limit: PAGE_SIZE, offset: (page - 1) * PAGE_SIZE };
    }


    // ============================================================
    // JELENTÉS (bármelyik bejelentkezett felhasználó)
    // ============================================================

    app.post("/api/reports", async (req, res) => {

        if (!req.session.user) {
            return res.status(401).json({ error: "Nem vagy bejelentkezve." });
        }

        const reporterId = Number(req.session.user.id);

        const conversationId = positiveInt(req.body.conversation_id);
        const messageId = req.body.message_id ? positiveInt(req.body.message_id) : null;
        const reason = String(req.body.reason || "");
        const details = String(req.body.details || "").trim();

        if (!conversationId) {
            return res.status(400).json({ error: "Hiányzik a beszélgetés." });
        }

        if (req.body.message_id && !messageId) {
            return res.status(400).json({ error: "Érvénytelen üzenet." });
        }

        if (!REASONS.includes(reason)) {
            return res.status(400).json({ error: "Válassz egy okot." });
        }

        if (details.length > MAX_DETAILS) {
            return res.status(400).json({ error: "A megjegyzés legfeljebb " + MAX_DETAILS + " karakter lehet." });
        }

        // Az alap-ellenőrzést kiálló kérések számítanak a korlátba (óránként 5):
        // egy elírás miatt visszadobott kérés nem fogyasztja el a keretet.
        if (!allow("report:" + reporterId, 5, 60 * 60 * 1000)) {
            return res.status(429).json({ error: "Egy órán belül legfeljebb 5 jelentést küldhetsz. Kérlek várj egy kicsit." });
        }

        try {

            // Résztvevő-e a beszélgetésben, és ki a másik fél?
            const [conversations] = await pool.query(
                "SELECT student_id, tutor_id FROM conversations WHERE id = ? LIMIT 1",
                [conversationId]
            );

            if (conversations.length === 0) {
                return res.status(404).json({ error: "A beszélgetés nem található." });
            }

            const { student_id: studentId, tutor_id: tutorId } = conversations[0];

            if (reporterId !== Number(studentId) && reporterId !== Number(tutorId)) {
                return res.status(403).json({ error: "Ebben a beszélgetésben nem veszel részt." });
            }

            const reportedId = reporterId === Number(studentId) ? Number(tutorId) : Number(studentId);

            const [targets] = await pool.query(
                "SELECT role, deleted_at FROM users WHERE id = ? LIMIT 1",
                [reportedId]
            );

            if (targets.length === 0 || targets[0].deleted_at) {
                return res.status(400).json({ error: "Ez a felhasználó már nem létezik." });
            }

            if (targets[0].role === "ADMIN") {
                return res.status(400).json({ error: "Adminisztrátor nem jelenthető ezen a felületen." });
            }

            // Ha konkrét üzenetet jelent: az a beszélgetés üzenete és a másik féltől van?
            if (messageId) {

                const [owned] = await pool.query(
                    "SELECT id FROM messages WHERE id = ? AND conversation_id = ? AND sender_id = ? AND deleted_at IS NULL",
                    [messageId, conversationId, reportedId]
                );

                if (owned.length === 0) {
                    return res.status(400).json({ error: "Ez az üzenet nem jelenthető." });
                }
            }

            // Ugyanazt ne lehessen többször beküldeni, amíg az előző nyitott
            const [duplicate] = await pool.query(
                `SELECT id FROM reports
                 WHERE reporter_id = ? AND reported_user_id = ? AND status = 'OPEN'
                   AND ((message_id IS NULL AND ? IS NULL) OR message_id = ?)
                 LIMIT 1`,
                [reporterId, reportedId, messageId, messageId]
            );

            if (duplicate.length > 0) {
                return res.status(409).json({ error: "Ezt már jelentetted, az ügyet még vizsgáljuk." });
            }

            // Másolat a jelentett üzenetről és az előzményekről
            const [recent] = messageId
                ? await pool.query(
                    `SELECT id, sender_id, content, created_at, attachment_name, deleted_at
                     FROM messages WHERE conversation_id = ? AND id <= ?
                     ORDER BY id DESC LIMIT ?`,
                    [conversationId, messageId, CONTEXT_MESSAGES]
                )
                : await pool.query(
                    `SELECT id, sender_id, content, created_at, attachment_name, deleted_at
                     FROM messages WHERE conversation_id = ?
                     ORDER BY id DESC LIMIT ?`,
                    [conversationId, CONTEXT_MESSAGES]
                );

            const context = recent.reverse().map((message) => ({
                id: message.id,
                sender_id: message.sender_id,
                // a korábban már visszavont üzenet tartalmát nem tároljuk
                content: message.deleted_at ? null : String(message.content || "").slice(0, 1000),
                retracted: Boolean(message.deleted_at),
                attachment_name: message.deleted_at ? null : message.attachment_name,
                created_at: message.created_at,
                target: messageId !== null && Number(message.id) === messageId
            }));

            const [inserted] = await pool.query(
                `INSERT INTO reports
                 (reporter_id, reported_user_id, conversation_id, message_id, reason, details, context_json)
                 VALUES (?, ?, ?, ?, ?, ?, ?)`,
                [reporterId, reportedId, conversationId, messageId, reason, details || null, JSON.stringify(context)]
            );

            console.log("🚩 Új jelentés:", inserted.insertId, "(jelentő:", reporterId, "→ jelentett:", reportedId + ")");

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/reports:", err);

            return res.status(500).json({ error: "Nem sikerült elküldeni a jelentést." });
        }
    });


    // ============================================================
    // ADMIN
    // ============================================================

    async function currentAdmin(req) {

        if (!req.session.user) {
            return null;
        }

        const [rows] = await pool.query(
            `SELECT id FROM users
             WHERE id = ? AND role = 'ADMIN' AND deleted_at IS NULL AND suspended_at IS NULL
             LIMIT 1`,
            [req.session.user.id]
        );

        return rows.length > 0 ? Number(rows[0].id) : null;
    }


    // Az admin oldal: nem adminnak átirányítás (a tartalma így sem látszik)
    app.get("/admin", async (req, res) => {

        try {

            if (!req.session.user) {
                return res.redirect("/login");
            }

            if (!(await currentAdmin(req))) {
                return res.redirect("/");
            }

            return res.sendFile(path.join(__dirname, "../html/admin.html"));

        } catch (err) {

            console.error("❌ GET /admin:", err);

            return res.status(500).send("Hiba történt.");
        }
    });


    app.use("/api/admin", async (req, res, next) => {

        try {

            if (!req.session.user) {
                return res.status(401).json({ error: "Nem vagy bejelentkezve." });
            }

            const adminId = await currentAdmin(req);

            if (!adminId) {
                return res.status(403).json({ error: "Ehhez adminisztrátori jogosultság kell." });
            }

            req.adminId = adminId;

            return next();

        } catch (err) {

            console.error("❌ admin jogosultság:", err);

            return res.status(500).json({ error: "Hiba történt." });
        }
    });


    // ---------------- áttekintés ----------------

    app.get("/api/admin/stats", async (req, res) => {

        try {

            const [[users]] = await pool.query(`
                SELECT
                    COUNT(*) AS total,
                    SUM(role = 'STUDENT') AS students,
                    SUM(role = 'TUTOR') AS tutors,
                    SUM(role = 'ADMIN') AS admins,
                    SUM(created_at >= DATE_SUB(NOW(), INTERVAL 7 DAY)) AS new_week,
                    SUM(email_verified_at IS NULL) AS unverified,
                    SUM(suspended_at IS NOT NULL) AS suspended
                FROM users WHERE deleted_at IS NULL
            `);

            const [bookingRows] = await pool.query(
                "SELECT status_ AS status, COUNT(*) AS n FROM bookings GROUP BY status_"
            );

            const bookings = {};
            bookingRows.forEach((row) => { bookings[row.status] = Number(row.n); });

            const [[reviews]] = await pool.query("SELECT COUNT(*) AS n, AVG(rating) AS avg FROM reviews");

            const [[reports]] = await pool.query(`
                SELECT SUM(status = 'OPEN') AS open_count, COUNT(*) AS total FROM reports
            `);

            const [[messages]] = await pool.query("SELECT COUNT(*) AS n FROM messages");

            const [log] = await pool.query(`
                SELECT l.id, l.action, l.target_type, l.target_id, l.details, l.created_at,
                       u.full_name AS admin_name
                FROM admin_log l JOIN users u ON u.id = l.admin_id
                ORDER BY l.id DESC LIMIT 12
            `);

            return res.json({
                users: {
                    total: Number(users.total) || 0,
                    students: Number(users.students) || 0,
                    tutors: Number(users.tutors) || 0,
                    admins: Number(users.admins) || 0,
                    new_week: Number(users.new_week) || 0,
                    unverified: Number(users.unverified) || 0,
                    suspended: Number(users.suspended) || 0
                },
                bookings,
                reviews: { count: Number(reviews.n) || 0, average: reviews.avg ? Number(reviews.avg) : null },
                reports: { open: Number(reports.open_count) || 0, total: Number(reports.total) || 0 },
                messages: Number(messages.n) || 0,
                log
            });

        } catch (err) {

            console.error("❌ GET /api/admin/stats:", err);

            return res.status(500).json({ error: "Nem sikerült betölteni." });
        }
    });


    // ---------------- jelentések ----------------

    app.get("/api/admin/reports", async (req, res) => {

        const status = String(req.query.status || "OPEN").toUpperCase();
        const { page, limit, offset } = pageParams(req.query);

        const where = REPORT_STATUSES.includes(status) ? "WHERE r.status = ?" : "";
        const params = where ? [status] : [];

        try {

            const [[count]] = await pool.query(`SELECT COUNT(*) AS n FROM reports r ${where}`, params);

            const [rows] = await pool.query(
                `SELECT r.id, r.reason, r.details, r.status, r.admin_note, r.message_id,
                        r.created_at, r.handled_at,
                        r.reporter_id, rep.full_name AS reporter_name, rep.email AS reporter_email,
                        r.reported_user_id, tgt.full_name AS reported_name, tgt.email AS reported_email,
                        tgt.role AS reported_role, tgt.suspended_at AS reported_suspended,
                        adm.full_name AS handled_by_name,
                        (SELECT COUNT(*) FROM reports x
                          WHERE x.reported_user_id = r.reported_user_id AND x.id <> r.id) AS other_reports
                 FROM reports r
                 JOIN users rep ON rep.id = r.reporter_id
                 JOIN users tgt ON tgt.id = r.reported_user_id
                 LEFT JOIN users adm ON adm.id = r.handled_by
                 ${where}
                 ORDER BY (r.status = 'OPEN') DESC, r.id DESC
                 LIMIT ? OFFSET ?`,
                [...params, limit, offset]
            );

            return res.json({ items: rows, total: Number(count.n), page, pageSize: limit });

        } catch (err) {

            console.error("❌ GET /api/admin/reports:", err);

            return res.status(500).json({ error: "Nem sikerült betölteni." });
        }
    });


    app.get("/api/admin/reports/:id", async (req, res) => {

        const id = positiveInt(req.params.id);

        if (!id) {
            return res.status(400).json({ error: "Érvénytelen azonosító." });
        }

        try {

            const [rows] = await pool.query(
                `SELECT r.*, rep.full_name AS reporter_name, tgt.full_name AS reported_name
                 FROM reports r
                 JOIN users rep ON rep.id = r.reporter_id
                 JOIN users tgt ON tgt.id = r.reported_user_id
                 WHERE r.id = ? LIMIT 1`,
                [id]
            );

            if (rows.length === 0) {
                return res.status(404).json({ error: "A jelentés nem található." });
            }

            const report = rows[0];

            let context = [];

            try {
                context = JSON.parse(report.context_json || "[]");
            } catch (error) {
                context = [];
            }

            delete report.context_json;

            return res.json({ report, context });

        } catch (err) {

            console.error("❌ GET /api/admin/reports/:id:", err);

            return res.status(500).json({ error: "Nem sikerült betölteni." });
        }
    });


    app.post("/api/admin/reports/:id/close", async (req, res) => {

        const id = positiveInt(req.params.id);
        const outcome = String(req.body.outcome || "").toUpperCase();
        const note = String(req.body.note || "").trim();

        if (!id) {
            return res.status(400).json({ error: "Érvénytelen azonosító." });
        }

        if (!["RESOLVED", "DISMISSED"].includes(outcome)) {
            return res.status(400).json({ error: "Érvénytelen döntés." });
        }

        if (note.length > MAX_NOTE) {
            return res.status(400).json({ error: "A megjegyzés legfeljebb " + MAX_NOTE + " karakter lehet." });
        }

        try {

            const [result] = await pool.query(
                `UPDATE reports
                 SET status = ?, admin_note = ?, handled_by = ?, handled_at = NOW()
                 WHERE id = ?`,
                [outcome, note || null, req.adminId, id]
            );

            if (result.affectedRows === 0) {
                return res.status(404).json({ error: "A jelentés nem található." });
            }

            await logAction(
                req.adminId,
                outcome === "RESOLVED" ? "report_resolved" : "report_dismissed",
                "report", id, note
            );

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/admin/reports/:id/close:", err);

            return res.status(500).json({ error: "Nem sikerült menteni." });
        }
    });


    // A lezárt jelentés újranyitása (ha az admin tévedett)
    app.post("/api/admin/reports/:id/reopen", async (req, res) => {

        const id = positiveInt(req.params.id);

        if (!id) {
            return res.status(400).json({ error: "Érvénytelen azonosító." });
        }

        try {

            const [result] = await pool.query(
                `UPDATE reports SET status = 'OPEN', handled_by = NULL, handled_at = NULL
                 WHERE id = ? AND status <> 'OPEN'`,
                [id]
            );

            if (result.affectedRows === 0) {
                return res.status(404).json({ error: "A jelentés nem található, vagy már nyitott." });
            }

            await logAction(req.adminId, "report_reopened", "report", id, null);

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/admin/reports/:id/reopen:", err);

            return res.status(500).json({ error: "Nem sikerült menteni." });
        }
    });


    // ---------------- felhasználók ----------------

    app.get("/api/admin/users", async (req, res) => {

        const q = String(req.query.q || "").trim().slice(0, 100);
        const role = String(req.query.role || "").toUpperCase();
        const status = String(req.query.status || "");
        const { page, limit, offset } = pageParams(req.query);

        const conditions = [];
        const params = [];

        if (q) {
            const like = "%" + q.replace(/[%_\\]/g, "\\$&") + "%";
            conditions.push("(u.full_name LIKE ? OR u.email LIKE ?)");
            params.push(like, like);
        }

        if (ROLES.includes(role)) {
            conditions.push("u.role = ?");
            params.push(role);
        }

        if (status === "suspended") {
            conditions.push("u.suspended_at IS NOT NULL AND u.deleted_at IS NULL");
        } else if (status === "unverified") {
            conditions.push("u.email_verified_at IS NULL AND u.deleted_at IS NULL");
        } else if (status === "deleted") {
            conditions.push("u.deleted_at IS NOT NULL");
        } else if (status === "active") {
            conditions.push("u.deleted_at IS NULL AND u.suspended_at IS NULL");
        }

        const where = conditions.length ? "WHERE " + conditions.join(" AND ") : "";

        try {

            const [[count]] = await pool.query(`SELECT COUNT(*) AS n FROM users u ${where}`, params);

            const [rows] = await pool.query(
                `SELECT u.id, u.full_name, u.email, u.role, u.created_at, u.email_verified_at,
                        u.suspended_at, u.suspended_reason, u.deleted_at,
                        (SELECT COUNT(*) FROM reports r
                          WHERE r.reported_user_id = u.id AND r.status = 'OPEN') AS open_reports,
                        (SELECT COUNT(*) FROM reports r WHERE r.reported_user_id = u.id) AS all_reports
                 FROM users u
                 ${where}
                 ORDER BY u.id DESC
                 LIMIT ? OFFSET ?`,
                [...params, limit, offset]
            );

            return res.json({ items: rows, total: Number(count.n), page, pageSize: limit });

        } catch (err) {

            console.error("❌ GET /api/admin/users:", err);

            return res.status(500).json({ error: "Nem sikerült betölteni." });
        }
    });


    async function loadTarget(req, res) {

        const id = positiveInt(req.params.id);

        if (!id) {
            res.status(400).json({ error: "Érvénytelen azonosító." });
            return null;
        }

        const [rows] = await pool.query(
            "SELECT id, full_name, role, deleted_at, suspended_at FROM users WHERE id = ? LIMIT 1",
            [id]
        );

        if (rows.length === 0) {
            res.status(404).json({ error: "A felhasználó nem található." });
            return null;
        }

        return rows[0];
    }


    app.post("/api/admin/users/:id/suspend", async (req, res) => {

        const reason = String(req.body.reason || "").trim();

        if (reason.length < 3 || reason.length > MAX_SUSPEND_REASON) {
            return res.status(400).json({
                error: "Adj meg egy indoklást (3–" + MAX_SUSPEND_REASON + " karakter)."
            });
        }

        try {

            const target = await loadTarget(req, res);

            if (!target) {
                return;
            }

            if (Number(target.id) === req.adminId) {
                return res.status(400).json({ error: "Magadat nem függesztheted fel." });
            }

            if (target.role === "ADMIN") {
                return res.status(400).json({ error: "Adminisztrátor nem függeszthető fel." });
            }

            if (target.deleted_at) {
                return res.status(400).json({ error: "A fiók már törölve van." });
            }

            await pool.query(
                "UPDATE users SET suspended_at = NOW(), suspended_reason = ? WHERE id = ?",
                [reason, target.id]
            );

            suspendedUserIds.add(Number(target.id));

            // azonnali kitiltás: websocket és munkamenetek
            const sockets = clients.get(Number(target.id));

            if (sockets) {
                sockets.forEach((ws) => ws.close());
            }

            endSessionsOf(target.id);

            await logAction(req.adminId, "user_suspended", "user", target.id, reason);

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/admin/users/:id/suspend:", err);

            return res.status(500).json({ error: "Nem sikerült menteni." });
        }
    });


    app.post("/api/admin/users/:id/unsuspend", async (req, res) => {

        try {

            const target = await loadTarget(req, res);

            if (!target) {
                return;
            }

            if (!target.suspended_at) {
                return res.status(400).json({ error: "A fiók nincs felfüggesztve." });
            }

            await pool.query(
                "UPDATE users SET suspended_at = NULL, suspended_reason = NULL WHERE id = ?",
                [target.id]
            );

            suspendedUserIds.delete(Number(target.id));

            await logAction(req.adminId, "user_unsuspended", "user", target.id, null);

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/admin/users/:id/unsuspend:", err);

            return res.status(500).json({ error: "Nem sikerült menteni." });
        }
    });


    // Kézi megerősítés (pl. ha a felhasználó nem kapja meg a levelet)
    app.post("/api/admin/users/:id/verify-email", async (req, res) => {

        try {

            const target = await loadTarget(req, res);

            if (!target) {
                return;
            }

            await pool.query(
                "UPDATE users SET email_verified_at = COALESCE(email_verified_at, NOW()) WHERE id = ?",
                [target.id]
            );

            await logAction(req.adminId, "user_email_verified", "user", target.id, null);

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ POST /api/admin/users/:id/verify-email:", err);

            return res.status(500).json({ error: "Nem sikerült menteni." });
        }
    });


    // ---------------- értékelések ----------------

    app.get("/api/admin/reviews", async (req, res) => {

        const { page, limit, offset } = pageParams(req.query);

        try {

            const [[count]] = await pool.query("SELECT COUNT(*) AS n FROM reviews");

            const [rows] = await pool.query(
                `SELECT r.id, r.rating, r.comment_ AS comment, r.crated_at AS created_at,
                        b.id AS booking_id,
                        s.id AS student_id, s.full_name AS student_name,
                        t.id AS tutor_id, t.full_name AS tutor_name
                 FROM reviews r
                 JOIN bookings b ON b.id = r.booking_id
                 JOIN users s ON s.id = b.student_id
                 JOIN users t ON t.id = b.tutor_id
                 ORDER BY r.id DESC
                 LIMIT ? OFFSET ?`,
                [limit, offset]
            );

            return res.json({ items: rows, total: Number(count.n), page, pageSize: limit });

        } catch (err) {

            console.error("❌ GET /api/admin/reviews:", err);

            return res.status(500).json({ error: "Nem sikerült betölteni." });
        }
    });


    app.delete("/api/admin/reviews/:id", async (req, res) => {

        const id = positiveInt(req.params.id);
        const reason = String(req.body && req.body.reason || "").trim().slice(0, 200);

        if (!id) {
            return res.status(400).json({ error: "Érvénytelen azonosító." });
        }

        try {

            const [result] = await pool.query("DELETE FROM reviews WHERE id = ?", [id]);

            if (result.affectedRows === 0) {
                return res.status(404).json({ error: "Az értékelés nem található." });
            }

            await logAction(req.adminId, "review_deleted", "review", id, reason);

            return res.json({ success: true });

        } catch (err) {

            console.error("❌ DELETE /api/admin/reviews/:id:", err);

            return res.status(500).json({ error: "Nem sikerült törölni." });
        }
    });


    // ---------------- foglalások (csak olvasható) ----------------

    app.get("/api/admin/bookings", async (req, res) => {

        const status = String(req.query.status || "").toUpperCase();
        const { page, limit, offset } = pageParams(req.query);

        const where = BOOKING_STATUSES.includes(status) ? "WHERE b.status_ = ?" : "";
        const params = where ? [status] : [];

        try {

            const [[count]] = await pool.query(`SELECT COUNT(*) AS n FROM bookings b ${where}`, params);

            const [rows] = await pool.query(
                `SELECT b.id, b.status_ AS status, b.start_time, b.end_time, b.price,
                        s.id AS student_id, s.full_name AS student_name,
                        t.id AS tutor_id, t.full_name AS tutor_name,
                        sub.name AS subject_name
                 FROM bookings b
                 LEFT JOIN users s ON s.id = b.student_id
                 LEFT JOIN users t ON t.id = b.tutor_id
                 LEFT JOIN subjects sub ON sub.id = b.subject_id
                 ${where}
                 ORDER BY b.start_time DESC, b.id DESC
                 LIMIT ? OFFSET ?`,
                [...params, limit, offset]
            );

            return res.json({ items: rows, total: Number(count.n), page, pageSize: limit });

        } catch (err) {

            console.error("❌ GET /api/admin/bookings:", err);

            return res.status(500).json({ error: "Nem sikerült betölteni." });
        }
    });


    return { ensureSchema };
}


module.exports = { setup };
