const express = require("express");
const path = require("path");
const fs = require("fs");
const crypto = require("crypto");
const multer = require("multer");
const mysql = require("mysql2");
const session = require("express-session");
const http = require("http");
const { WebSocketServer } = require("ws");


const { hashPassword, verifyPassword, debug, DEBUG_LOGS } = require("./auth");

const app = express();

// ============================================================
// EXPRESS SETUP
// ============================================================

console.log("==========================================");
console.log("🚀 TUTOR CONNECT SERVER STARTING");
console.log("==========================================");

app.use(express.urlencoded({ extended: true }));
app.use(express.json());

// ============================================================
// LOGIN SESSION
// ============================================================
//
// HOW "STAYING LOGGED IN" WORKS (overview):
//
// 1. When a user logs in, the server stores their data
//    (id, name, email, role) in a "session" that lives on the
//    SERVER (in req.session.user).
//
// 2. The server gives the browser a small cookie called
//    "connect.sid". It contains only a random session ID
//    (signed with the secret below) - NOT the user's data.
//
// 3. On every later request, the browser automatically sends
//    that cookie back. express-session reads it, finds the
//    matching session and fills in req.session again.
//    So if req.session.user exists => the user is logged in.
//
// 4. The cookie lasts 7 days (maxAge), so the user stays
//    logged in even after closing the browser.
//
// IMPORTANT: by default sessions are kept in the server's
// memory. If the server restarts, all sessions are lost and
// everybody has to log in again. (A persistent session store,
// e.g. MySQL-based, would fix that.)
//
// ============================================================

const sessionMiddleware = session({
        // Secret used to sign the session cookie so it can't be forged.
        // In production set the SESSION_SECRET environment variable
        // and don't rely on the fallback value.
        secret: process.env.SESSION_SECRET || "tutorconnect-dev-secret",
        // Don't re-save the session on every request if nothing changed.
        resave: false,
        // Don't create a session (and cookie) for visitors who never log in.
        saveUninitialized: false,

        cookie: {
            // JavaScript in the browser can NOT read this cookie
            // (protects the session from XSS attacks).
            httpOnly: true,
            // The cookie is not sent on cross-site POST requests (CSRF protection),
            // but it IS sent when the user simply navigates to our site.
            sameSite: "lax",

            // For localhost development use false.
            // If you later use HTTPS, change this to true.
            secure: false,

            // How long the cookie (= the login) lasts, in milliseconds:
            // 1000 ms * 60 s * 60 min * 24 h * 7 days = 7 days
            maxAge: 1000 * 60 * 60 * 24 * 7
        }
});

app.use(sessionMiddleware);

// A törölt (anonimizált) fiókok azonosítói. Ha egy ilyen felhasználónak
// még él egy munkamenete egy másik böngészőben, a következő kérésnél
// kijelentkeztetjük. Induláskor az adatbázisból töltődik fel.
const deletedUserIds = new Set();

app.use((req, res, next) => {

    if (
        req.session &&
        req.session.user &&
        deletedUserIds.has(Number(req.session.user.id))
    ) {
        delete req.session.user;
    }

    next();
});

// ============================================================
// STATIC FILES
// ============================================================

// A /js mappából a böngészőnek CSAK az itt felsorolt kliens fájlok járnak.
// A mappában a szerver forráskódja is van (main.js, auth.js), amely az
// adatbázis-hozzáférést és a session titkot tartalmazza, ezért azt soha
// nem szabad kiszolgálni.
//
// Fehérlistát használunk, nem tiltólistát: ha egy új kliens fájl
// kimarad innen, az azonnal látszik (404), míg egy tiltólistáról
// elfelejtett szerverfájl csendben kiszivárogna.
// ÚJ KLIENS SCRIPT HOZZÁADÁSAKOR ITT IS FEL KELL VENNI!
const PUBLIC_JS_FILES = new Set([
    "creator.js",
    "chat.js",
    "index.js",
    "dropdown.js",
    "oktatok.js",
    "booking.js",
    "foglalas.js",
    "foglalasutan.js"
]);

// Ennek a statikus kiszolgálás ELŐTT kell futnia, különben az
// express.static már kiadja a fájlt.
app.use((req, res, next) => {

    let segments;

    try {
        segments = decodeURIComponent(req.path)
            .split(/[\\/]+/)
            .filter(Boolean)
            .map((segment) => segment.toLowerCase());
    } catch (error) {
        return res.status(400).end();
    }

    const firstSegment = segments[0] || "";

    if (firstSegment === "node_modules") {
        return res.status(404).end();
    }

    // /js/<fájl>: csak a fehérlistáról. Mappa, "..", "." vagy ismeretlen
    // fájlnév (pl. MAIN.JS, main.js., main~1.js) esetén 404.
    if (firstSegment === "js") {

        if (segments.length !== 2 || !PUBLIC_JS_FILES.has(segments[1])) {
            return res.status(404).end();
        }
    }

    next();
});

app.use("/css", express.static(path.join(__dirname, "../css")));
app.use("/js", express.static(path.join(__dirname, "../js")));
app.use("/html", express.static(path.join(__dirname, "../html")));
app.use("/images", express.static(path.join(__dirname, "../images")));


// Serve the project root from the actual vsc folder regardless of
// the terminal's current working directory.
app.use(express.static(path.join(__dirname, "..")));

// ============================================================
// MYSQL CONNECTION
// ============================================================

console.log("\n📦 Creating MySQL connection...");

const db = mysql.createConnection({
    host: "localhost",
    user: "root",
    password: "",
    database: "zsamo"
});

// ============================================================
// BASIC PAGES
// ============================================================

app.get("/", (req, res) => {
    console.log("🌐 GET /");

    res.sendFile(
        path.join(__dirname, "../html/index.html")
    );
});


app.get("/index", (req, res) => {
    console.log("🌐 GET /index");

    res.sendFile(
        path.join(__dirname, "../html/index.html")
    );
});


app.get("/subs", (req, res) => {
    console.log("🌐 GET /subs");

    res.sendFile(
        path.join(__dirname, "../html/subs.html")
    );
});


app.get("/login", (req, res) => {
    console.log("🌐 GET /login");

    res.sendFile(
        path.join(__dirname, "../html/login.html")
    );
});


app.get("/register", (req, res) => {
    console.log("🌐 GET /register");

    res.sendFile(
        path.join(__dirname, "../html/register.html")
    );
});


app.get("/gyik", (req, res) => {
    res.sendFile(
        path.join(__dirname, "../html/gyik.html")
    );
});


app.get("/chat", (req, res) => {
    res.sendFile(
        path.join(__dirname, "../html/chat.html")
    );
});


app.get("/oktatok", (req, res) => {
    res.sendFile(
        path.join(__dirname, "../html/oktatok.html")
    );
});


app.get("/foglalas", (req, res) => {
    res.sendFile(
        path.join(__dirname, "../html/foglalas.html")
    );
});


app.get("/foglalasutan", (req, res) => {
    res.sendFile(
        path.join(__dirname, "../html/foglalasutan.html")
    );
});


// ============================================================
// CURRENT USER
// ============================================================
//
// This is how the frontend "remembers" the login.
// creator.js calls this on EVERY page load. The browser sends
// the session cookie automatically, express-session restores
// req.session, and we simply report what is inside it.
//
// creator.js uses this endpoint to find out:
// - Is the user logged in?
// - What is their name?
// - What is their role?
//
// ============================================================

app.get("/api/me", (req, res) => {

    // No user stored in the session => nobody is logged in
    // (no cookie, expired cookie, or the session was destroyed).
    if (!req.session.user) {

        return res.json({
            loggedIn: false
        });
    }


    // A user is stored in the session => still logged in.
    // Only send the safe fields (never the password hash).
    return res.json({

        loggedIn: true,

        user: {
            id: req.session.user.id,
            name: req.session.user.name,
            email: req.session.user.email,
            role: req.session.user.role
        }
    });
});


// ============================================================
// LOGOUT
// ============================================================

app.post("/logout", (req, res) => {

    // Deleting the session from the server = the user is logged out.
    // Even if the browser still has the cookie, it no longer matches
    // any session, so /api/me will answer "loggedIn: false".
    req.session.destroy((err) => {

        if (err) {

            console.error(
                "❌ LOGOUT ERROR:",
                err
            );

            return res.status(500).json({
                success: false,
                message: "Nem sikerült kijelentkezni."
            });
        }


        // Also remove the (now useless) session cookie from the browser
        res.clearCookie("connect.sid");


        return res.json({
            success: true
        });
    });
});


// ============================================================
// REGISTER
// ============================================================

app.post("/register", async (req, res) => {

    console.log("\n");
    console.log("==========================================");
    console.log("📝 REGISTRATION REQUEST RECEIVED");
    console.log("==========================================");


    // A kérés törzse a jelszót is tartalmazza: csak teszt-naplózásnál írjuk ki
    debug("\n📥 RAW DATA RECEIVED:");
    debug(req.body);


    const {
        name,
        email,
        password,
        password_confirm,
        role
    } = req.body;


    // --------------------------------------------------------
    // VALIDATION
    // --------------------------------------------------------

    if (
        !name ||
        !email ||
        !password ||
        !password_confirm ||
        !role
    ) {

        return res.status(400).send(
            "Minden mezőt ki kell tölteni."
        );
    }


    if (password !== password_confirm) {

        return res.status(400).send(
            "A két jelszó nem egyezik."
        );
    }


    if (String(name).length > 60) {

        return res.status(400).send(
            "A név legfeljebb 60 karakter lehet."
        );
    }


    if (String(email).length > MAX_EMAIL_LENGTH) {

        return res.status(400).send(
            "Az e-mail cím legfeljebb " + MAX_EMAIL_LENGTH + " karakter lehet."
        );
    }


    const allowedRoles = [
        "STUDENT",
        "TUTOR"
    ];


    if (!allowedRoles.includes(role)) {

        return res.status(400).send(
            "Érvénytelen szerepkör."
        );
    }


    // --------------------------------------------------------
    // HASH PASSWORD
    // --------------------------------------------------------

    let passwordHash;

    try {

        passwordHash =
            await hashPassword(password);

    } catch (error) {

        console.error(
            "❌ HASHING FAILED:",
            error
        );

        return res.status(500).send(
            "Nem sikerült feldolgozni a jelszót."
        );
    }


    // --------------------------------------------------------
    // INSERT USER
    // --------------------------------------------------------

    const sql = `
        INSERT INTO users
        (
            full_name,
            email,
            password_hash,
            role,
            bio,
            hourly_rate
        )
        VALUES (?, ?, ?, ?, ?, ?)
    `;


    const values = [
        name,
        email,
        passwordHash,
        role,
        "",
        0
    ];


    db.query(
        sql,
        values,
        (err, result) => {

            if (err) {

                console.error(
                    "❌ MYSQL ERROR:",
                    err
                );


                if (
                    err.code === "ER_DUP_ENTRY" &&
                    err.sqlMessage &&
                    err.sqlMessage.includes("email")
                ) {

                    return res.status(409).send(
                        "Ez az e-mail cím már regisztrálva van."
                    );
                }


                return res.status(500).send(
                    "Adatbázis hiba: " +
                    err.message
                );
            }


            console.log(
                "✅ REGISTRATION COMPLETE"
            );

            console.log(
                "Inserted ID:",
                result.insertId
            );


            // ------------------------------------------------
            // AUTOMATIC LOGIN AFTER REGISTRATION
            // ------------------------------------------------
            //
            // The account was just created, so we already know
            // who this is. We do exactly what the /login route does
            // after a successful password check: put the user into
            // the session. That sets the cookie and the user is
            // logged in without having to type anything again.
            //
            // result.insertId = the new user's id from MySQL.
            // name / email / role come from the (validated) form.

            req.session.user = {

                id: result.insertId,

                name: name,

                email: email,

                role: role
            };


            // Save the session before redirecting (same reason
            // as in /login).
            req.session.save(
                (sessionError) => {

                    if (sessionError) {

                        console.error(
                            "❌ SESSION SAVE ERROR (register):",
                            sessionError
                        );

                        // The account exists, only the auto-login failed,
                        // so send the user to the login page.
                        return res.redirect(
                            "/login"
                        );
                    }


                    console.log(
                        "💾 SESSION CREATED (auto login after register)"
                    );


                    // Logged in - go to the homepage.
                    return res.redirect(
                        "/index"
                    );
                }
            );
        }
    );
});


// ============================================================
// LOGIN
// ============================================================

app.post("/login", async (req, res) => {

    console.log("\n");
    console.log("==========================================");
    console.log("🔑 LOGIN REQUEST RECEIVED");
    console.log("==========================================");


    const {
        email,
        password
    } = req.body;


    // --------------------------------------------------------
    // VALIDATE
    // --------------------------------------------------------

    if (!email || !password) {

        return res.status(400).send(
            "E-mail és jelszó szükséges."
        );
    }


    // --------------------------------------------------------
    // FIND USER
    // --------------------------------------------------------

    const sql = `
        SELECT
            id,
            full_name,
            email,
            password_hash,
            role
        FROM users
        WHERE email = ?
        LIMIT 1
    `;


    db.query(
        sql,
        [email],
        async (err, results) => {

            if (err) {

                console.error(
                    "❌ MYSQL LOGIN ERROR:",
                    err
                );

                return res.status(500).send(
                    "Adatbázis hiba."
                );
            }


            // User doesn't exist
            if (results.length === 0) {

                return res.status(401).send(
                    "Hibás e-mail vagy jelszó."
                );
            }


            const user = results[0];


            console.log("\n👤 USER FOUND:");
            console.log(
                "ID:",
                user.id
            );

            console.log(
                "Name:",
                user.full_name
            );

            console.log(
                "Email:",
                user.email
            );

            console.log(
                "Role:",
                user.role
            );


            // ------------------------------------------------
            // VERIFY PASSWORD
            // ------------------------------------------------

            try {

                const validPassword =
                    await verifyPassword(
                        user.password_hash,
                        password
                    );


                if (!validPassword) {

                    console.log(
                        "❌ LOGIN FAILED"
                    );

                    return res.status(401).send(
                        "Hibás e-mail vagy jelszó."
                    );
                }


                // ------------------------------------------------
                // LOGIN SUCCESSFUL
                // ------------------------------------------------

                console.log(
                    "✅ LOGIN SUCCESSFUL"
                );


                // THIS is the moment the user becomes "logged in":
                // we store their data in the session. From now on every
                // request carrying their cookie will have req.session.user.
                // (Only store what you need - never the password hash.)
                req.session.user = {

                    id: user.id,

                    name: user.full_name,

                    email: user.email,

                    role: user.role
                };


                // Make sure the session is really saved (and the cookie is
                // set) BEFORE redirecting, otherwise the next page might
                // load before the server knows the user is logged in.
                req.session.save(
                    (sessionError) => {

                        if (sessionError) {

                            console.error(
                                "❌ SESSION SAVE ERROR:",
                                sessionError
                            );

                            return res.status(500).send(
                                "Nem sikerült létrehozni a bejelentkezést."
                            );
                        }


                        console.log(
                            "💾 SESSION CREATED"
                        );


                        console.log(
                            "👤 User:",
                            user.full_name
                        );


                        console.log(
                            "🎭 Role:",
                            user.role
                        );


                        // ------------------------------------------------
                        // REDIRECT TO INDEX
                        // ------------------------------------------------

                        return res.redirect(
                            "/index"
                        );
                    }
                );

            } catch (error) {

                console.error(
                    "❌ PASSWORD VERIFICATION ERROR:",
                    error
                );

                return res.status(500).send(
                    "Bejelentkezési hiba."
                );
            }
        }
    );
});


// ============================================================
// SUBJECTS API
// ============================================================

app.get(
    "/api/subjects",
    (req, res) => {

        console.log(
            "📚 GET /api/subjects"
        );


        const sql =
            "SELECT * FROM subjects";


        db.query(
            sql,
            (err, results) => {

                if (err) {

                    console.error(
                        "❌ SQL hiba:",
                        err
                    );


                    return res.status(500).json({
                        error: "Adatbázis hiba"
                    });
                }


                console.log(
                    "✅ Tantárgyak lekérve:",
                    results.length
                );


                res.json(results);
            }
        );
    }
);


// ============================================================
// MYSQL CONNECT
// ============================================================

db.connect(
    (err) => {

        if (err) {

            console.error(
                "❌ MYSQL CONNECTION ERROR"
            );

            console.error(err);

            return;
        }


        console.log(
            "✅ Sikeresen csatlakozva a MySQL-hez!"
        );

        console.log(
            "📊 Database: zsamo"
        );

        ensureMessageColumns();
        ensureUserEmailLength();
        ensureProfileSchema();
        ensureBookingSchema();

        console.log(
            "🖥️ Host: localhost"
        );
    }
);





app.get("/api/tutors", (req, res) => {

    const sql = `
        SELECT
            u.id,
            u.full_name,
            u.email,
            u.bio,

            -- legolcsóbb tantárgy ára (a régi hourly_rate mező helyett)
            COALESCE(MIN(ts.hourly_rate), 0) AS hourly_rate,
            COALESCE(MAX(ts.hourly_rate), 0) AS max_hourly_rate,

            COALESCE(
                review_stats.average_rating,
                0
            ) AS average_rating,

            COALESCE(
                review_stats.review_count,
                0
            ) AS review_count,

            GROUP_CONCAT(
                DISTINCT s.name
                ORDER BY s.name
                SEPARATOR ', '
            ) AS subjects,

            -- tantárgyanként az ár: "id|név|ár;;id|név|ár"
            GROUP_CONCAT(
                DISTINCT CONCAT(s.id, '|', s.name, '|', ts.hourly_rate)
                ORDER BY s.name
                SEPARATOR ';;'
            ) AS subject_prices

        FROM users u

        LEFT JOIN tutor_subjects ts
            ON ts.tutor_id = u.id

        LEFT JOIN subjects s
            ON s.id = ts.subject_id

        LEFT JOIN (
            SELECT
                b.tutor_id,

                AVG(
                    CAST(r.rating AS DECIMAL(2,1))
                ) AS average_rating,

                COUNT(r.id) AS review_count

            FROM bookings b

            INNER JOIN reviews r
                ON r.booking_id = b.id

            GROUP BY
                b.tutor_id

        ) AS review_stats
            ON review_stats.tutor_id = u.id

        WHERE u.role = 'TUTOR'
          AND u.deleted_at IS NULL

        GROUP BY
            u.id,
            u.full_name,
            u.email,
            u.bio,
            review_stats.average_rating,
            review_stats.review_count

        ORDER BY
            u.full_name ASC
    `;


    db.query(sql, (err, results) => {

        if (err) {

            console.error(
                "❌ /api/tutors SQL ERROR:",
                err
            );

            return res.status(500).json({
                error: "Adatbázis hiba"
            });
        }


        console.log(
            "✅ Tutorok lekérve:",
            results.length
        );


        // Az e-mail cím személyes adat: csak bejelentkezett felhasználó
        // láthatja. A vendégeknek üresen megy ki.
        if (!req.session.user) {

            results.forEach((tutor) => {
                tutor.email = null;
            });
        }


        res.json(results);

    });

});


app.get("/api/tutors/:id/reviews", (req, res) => {

    const tutorId = Number(req.params.id);

    console.log("⭐ REVIEWS REQUEST");
    console.log("Tutor ID:", tutorId);


    if (!Number.isInteger(tutorId)) {

        return res.status(400).json({
            error: "Érvénytelen oktató ID."
        });

    }


    const sql = `
        SELECT
            r.id,
            r.rating,
            r.comment_ AS comment,
            r.crated_at AS created_at,
            u.full_name AS reviewer_name

        FROM reviews r

        INNER JOIN bookings b
            ON b.id = r.booking_id

        LEFT JOIN users u
            ON u.id = b.student_id

        WHERE b.tutor_id = ?

        ORDER BY r.crated_at DESC
    `;


    console.log("📋 Review SQL futtatása...");


    db.query(
        sql,
        [tutorId],
        (err, results) => {

            if (err) {

                console.error(
                    "❌ REVIEW SQL ERROR:"
                );

                console.error(err);

                return res.status(500).json({
                    error: "Adatbázis hiba.",
                    details: err.message
                });

            }


            console.log(
                "✅ Review-k lekérve:",
                results
            );


            return res.json(results);

        }
    );

});

//stats

app.get("/api/stats", (req, res) => {

    const sql = `
        SELECT
            (SELECT COUNT(*) FROM users WHERE role = 'TUTOR' AND deleted_at IS NULL) AS tutor_count,
            (SELECT COUNT(*) FROM bookings) AS booking_count,
            (SELECT COALESCE(AVG(CAST(rating AS DECIMAL(2,1))), 0) FROM reviews) AS avg_rating
    `;

    db.query(sql, (err, results) => {

        if (err) {
            console.error("❌ /api/stats SQL ERROR:", err);
            return res.status(500).json({ error: "Adatbázis hiba" });
        }

        const row = results[0];

        res.json({
            tutors: row.tutor_count,
            bookings: row.booking_count,

            satisfaction: Math.round((Number(row.avg_rating) / 5) * 100)
        });
    });
});


// ============================================================
// CONVERSATIONS API
// ============================================================
app.get("/api/conversations", (req, res) => {

    console.log("💬 GET /api/conversations");

    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    // A bejelentkezett felhasználó ID-ja
    const userId = req.session.user.id;

    const sql = `
        SELECT
            conversations.id,
            conversations.student_id,
            conversations.tutor_id,
            conversations.created_at,

            CASE
                WHEN conversations.student_id = ?
                    THEN tutor.full_name
                ELSE student.full_name
            END AS other_user_name,

            (
                SELECT messages.content
                FROM messages
                WHERE messages.conversation_id = conversations.id
                ORDER BY messages.created_at DESC, messages.id DESC
                LIMIT 1
            ) AS last_message,

            (
                SELECT messages.created_at
                FROM messages
                WHERE messages.conversation_id = conversations.id
                ORDER BY messages.created_at DESC, messages.id DESC
                LIMIT 1
            ) AS last_message_time,

            (
                SELECT messages.deleted_at IS NOT NULL
                FROM messages
                WHERE messages.conversation_id = conversations.id
                ORDER BY messages.created_at DESC, messages.id DESC
                LIMIT 1
            ) AS last_message_retracted,

            (
                SELECT COUNT(*)
                FROM messages
                WHERE messages.conversation_id = conversations.id
                  AND messages.sender_id <> ?
                  AND COALESCE(messages.is_read, 0) = 0
                  AND messages.deleted_at IS NULL
            ) AS unread_count

        FROM conversations

        JOIN users AS student
            ON conversations.student_id = student.id

        JOIN users AS tutor
            ON conversations.tutor_id = tutor.id

        WHERE conversations.student_id = ?
           OR (
               conversations.tutor_id = ?
               AND EXISTS (
                   SELECT 1
                   FROM messages
                   WHERE messages.conversation_id = conversations.id
               )
           )
    `;

    db.query(
        sql,
        [userId, userId, userId, userId],
        (err, results) => {

            if (err) {

                console.error("❌ SQL hiba:", err);

                return res.status(500).json({
                    error: "Adatbázis hiba"
                });
            }

            console.log(
                "✅ Beszélgetések lekérve:",
                results.length
            );

            res.json(results);
        }
    );
});
// ============================================================
// START CONVERSATION API
// ============================================================

// Egy beszélgetés lekérése úgy, ahogy a GET /api/conversations adja
function getConversationForUser(conversationId, userId, callback) {

    const sql = `
        SELECT
            conversations.id,
            conversations.student_id,
            conversations.tutor_id,
            conversations.created_at,

            CASE
                WHEN conversations.student_id = ?
                    THEN tutor.full_name
                ELSE student.full_name
            END AS other_user_name,

            (
                SELECT messages.content
                FROM messages
                WHERE messages.conversation_id = conversations.id
                ORDER BY messages.created_at DESC, messages.id DESC
                LIMIT 1
            ) AS last_message,

            (
                SELECT messages.created_at
                FROM messages
                WHERE messages.conversation_id = conversations.id
                ORDER BY messages.created_at DESC, messages.id DESC
                LIMIT 1
            ) AS last_message_time,

            (
                SELECT messages.deleted_at IS NOT NULL
                FROM messages
                WHERE messages.conversation_id = conversations.id
                ORDER BY messages.created_at DESC, messages.id DESC
                LIMIT 1
            ) AS last_message_retracted,

            (
                SELECT COUNT(*)
                FROM messages
                WHERE messages.conversation_id = conversations.id
                  AND messages.sender_id <> ?
                  AND COALESCE(messages.is_read, 0) = 0
                  AND messages.deleted_at IS NULL
            ) AS unread_count

        FROM conversations

        JOIN users AS student
            ON conversations.student_id = student.id

        JOIN users AS tutor
            ON conversations.tutor_id = tutor.id

        WHERE conversations.id = ?
    `;

    db.query(sql, [userId, userId, conversationId], (err, rows) => {
        callback(err, rows && rows[0]);
    });
}
app.post("/api/conversations", (req, res) => {

    console.log("💬 POST /api/conversations");

    // 1. Be van jelentkezve?
    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    // 2. Új beszélgetést csak diák indíthat
    if (req.session.user.role !== "STUDENT") {
        return res.status(403).json({
            error: "Csak diák indíthat új beszélgetést"
        });
    }

    const studentId = req.session.user.id;
    const tutorId = Number(req.body.tutor_id);

    if (!Number.isInteger(tutorId) || tutorId <= 0) {
        return res.status(400).json({ error: "Érvénytelen oktató" });
    }

    // 3. Létezik ez az oktató?
    db.query(
        "SELECT id FROM users WHERE id = ? AND role = 'TUTOR' AND deleted_at IS NULL",
        [tutorId],
        (err, tutors) => {

            if (err) {
                console.error("❌ SQL hiba (oktató):", err);
                return res.status(500).json({ error: "Adatbázis hiba" });
            }

            if (tutors.length === 0) {
                return res.status(404).json({ error: "Az oktató nem található" });
            }

            // 4. Van már beszélgetésük?
            db.query(
                "SELECT id FROM conversations WHERE student_id = ? AND tutor_id = ?",
                [studentId, tutorId],
                (err, existing) => {

                    if (err) {
                        console.error("❌ SQL hiba (meglévő):", err);
                        return res.status(500).json({ error: "Adatbázis hiba" });
                    }

                    // Ha igen, azt adjuk vissza (nem hozunk létre újat)
                    if (existing.length > 0) {

                        return getConversationForUser(
                            existing[0].id,
                            studentId,
                            (err, conversation) => {

                                if (err || !conversation) {
                                    console.error("❌ SQL hiba (lekérés):", err);
                                    return res.status(500).json({ error: "Adatbázis hiba" });
                                }

                                res.json(conversation);
                            }
                        );
                    }

                    // 5. Ha nem, létrehozzuk
                    db.query(
                        "INSERT INTO conversations (student_id, tutor_id) VALUES (?, ?)",
                        [studentId, tutorId],
                        (err, result) => {

                            if (err) {
                                console.error("❌ SQL hiba (INSERT):", err);
                                return res.status(500).json({ error: "Adatbázis hiba" });
                            }

                            console.log("✅ Új beszélgetés:", result.insertId);

                            getConversationForUser(
                                result.insertId,
                                studentId,
                                (err, conversation) => {

                                    if (err || !conversation) {
                                        console.error("❌ SQL hiba (lekérés):", err);
                                        return res.status(500).json({ error: "Adatbázis hiba" });
                                    }

                                    res.status(201).json(conversation);
                                }
                            );
                        }
                    );
                }
            );
        }
    );
});

// ============================================================
// MARK AS READ API
// ============================================================

app.post("/api/conversations/:id/read", (req, res) => {

    console.log("👁️ POST /api/conversations/:id/read");

    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    const userId = req.session.user.id;
    const conversationId = req.params.id;

    // Csak a beszélgetés résztvevője jelölhet olvasottnak
    const checkSql = `
        SELECT id FROM conversations
        WHERE id = ?
          AND (student_id = ? OR tutor_id = ?)
    `;

    db.query(checkSql, [conversationId, userId, userId], (err, rows) => {

        if (err) {
            console.error("❌ SQL hiba (ellenőrzés):", err);
            return res.status(500).json({ error: "Adatbázis hiba" });
        }

        if (rows.length === 0) {
            return res.status(403).json({
                error: "Nem vagy résztvevője ennek a beszélgetésnek"
            });
        }

        // Csak a MÁSIK fél üzenetei számítanak olvasatlannak
        const updateSql = `
            UPDATE messages
            SET is_read = 1
            WHERE conversation_id = ?
              AND sender_id <> ?
              AND COALESCE(is_read, 0) = 0
        `;

        db.query(updateSql, [conversationId, userId], (err, result) => {

            if (err) {
                console.error("❌ SQL hiba (UPDATE):", err);
                return res.status(500).json({ error: "Adatbázis hiba" });
            }

            console.log("✅ Olvasottnak jelölve:", result.affectedRows);

            res.json({ updated: result.affectedRows });
        });
    });
});

// ============================================================
// ATTACHMENTS API
// ============================================================

// Feltöltött fájlok helye (a böngészőből közvetlenül nem érhető el)
//
// A fájlok a nyilvánosan kiszolgált vsc mappán KÍVÜL vannak
// (projekt/uploads/chat). Így a böngésző nem érheti el őket közvetlenül,
// csak a /api/messages/:id/attachment végponton keresztül, amely
// ellenőrzi, hogy a kérő tagja-e a beszélgetésnek.
// Az UPLOAD_DIR környezeti változóval át lehet írni.
const CHAT_UPLOAD_DIR = process.env.UPLOAD_DIR
    ? path.resolve(process.env.UPLOAD_DIR)
    : path.join(__dirname, "..", "..", "uploads", "chat");

// A régi hely (vsc/js/uploads/chat) a nyilvános /js mappában volt
const LEGACY_UPLOAD_DIR = path.join(__dirname, "uploads", "chat");

fs.mkdirSync(CHAT_UPLOAD_DIR, { recursive: true });

// A korábban a régi helyre feltöltött fájlok átköltöztetése az újra
// (egyszeri; ha nincs régi mappa, nem csinál semmit)
function migrateLegacyUploads() {

    if (!fs.existsSync(LEGACY_UPLOAD_DIR)) {
        return;
    }

    let moved = 0;

    for (const name of fs.readdirSync(LEGACY_UPLOAD_DIR)) {

        const from = path.join(LEGACY_UPLOAD_DIR, name);
        const to = path.join(CHAT_UPLOAD_DIR, name);

        try {

            if (!fs.statSync(from).isFile() || fs.existsSync(to)) {
                continue;
            }

            try {
                fs.renameSync(from, to);
            } catch (renameError) {
                // Másik meghajtóra nem lehet átnevezni: másolás + törlés
                fs.copyFileSync(from, to);
                fs.unlinkSync(from);
            }

            moved++;

        } catch (error) {
            console.error("❌ Feltöltött fájl átköltöztetése sikertelen:", name, error.message);
        }
    }

    // A régi mappák törlése, ha kiürültek
    try {
        fs.rmdirSync(LEGACY_UPLOAD_DIR);
        fs.rmdirSync(path.dirname(LEGACY_UPLOAD_DIR));
    } catch (error) {
        // nem üres, vagy már nincs: nem baj
    }

    if (moved > 0) {
        console.log(`📦 ${moved} feltöltött fájl átköltöztetve a nyilvános mappából`);
    }
}

migrateLegacyUploads();

const MAX_ATTACHMENT_SIZE = 10 * 1024 * 1024; // 10 MB

// Engedélyezett kiterjesztések és a hozzájuk tartozó fájltípus.
// A böngésző által küldött típust szándékosan nem használjuk.
const ALLOWED_ATTACHMENTS = {
    jpg: "image/jpeg",
    jpeg: "image/jpeg",
    png: "image/png",
    gif: "image/gif",
    webp: "image/webp",
    pdf: "application/pdf",
    doc: "application/msword",
    docx: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    xls: "application/vnd.ms-excel",
    xlsx: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    ppt: "application/vnd.ms-powerpoint",
    pptx: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    txt: "text/plain; charset=utf-8"
};

function getFileExtension(fileName) {
    return path.extname(String(fileName || "")).slice(1).toLowerCase();
}

function isAllowedExtension(extension) {
    return Object.prototype.hasOwnProperty.call(ALLOWED_ATTACHMENTS, extension);
}

// A multer a fájlnevet latin1-ként olvassa, ezt javítjuk (ő, ű stb.),
// és eltávolítjuk a veszélyes karaktereket.
function cleanFileName(name) {

    let fixed = Buffer.from(name, "latin1").toString("utf8");

    if (fixed.includes("\uFFFD")) {
        fixed = name;
    }

    fixed = path.basename(fixed.replace(/\\/g, "/"));
    fixed = fixed.replace(/[<>:"|?*\u0000-\u001f]/g, "_").trim();

    return (fixed || "fajl").slice(0, 200);
}

const uploadChatFile = multer({
    storage: multer.diskStorage({
        destination: (req, file, cb) => cb(null, CHAT_UPLOAD_DIR),
        filename: (req, file, cb) => {
            const extension = getFileExtension(file.originalname);
            cb(null, crypto.randomBytes(16).toString("hex") + "." + extension);
        }
    }),
    limits: {
        fileSize: MAX_ATTACHMENT_SIZE,
        files: 1
    },
    fileFilter: (req, file, cb) => {
        if (!isAllowedExtension(getFileExtension(file.originalname))) {
            return cb(new Error("UNSUPPORTED_FILE_TYPE"));
        }
        cb(null, true);
    }
}).single("file");


// ------------------------------------------------------------
// POST: üzenet csatolmánnyal
// ------------------------------------------------------------

app.post("/api/conversations/:id/attachments", (req, res) => {

    console.log("📎 POST /api/conversations/:id/attachments");

    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    const userId = req.session.user.id;
    const conversationId = req.params.id;

    uploadChatFile(req, res, (uploadError) => {

        const file = req.file;

        // Hiba esetén a már lemezre került fájlt töröljük
        const discard = () => {
            if (file) {
                fs.unlink(file.path, () => {});
            }
        };

        if (uploadError) {

            discard();

            if (uploadError.code === "LIMIT_FILE_SIZE") {
                return res.status(413).json({
                    error: "A fájl túl nagy (legfeljebb 10 MB)."
                });
            }

            if (uploadError.message === "UNSUPPORTED_FILE_TYPE") {
                return res.status(400).json({
                    error: "Ez a fájltípus nem engedélyezett."
                });
            }

            console.error("❌ Feltöltési hiba:", uploadError);

            return res.status(400).json({ error: "A feltöltés nem sikerült." });
        }

        if (!file) {
            return res.status(400).json({ error: "Nincs kiválasztott fájl." });
        }

        // Résztvevője-e a bejelentkezett user ennek a beszélgetésnek?
        const checkSql = `
            SELECT id, student_id, tutor_id,
                   (SELECT COUNT(*) FROM users
                    WHERE id IN (conversations.student_id, conversations.tutor_id)
                      AND deleted_at IS NOT NULL) AS deleted_count
            FROM conversations
            WHERE id = ?
              AND (student_id = ? OR tutor_id = ?)
        `;

        db.query(checkSql, [conversationId, userId, userId], (err, rows) => {

            if (err) {
                discard();
                console.error("❌ SQL hiba (ellenőrzés):", err);
                return res.status(500).json({ error: "Adatbázis hiba" });
            }

            if (rows.length === 0) {
                discard();
                return res.status(403).json({
                    error: "Nem vagy résztvevője ennek a beszélgetésnek"
                });
            }

            // A másik fél törölte a fiókját: neki már nem lehet küldeni
            if (Number(rows[0].deleted_count) > 0) {
                discard();
                return res.status(403).json({
                    error: "A beszélgetés másik tagja törölte a fiókját."
                });
            }

            const originalName = cleanFileName(file.originalname);
            const extension = getFileExtension(file.originalname);
            const fileType = ALLOWED_ATTACHMENTS[extension];

            // Ha nincs szöveg, a fájl neve lesz az üzenet szövege
            const caption = String(req.body.content || "").trim().slice(0, 2000);
            const content = caption || originalName;

            const insertSql = `
                INSERT INTO messages
                    (conversation_id, sender_id, content,
                     attachment_name, attachment_path, attachment_type, attachment_size)
                VALUES (?, ?, ?, ?, ?, ?, ?)
            `;

            db.query(
                insertSql,
                [conversationId, userId, content, originalName, file.filename, fileType, file.size],
                (err, result) => {

                    if (err) {
                        discard();
                        console.error("❌ SQL hiba (INSERT):", err);
                        return res.status(500).json({ error: "Adatbázis hiba" });
                    }

                    // A szerveren lévő fájl útvonalát nem adjuk ki
                    const selectSql = `
                        SELECT id, conversation_id, sender_id, content, is_read,
                               created_at, attachment_name, attachment_type, attachment_size
                        FROM messages
                        WHERE id = ?
                    `;

                    db.query(selectSql, [result.insertId], (err, saved) => {

                        if (err) {
                            console.error("❌ SQL hiba (SELECT):", err);
                            return res.status(500).json({ error: "Adatbázis hiba" });
                        }

                        const message = saved[0];
                        const conversation = rows[0];

                        const recipientId =
                            Number(conversation.student_id) === Number(userId)
                                ? conversation.tutor_id
                                : conversation.student_id;

                        console.log("✅ Csatolmány elmentve:", result.insertId);

                        // Valós idejű értesítés a másik félnek
                        sendToUser(recipientId, { type: "new_message", message });

                        res.status(201).json(message);
                    });
                }
            );
        });
    });
});


// ------------------------------------------------------------
// GET: csatolmány letöltése / megjelenítése
// ------------------------------------------------------------

app.get("/api/messages/:id/attachment", (req, res) => {

    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    const userId = req.session.user.id;

    const sql = `
        SELECT
            messages.attachment_name,
            messages.attachment_path,
            messages.attachment_type

        FROM messages

        JOIN conversations
            ON messages.conversation_id = conversations.id

        WHERE messages.id = ?
          AND messages.attachment_path IS NOT NULL
          AND (
              conversations.student_id = ?
              OR conversations.tutor_id = ?
          )
    `;

    db.query(sql, [req.params.id, userId, userId], (err, rows) => {

        if (err) {
            console.error("❌ SQL hiba:", err);
            return res.status(500).json({ error: "Adatbázis hiba" });
        }

        if (rows.length === 0) {
            return res.status(404).json({ error: "A fájl nem található" });
        }

        const row = rows[0];

        const filePath = path.join(
            CHAT_UPLOAD_DIR,
            path.basename(row.attachment_path)
        );

        // A képeket megjelenítjük, minden mást letöltésként adunk át
        const disposition = row.attachment_type.startsWith("image/")
            ? "inline"
            : "attachment";

        const encodedName = encodeURIComponent(row.attachment_name)
            .replace(/['()*]/g, (c) => "%" + c.charCodeAt(0).toString(16).toUpperCase());

        res.setHeader("X-Content-Type-Options", "nosniff");
        res.setHeader("Content-Type", row.attachment_type);
        res.setHeader(
            "Content-Disposition",
            `${disposition}; filename*=UTF-8''${encodedName}`
        );

        res.sendFile(filePath, (err) => {
            if (err && !res.headersSent) {
                res.status(404).json({ error: "A fájl nem található" });
            }
        });
    });
});




// ============================================================
// MESSAGES API
// ============================================================

// ------------------------------------------------------------
// GET: egy beszélgetés üzenetei
// ------------------------------------------------------------

app.get("/api/conversations/:id/messages", (req, res) => {

    console.log("💬 GET /api/conversations/:id/messages");

    // Be van jelentkezve?
    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    const userId = req.session.user.id;
    const conversationId = req.params.id;

    const sql = `
        SELECT
            messages.id,
            messages.conversation_id,
            messages.sender_id,
            messages.content,
            messages.is_read,
            messages.created_at,
            messages.attachment_name,
            messages.attachment_type,
            messages.attachment_size,
            messages.deleted_at,
            messages.edited_at

        FROM messages

        JOIN conversations
            ON messages.conversation_id = conversations.id

        WHERE messages.conversation_id = ?
          AND (
              conversations.student_id = ?
              OR conversations.tutor_id = ?
          )

        ORDER BY messages.created_at ASC, messages.id ASC
    `;

    db.query(sql, [conversationId, userId, userId], (err, results) => {

        if (err) {
            console.error("❌ SQL hiba:", err);
            return res.status(500).json({ error: "Adatbázis hiba" });
        }

        console.log("✅ Üzenetek lekérve:", results.length);

        res.json(results);
    });
});


// ------------------------------------------------------------
// POST: új üzenet küldése
// ------------------------------------------------------------

app.post("/api/conversations/:id/messages", (req, res) => {

    console.log("📨 POST /api/conversations/:id/messages");

    // 1. Be van jelentkezve?
    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    const userId = req.session.user.id;
    const conversationId = req.params.id;

    // 2. Az üzenet szövege a kérés törzséből jön
    const content = (req.body.content || "").trim();

    if (!content) {
        return res.status(400).json({ error: "Az üzenet nem lehet üres" });
    }

    if (content.length > 2000) {
        return res.status(400).json({ error: "Az üzenet túl hosszú" });
    }

    // 3. Résztvevője-e a bejelentkezett user ennek a beszélgetésnek?
    const checkSql = `
        SELECT id, student_id, tutor_id,
               (SELECT COUNT(*) FROM users
                WHERE id IN (conversations.student_id, conversations.tutor_id)
                  AND deleted_at IS NOT NULL) AS deleted_count
        FROM conversations
        WHERE id = ?
          AND (student_id = ? OR tutor_id = ?)
    `;

    db.query(checkSql, [conversationId, userId, userId], (err, rows) => {

        if (err) {
            console.error("❌ SQL hiba (ellenőrzés):", err);
            return res.status(500).json({ error: "Adatbázis hiba" });
        }

        if (rows.length === 0) {
            return res.status(403).json({
                error: "Nem vagy résztvevője ennek a beszélgetésnek"
            });
        }

        // A másik fél törölte a fiókját: neki már nem lehet írni
        if (Number(rows[0].deleted_count) > 0) {
            return res.status(403).json({
                error: "A beszélgetés másik tagja törölte a fiókját."
            });
        }

        // 4. Mentés. A sender_id MINDIG a sessionből jön,
        //    sosem a böngészőből.
        const insertSql = `
            INSERT INTO messages (conversation_id, sender_id, content)
            VALUES (?, ?, ?)
        `;

        db.query(insertSql, [conversationId, userId, content], (err, result) => {

            if (err) {
                console.error("❌ SQL hiba (INSERT):", err);
                return res.status(500).json({ error: "Adatbázis hiba" });
            }

            // 5. Visszaadjuk a frissen mentett üzenetet
            db.query(
                "SELECT * FROM messages WHERE id = ?",
                [result.insertId],
                (err, saved) => {

                    if (err) {
                        console.error("❌ SQL hiba (SELECT):", err);
                        return res.status(500).json({ error: "Adatbázis hiba" });
                    }

                    const message = saved[0];
                    const conversation = rows[0];

                    // A beszélgetés másik résztvevője
                    const recipientId =
                        Number(conversation.student_id) === Number(userId)
                            ? conversation.tutor_id
                            : conversation.student_id;

                    console.log("✅ Üzenet elmentve:", result.insertId);

                    // Valós idejű értesítés a másik félnek
                    sendToUser(recipientId, { type: "new_message", message });

                    res.status(201).json(message);
                }
            );
        });
    });
});
// ============================================================
// RETRACT MESSAGE API (üzenet visszavonása)
// ============================================================
//
// A visszavont üzenet sora megmarad (deleted_at kapja az időt), de a
// szövege és a csatolmánya törlődik, így tartalma sehol nem érhető el.
// A beszélgetésben "Az üzenet vissza lett vonva" felirat marad.

// A hiányzó oszlopokat a szerver indulásakor létrehozza (egyszeri
// migráció), így az adatbázist nem kell kézzel módosítani.
//   deleted_at - mikor vonták vissza az üzenetet
//   edited_at  - mikor szerkesztették utoljára az üzenetet
// Diák profil és fióktörlés: a hiányzó oszlopok és a tábla létrehozása
// induláskor (egyszeri migráció), a törölt fiókok betöltése.
//   users.school_level  - iskolai szint (pl. "Középiskola")
//   users.grade         - osztály / évfolyam (szabad szöveg)
//   users.deleted_at    - mikor törölték (anonimizálták) a fiókot
//   student_subjects    - miből kér segítséget a diák
async function ensureProfileSchema() {

    try {

        const pool = db.promise();

        const columns = [
            ["school_level", "VARCHAR(30) NULL DEFAULT NULL"],
            ["grade", "VARCHAR(30) NULL DEFAULT NULL"],
            ["deleted_at", "TIMESTAMP NULL DEFAULT NULL"]
        ];

        for (const [column, definition] of columns) {

            const [found] = await pool.query(
                "SHOW COLUMNS FROM users LIKE ?",
                [column]
            );

            if (found.length === 0) {

                // Az oszlopnév fix listából jön, nem a felhasználótól
                await pool.query(
                    `ALTER TABLE users ADD COLUMN ${column} ${definition}`
                );

                console.log(`✅ users.${column} oszlop létrehozva`);
            }
        }

        await pool.query(`
            CREATE TABLE IF NOT EXISTS student_subjects (
                student_id INT NOT NULL,
                subject_id INT NOT NULL,
                PRIMARY KEY (student_id, subject_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        `);

        const [deleted] = await pool.query(
            "SELECT id FROM users WHERE deleted_at IS NOT NULL"
        );

        deleted.forEach((row) => deletedUserIds.add(Number(row.id)));

    } catch (err) {

        console.error("❌ SQL hiba (profil séma):", err);
    }
}


// Foglalás: a hiányzó oszlopok és a lemondás státusza.
//   bookings.status_  - új érték: CANCELLED (lemondta valamelyik fél)
//   bookings.price    - a foglaláskor érvényes ár (Ft)
//   bookings.note     - a diák üzenete az oktatónak
async function ensureBookingSchema() {

    try {

        const pool = db.promise();

        const [status] = await pool.query(
            `SELECT COLUMN_TYPE AS type
             FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE()
               AND TABLE_NAME = 'bookings'
               AND COLUMN_NAME = 'status_'`
        );

        if (status.length > 0 && !String(status[0].type).includes("CANCELLED")) {

            await pool.query(
                `ALTER TABLE bookings MODIFY status_
                 ENUM('PENDING','CONFIRMED','REJECTED','COMPLETED','CANCELLED')
                 DEFAULT NULL`
            );

            console.log("✅ bookings.status_ bővítve: CANCELLED");
        }

        const columns = [
            ["price", "INT UNSIGNED NULL DEFAULT NULL"],
            ["note", "VARCHAR(300) NULL DEFAULT NULL"]
        ];

        for (const [column, definition] of columns) {

            const [found] = await pool.query(
                "SHOW COLUMNS FROM bookings LIKE ?",
                [column]
            );

            if (found.length === 0) {

                // Az oszlopnév fix listából jön, nem a felhasználótól
                await pool.query(
                    `ALTER TABLE bookings ADD COLUMN ${column} ${definition}`
                );

                console.log(`✅ bookings.${column} oszlop létrehozva`);
            }
        }

        // Egy foglaláshoz legfeljebb egy értékelés (ha az adatbázisban már van
        // egyedi index a booking_id-n, nem hozunk létre újat). Ha a régi adatokban
        // már van duplikált értékelés, az index nem hozható létre: ilyenkor
        // csak figyelmeztetünk, a szerver ettől még ellenőrzi a duplikációt.
        const [index] = await pool.query(
            "SHOW INDEX FROM reviews WHERE Column_name = 'booking_id' AND Non_unique = 0"
        );

        if (index.length === 0) {

            try {

                await pool.query(
                    "ALTER TABLE reviews ADD UNIQUE KEY uq_reviews_booking (booking_id)"
                );

                console.log("✅ reviews: egyedi index (foglalásonként 1 értékelés)");

            } catch (indexError) {

                console.warn(
                    "⚠️ reviews: az egyedi index nem hozható létre " +
                    "(van duplikált értékelés a régi adatokban):",
                    indexError.code
                );
            }
        }

    } catch (err) {

        console.error("❌ SQL hiba (foglalás séma):", err);
    }
}


// A users.email oszlop eredetileg VARCHAR(30) volt, amibe a hosszabb
// valódi e-mail címek nem fértek el. Induláskor VARCHAR(100)-ra bővítjük
// (a UNIQUE kulcs megmarad).
function ensureUserEmailLength() {

    db.query(
        `SELECT CHARACTER_MAXIMUM_LENGTH AS len
         FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA = DATABASE()
           AND TABLE_NAME = 'users'
           AND COLUMN_NAME = 'email'`,
        (err, rows) => {

            if (err) {
                console.error("❌ SQL hiba (email hossz ellenőrzés):", err);
                return;
            }

            if (rows.length === 0 || Number(rows[0].len) >= MAX_EMAIL_LENGTH) {
                return;
            }

            db.query(
                `ALTER TABLE users MODIFY email VARCHAR(${MAX_EMAIL_LENGTH}) NOT NULL`,
                (err) => {

                    if (err) {
                        console.error("❌ SQL hiba (email bővítés):", err);
                        return;
                    }

                    console.log(`✅ users.email VARCHAR(${MAX_EMAIL_LENGTH})-ra bővítve`);
                }
            );
        }
    );
}


function ensureMessageColumns() {

    ["deleted_at", "edited_at"].forEach((column) => {

        db.query(
            "SHOW COLUMNS FROM messages LIKE ?",
            [column],
            (err, rows) => {

                if (err) {
                    console.error(`❌ SQL hiba (${column} ellenőrzés):`, err);
                    return;
                }

                if (rows.length > 0) {
                    return;
                }

                // Az oszlopnév fix listából jön, nem a felhasználótól
                db.query(
                    `ALTER TABLE messages ADD COLUMN ${column} TIMESTAMP NULL DEFAULT NULL`,
                    (err) => {

                        if (err) {
                            console.error(`❌ SQL hiba (${column} létrehozása):`, err);
                            return;
                        }

                        console.log(`✅ messages.${column} oszlop létrehozva`);
                    }
                );
            }
        );
    });
}


app.delete("/api/messages/:id", (req, res) => {

    console.log("↩️ DELETE /api/messages/:id");

    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    const userId = req.session.user.id;
    const messageId = Number(req.params.id);

    if (!Number.isInteger(messageId) || messageId <= 0) {
        return res.status(400).json({ error: "Érvénytelen üzenet" });
    }

    // Csak a SAJÁT, még nem visszavont üzenet vonható vissza
    const selectSql = `
        SELECT
            messages.id,
            messages.conversation_id,
            messages.attachment_path,
            conversations.student_id,
            conversations.tutor_id

        FROM messages

        JOIN conversations
            ON messages.conversation_id = conversations.id

        WHERE messages.id = ?
          AND messages.sender_id = ?
          AND messages.deleted_at IS NULL
    `;

    db.query(selectSql, [messageId, userId], (err, rows) => {

        if (err) {
            console.error("❌ SQL hiba (visszavonás, keresés):", err);
            return res.status(500).json({ error: "Adatbázis hiba" });
        }

        if (rows.length === 0) {
            return res.status(404).json({
                error: "Az üzenet nem található, vagy nem vonható vissza"
            });
        }

        const message = rows[0];

        const updateSql = `
            UPDATE messages
            SET content = '',
                deleted_at = NOW(),
                attachment_name = NULL,
                attachment_path = NULL,
                attachment_type = NULL,
                attachment_size = NULL
            WHERE id = ?
              AND sender_id = ?
        `;

        db.query(updateSql, [messageId, userId], (err) => {

            if (err) {
                console.error("❌ SQL hiba (visszavonás, módosítás):", err);
                return res.status(500).json({ error: "Adatbázis hiba" });
            }

            // A csatolt fájlt a lemezről is töröljük
            if (message.attachment_path) {

                fs.unlink(
                    path.join(
                        CHAT_UPLOAD_DIR,
                        path.basename(message.attachment_path)
                    ),
                    () => {}
                );
            }

            console.log("✅ Üzenet visszavonva:", messageId);

            const recipientId =
                Number(message.student_id) === Number(userId)
                    ? message.tutor_id
                    : message.student_id;

            const payload = {
                type: "message_retracted",
                conversation_id: message.conversation_id,
                message_id: message.id
            };

            // A másik félnek és a küldő többi böngészőfülének is
            sendToUser(recipientId, payload);
            sendToUser(userId, payload);

            res.json({ success: true, message_id: message.id });
        });
    });
});


// ============================================================
// EDIT MESSAGE API (üzenet szerkesztése)
// ============================================================
//
// Csak a saját, még meglévő, szöveges (csatolmány nélküli) üzenet
// szerkeszthető. A módosítás idejét az edited_at tárolja.

app.patch("/api/messages/:id", (req, res) => {

    console.log("✏️ PATCH /api/messages/:id");

    if (!req.session.user) {
        return res.status(401).json({ error: "Nincs bejelentkezve" });
    }

    const userId = req.session.user.id;
    const messageId = Number(req.params.id);

    if (!Number.isInteger(messageId) || messageId <= 0) {
        return res.status(400).json({ error: "Érvénytelen üzenet" });
    }

    const content = String(req.body.content || "").trim();

    if (!content) {
        return res.status(400).json({ error: "Az üzenet nem lehet üres" });
    }

    if (content.length > 2000) {
        return res.status(400).json({ error: "Az üzenet túl hosszú" });
    }

    const selectSql = `
        SELECT
            messages.id,
            messages.conversation_id,
            conversations.student_id,
            conversations.tutor_id

        FROM messages

        JOIN conversations
            ON messages.conversation_id = conversations.id

        WHERE messages.id = ?
          AND messages.sender_id = ?
          AND messages.deleted_at IS NULL
          AND messages.attachment_path IS NULL
    `;

    db.query(selectSql, [messageId, userId], (err, rows) => {

        if (err) {
            console.error("❌ SQL hiba (szerkesztés, keresés):", err);
            return res.status(500).json({ error: "Adatbázis hiba" });
        }

        if (rows.length === 0) {
            return res.status(404).json({
                error: "Az üzenet nem található, vagy nem szerkeszthető"
            });
        }

        const message = rows[0];

        db.query(
            `UPDATE messages
             SET content = ?, edited_at = NOW()
             WHERE id = ? AND sender_id = ?`,
            [content, messageId, userId],
            (err) => {

                if (err) {
                    console.error("❌ SQL hiba (szerkesztés, módosítás):", err);
                    return res.status(500).json({ error: "Adatbázis hiba" });
                }

                console.log("✅ Üzenet szerkesztve:", messageId);

                const recipientId =
                    Number(message.student_id) === Number(userId)
                        ? message.tutor_id
                        : message.student_id;

                const payload = {
                    type: "message_edited",
                    conversation_id: message.conversation_id,
                    message_id: message.id
                };

                // A másik félnek és a küldő többi böngészőfülének is
                sendToUser(recipientId, payload);
                sendToUser(userId, payload);

                res.json({ success: true, message_id: message.id, content });
            }
        );
    });
});


// ============================================================
// WEBSOCKET
// ============================================================

const server = http.createServer(app);
const wss = new WebSocketServer({ noServer: true, maxPayload: 1024 });

// userId -> a user nyitott kapcsolatai (több böngészőfül is lehet)
const clients = new Map();

server.on("upgrade", (req, socket, head) => {

    // A session cookie alapján megnézzük, ki csatlakozik
    sessionMiddleware(req, {}, () => {

        if (
            !req.session ||
            !req.session.user ||
            deletedUserIds.has(Number(req.session.user.id))
        ) {
            socket.write("HTTP/1.1 401 Unauthorized\r\n\r\n");
            socket.destroy();
            return;
        }

        wss.handleUpgrade(req, socket, head, (ws) => {
            ws.userId = Number(req.session.user.id);
            wss.emit("connection", ws);
        });
    });
});

wss.on("connection", (ws) => {

    if (!clients.has(ws.userId)) {
        clients.set(ws.userId, new Set());
    }
    clients.get(ws.userId).add(ws);

    console.log("🔌 WebSocket csatlakozott, user:", ws.userId);

    // "Gépel..." jelzés továbbítása a beszélgetés másik résztvevőjének
    ws.on("message", (raw) => {

        let data;

        try {
            data = JSON.parse(raw);
        } catch (error) {
            return;
        }

        if (!data || data.type !== "typing") {
            return;
        }

        const conversationId = Number(data.conversation_id);

        if (!Number.isInteger(conversationId)) {
            return;
        }

        // Csak a beszélgetés résztvevője küldhet jelzést
        db.query(
            `SELECT student_id, tutor_id FROM conversations
             WHERE id = ? AND (student_id = ? OR tutor_id = ?)`,
            [conversationId, ws.userId, ws.userId],
            (err, rows) => {

                if (err || rows.length === 0) {
                    return;
                }

                const conversation = rows[0];

                const recipientId =
                    Number(conversation.student_id) === ws.userId
                        ? conversation.tutor_id
                        : conversation.student_id;

                sendToUser(recipientId, {
                    type: "typing",
                    conversation_id: conversationId
                });
            }
        );
    });

    ws.on("close", () => {
        const set = clients.get(ws.userId);
        if (set) {
            set.delete(ws);
            if (set.size === 0) clients.delete(ws.userId);
        }
        console.log("🔌 WebSocket lecsatlakozott, user:", ws.userId);
    });
});

// Üzenet küldése egy adott usernek (minden nyitott fülére)
function sendToUser(userId, payload) {
    const set = clients.get(Number(userId));
    if (!set) return;

    const data = JSON.stringify(payload);
    set.forEach((ws) => {
        if (ws.readyState === ws.OPEN) ws.send(data);
    });
}


// ============================================================
// ACCOUNT
// ============================================================
//
// GET /api/account  -> the logged-in user's editable data
//                      (tutors also get their subjects and prices)
// PUT /api/account  -> update name, email, bio, subjects/prices, password
//
// Changing the e-mail or the password requires the CURRENT password,
// so someone who finds a logged-in browser can't take over the account.
//
// ============================================================


const STRONG_PASSWORD = /^(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{6,}$/;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const MAX_TUTOR_SUBJECTS = 10;
const MAX_HOURLY_RATE = 100000;

// A users.email oszlop hossza (lásd ensureUserEmailLength)
const MAX_EMAIL_LENGTH = 100;

// Diák profil
const SCHOOL_LEVELS = [
    "Általános iskola",
    "Középiskola",
    "Egyetem / főiskola",
    "Felnőtt / egyéb"
];
const MAX_STUDENT_SUBJECTS = 10;
const MAX_GRADE_LENGTH = 30;


app.get("/api/account", async (req, res) => {

    if (!req.session.user) {
        return res.status(401).json({ error: "Nem vagy bejelentkezve." });
    }

    try {

        const [rows] = await db.promise().query(
            `SELECT id, full_name, email, role, bio, school_level, grade
             FROM users
             WHERE id = ?
             LIMIT 1`,
            [req.session.user.id]
        );

        if (rows.length === 0) {
            return res.status(404).json({ error: "A felhasználó nem található." });
        }

        const u = rows[0];

        const account = {
            id: u.id,
            name: u.full_name,
            email: u.email,
            role: u.role,
            bio: u.bio || ""
        };

        // A tutor's subjects and prices
        if (u.role === "TUTOR") {

            const [subjects] = await db.promise().query(
                `SELECT ts.subject_id, s.name, ts.hourly_rate
                 FROM tutor_subjects ts
                 INNER JOIN subjects s ON s.id = ts.subject_id
                 WHERE ts.tutor_id = ?
                 ORDER BY s.name`,
                [u.id]
            );

            account.subjects = subjects;
        }

        // A diák tanulmányai
        if (u.role === "STUDENT") {

            const [learning] = await db.promise().query(
                `SELECT ss.subject_id, s.name
                 FROM student_subjects ss
                 INNER JOIN subjects s ON s.id = ss.subject_id
                 WHERE ss.student_id = ?
                 ORDER BY s.name`,
                [u.id]
            );

            account.school_level = u.school_level || "";
            account.grade = u.grade || "";
            account.learning_subjects = learning;
        }

        res.json(account);

    } catch (err) {
        console.error("❌ GET /api/account error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


app.put("/api/account", async (req, res) => {

    if (!req.session.user) {
        return res.status(401).json({ error: "Nem vagy bejelentkezve." });
    }

    const userId = req.session.user.id;

    const name = String(req.body.name || "").trim();
    const email = String(req.body.email || "").trim();
    const bio = String(req.body.bio ?? "").trim();
    const currentPassword = String(req.body.currentPassword || "");
    const newPassword = String(req.body.newPassword || "");


    // ---------------- validation ----------------

    if (name.length < 2 || name.length > 60) {
        return res.status(400).json({ error: "A név 2 és 60 karakter között legyen." });
    }

    if (!EMAIL_PATTERN.test(email) || email.length > MAX_EMAIL_LENGTH) {
        return res.status(400).json({
            error: "Adj meg egy érvényes e-mail címet (legfeljebb " + MAX_EMAIL_LENGTH + " karakter)."
        });
    }

    if (newPassword && !STRONG_PASSWORD.test(newPassword)) {
        return res.status(400).json({
            error: "Az új jelszó legalább 6 karakter legyen, és tartalmazzon számot és speciális karaktert."
        });
    }


    try {

        // The current row (we need the hash, the current e-mail and the role)
        const [rows] = await db.promise().query(
            "SELECT email, role, password_hash FROM users WHERE id = ? LIMIT 1",
            [userId]
        );

        if (rows.length === 0) {
            return res.status(404).json({ error: "A felhasználó nem található." });
        }

        const current = rows[0];
        const isTutor = current.role === "TUTOR";
        const isStudent = current.role === "STUDENT";


        // ---------------- tutor only: bio + subjects ----------------

        let subjectRows = null;     // null = "don't touch the subjects"

        if (isTutor) {

            if (bio.length > 1000) {
                return res.status(400).json({ error: "A bemutatkozás legfeljebb 1000 karakter lehet." });
            }

            if (Array.isArray(req.body.subjects)) {

                if (req.body.subjects.length > MAX_TUTOR_SUBJECTS) {
                    return res.status(400).json({
                        error: "Legfeljebb " + MAX_TUTOR_SUBJECTS + " tantárgyat adhatsz meg."
                    });
                }

                const seen = new Set();
                subjectRows = [];

                for (const item of req.body.subjects) {

                    const subjectId = Number(item && item.subject_id);
                    const rate = Number(item && item.hourly_rate);

                    if (!Number.isInteger(subjectId) || subjectId <= 0) {
                        return res.status(400).json({ error: "Érvénytelen tantárgy." });
                    }

                    if (seen.has(subjectId)) {
                        return res.status(400).json({ error: "Egy tantárgy csak egyszer szerepelhet." });
                    }

                    if (!Number.isInteger(rate) || rate < 0 || rate > MAX_HOURLY_RATE) {
                        return res.status(400).json({
                            error: "Az óradíj 0 és 100 000 Ft között legyen."
                        });
                    }

                    seen.add(subjectId);
                    subjectRows.push([userId, subjectId, rate]);
                }

                // every chosen subject must really exist
                if (seen.size > 0) {

                    const [found] = await db.promise().query(
                        "SELECT id FROM subjects WHERE id IN (?)",
                        [[...seen]]
                    );

                    if (found.length !== seen.size) {
                        return res.status(400).json({ error: "Ismeretlen tantárgy." });
                    }
                }
            }
        }


        // ---------------- student only: level, grade, learning subjects ----------------

        let schoolLevel;            // undefined = "don't touch"
        let grade;
        let learningRows = null;    // null = "don't touch the subjects"

        if (isStudent) {

            if (req.body.school_level !== undefined) {

                schoolLevel = String(req.body.school_level || "").trim();

                if (schoolLevel && !SCHOOL_LEVELS.includes(schoolLevel)) {
                    return res.status(400).json({ error: "Érvénytelen iskolai szint." });
                }
            }

            if (req.body.grade !== undefined) {

                grade = String(req.body.grade || "").trim();

                if (grade.length > MAX_GRADE_LENGTH) {
                    return res.status(400).json({
                        error: "Az osztály legfeljebb " + MAX_GRADE_LENGTH + " karakter lehet."
                    });
                }
            }

            if (Array.isArray(req.body.learning_subjects)) {

                if (req.body.learning_subjects.length > MAX_STUDENT_SUBJECTS) {
                    return res.status(400).json({
                        error: "Legfeljebb " + MAX_STUDENT_SUBJECTS + " tantárgyat adhatsz meg."
                    });
                }

                const seen = new Set();
                learningRows = [];

                for (const item of req.body.learning_subjects) {

                    const subjectId = Number(item);

                    if (!Number.isInteger(subjectId) || subjectId <= 0) {
                        return res.status(400).json({ error: "Érvénytelen tantárgy." });
                    }

                    if (seen.has(subjectId)) {
                        return res.status(400).json({ error: "Egy tantárgy csak egyszer szerepelhet." });
                    }

                    seen.add(subjectId);
                    learningRows.push([userId, subjectId]);
                }

                if (seen.size > 0) {

                    const [found] = await db.promise().query(
                        "SELECT id FROM subjects WHERE id IN (?)",
                        [[...seen]]
                    );

                    if (found.length !== seen.size) {
                        return res.status(400).json({ error: "Ismeretlen tantárgy." });
                    }
                }
            }
        }


        // ---------------- e-mail / password change needs the current password ----------------

        const emailChanged = email.toLowerCase() !== current.email.toLowerCase();

        if (emailChanged || newPassword) {

            if (!currentPassword) {
                return res.status(400).json({
                    error: "E-mail vagy jelszó módosításához add meg a jelenlegi jelszavad."
                });
            }

            const ok = await verifyPassword(current.password_hash, currentPassword);

            if (!ok) {
                return res.status(403).json({ error: "A jelenlegi jelszó hibás." });
            }
        }


        // ---------------- build the UPDATE ----------------

        const fields = ["full_name = ?", "email = ?"];
        const values = [name, email];

        if (isTutor) {
            fields.push("bio = ?");
            values.push(bio);
        }

        if (isStudent && schoolLevel !== undefined) {
            fields.push("school_level = ?");
            values.push(schoolLevel || null);
        }

        if (isStudent && grade !== undefined) {
            fields.push("grade = ?");
            values.push(grade || null);
        }

        if (newPassword) {
            fields.push("password_hash = ?");
            values.push(await hashPassword(newPassword));
        }

        values.push(userId);

        await db.promise().query(
            `UPDATE users SET ${fields.join(", ")} WHERE id = ?`,
            values
        );


        // ---------------- replace the tutor's subjects ----------------

        if (subjectRows !== null) {

            await db.promise().query(
                "DELETE FROM tutor_subjects WHERE tutor_id = ?",
                [userId]
            );

            if (subjectRows.length > 0) {
                await db.promise().query(
                    "INSERT INTO tutor_subjects (tutor_id, subject_id, hourly_rate) VALUES ?",
                    [subjectRows]
                );
            }
        }


        // ---------------- replace the student's learning subjects ----------------

        if (learningRows !== null) {

            await db.promise().query(
                "DELETE FROM student_subjects WHERE student_id = ?",
                [userId]
            );

            if (learningRows.length > 0) {
                await db.promise().query(
                    "INSERT INTO student_subjects (student_id, subject_id) VALUES ?",
                    [learningRows]
                );
            }
        }


        // Keep the session in sync so the navbar shows the new data
        req.session.user.name = name;
        req.session.user.email = email;

        res.json({
            success: true,
            user: {
                id: userId,
                name: name,
                email: email,
                role: current.role
            }
        });

    } catch (err) {

        if (err.code === "ER_DUP_ENTRY") {
            return res.status(409).json({ error: "Ez az e-mail cím már foglalt." });
        }

        console.error("❌ PUT /api/account error:", err);
        res.status(500).json({ error: "Nem sikerült menteni a módosításokat." });
    }
});



// ============================================================
// BOOKINGS (foglalás)
// ============================================================
//
// Menete:
//   1. Az oktató szabad időpontokat ad meg (availabilities).
//   2. A diák kiválaszt egyet + egy tantárgyat -> a foglalás PENDING,
//      az időpont foglalt (is_booked = 1).
//   3. Az oktató elfogadja (CONFIRMED) vagy elutasítja (REJECTED).
//      Bármelyik fél lemondhatja (CANCELLED) az óra kezdete előtt.
//      Elutasításkor és lemondáskor az időpont újra szabad lesz.
//   4. Az elfogadott foglalás az óra végén magától COMPLETED lesz
//      (ez után lehet értékelni).
//
// Az időpont lefoglalása egyetlen atomi UPDATE-tel történik, így két
// diák nem foglalhatja le ugyanazt.

const BOOKING_FILTERS = {
    pending:   "b.status_ = 'PENDING' AND b.start_time >= NOW()",
    upcoming:  "b.status_ = 'CONFIRMED' AND b.end_time >= NOW()",
    cancelled: "b.status_ IN ('REJECTED', 'CANCELLED')",
    past:      "b.status_ = 'COMPLETED'"
};

const SLOT_MINUTES = [30, 45, 60, 90, 120];
const MAX_REPEAT_WEEKS = 12;
const MAX_FUTURE_SLOTS = 300;
const MAX_DAYS_AHEAD = 120;
const MAX_BOOKING_NOTE = 300;


// Lejárt foglalások rendezése (minden foglalás-lekérés előtt)
async function refreshBookingStatuses() {

    const pool = db.promise();

    // Az óra véget ért: teljesítettnek számít
    await pool.query(
        `UPDATE bookings SET status_ = 'COMPLETED'
         WHERE status_ = 'CONFIRMED' AND end_time < NOW()`
    );

    // Senki nem hagyta jóvá, és az időpont elmúlt: lejárt
    await pool.query(
        `UPDATE bookings SET status_ = 'CANCELLED'
         WHERE status_ = 'PENDING' AND start_time < NOW()`
    );
}


function requireLogin(req, res) {

    if (!req.session.user) {
        res.status(401).json({ error: "Nem vagy bejelentkezve." });
        return false;
    }

    return true;
}


// ---------------- oktató: szabad időpontok kezelése ----------------

app.get("/api/availability", async (req, res) => {

    if (!requireLogin(req, res)) return;

    if (req.session.user.role !== "TUTOR") {
        return res.status(403).json({ error: "Csak oktatónak." });
    }

    try {

        const [rows] = await db.promise().query(
            `SELECT id, start_time, end_time, is_booked
             FROM availabilities
             WHERE tutor_id = ? AND end_time > NOW()
             ORDER BY start_time
             LIMIT ?`,
            [req.session.user.id, MAX_FUTURE_SLOTS]
        );

        res.json(rows);

    } catch (err) {
        console.error("❌ GET /api/availability error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


app.post("/api/availability", async (req, res) => {

    if (!requireLogin(req, res)) return;

    if (req.session.user.role !== "TUTOR") {
        return res.status(403).json({ error: "Csak oktató adhat meg időpontot." });
    }

    const userId = req.session.user.id;

    const startText = String(req.body.start || "");
    const minutes = Number(req.body.minutes);
    const repeatWeeks = Number(req.body.repeat_weeks || 1);

    if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(startText)) {
        return res.status(400).json({ error: "Érvénytelen kezdési időpont." });
    }

    const firstStart = new Date(startText);

    if (Number.isNaN(firstStart.getTime())) {
        return res.status(400).json({ error: "Érvénytelen kezdési időpont." });
    }

    if (!SLOT_MINUTES.includes(minutes)) {
        return res.status(400).json({ error: "Érvénytelen időtartam." });
    }

    if (
        !Number.isInteger(repeatWeeks) ||
        repeatWeeks < 1 ||
        repeatWeeks > MAX_REPEAT_WEEKS
    ) {
        return res.status(400).json({
            error: "Az ismétlés 1 és " + MAX_REPEAT_WEEKS + " hét között legyen."
        });
    }

    const now = new Date();
    const horizon = new Date(now.getTime() + MAX_DAYS_AHEAD * 24 * 3600 * 1000);

    if (firstStart <= now) {
        return res.status(400).json({ error: "Az időpont legyen a jövőben." });
    }

    try {

        const pool = db.promise();

        const [count] = await pool.query(
            "SELECT COUNT(*) AS n FROM availabilities WHERE tutor_id = ? AND end_time > NOW()",
            [userId]
        );

        if (Number(count[0].n) + repeatWeeks > MAX_FUTURE_SLOTS) {
            return res.status(400).json({
                error: "Legfeljebb " + MAX_FUTURE_SLOTS + " jövőbeli időpontod lehet."
            });
        }

        let created = 0;
        let skipped = 0;

        for (let week = 0; week < repeatWeeks; week++) {

            const start = new Date(firstStart);
            start.setDate(start.getDate() + 7 * week);

            const end = new Date(start.getTime() + minutes * 60 * 1000);

            // Túl messzi időpontot nem veszünk fel
            if (start > horizon) {
                skipped++;
                continue;
            }

            // Ütközik egy meglévő időponttal?
            const [overlap] = await pool.query(
                `SELECT id FROM availabilities
                 WHERE tutor_id = ? AND start_time < ? AND end_time > ?
                 LIMIT 1`,
                [userId, end, start]
            );

            if (overlap.length > 0) {
                skipped++;
                continue;
            }

            await pool.query(
                `INSERT INTO availabilities (tutor_id, start_time, end_time, is_booked)
                 VALUES (?, ?, ?, 0)`,
                [userId, start, end]
            );

            created++;
        }

        if (created === 0) {
            return res.status(409).json({
                error: "Az időpont ütközik egy már megadottal, vagy túl messze van."
            });
        }

        res.status(201).json({ created, skipped });

    } catch (err) {
        console.error("❌ POST /api/availability error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


app.delete("/api/availability/:id", async (req, res) => {

    if (!requireLogin(req, res)) return;

    if (req.session.user.role !== "TUTOR") {
        return res.status(403).json({ error: "Csak oktatónak." });
    }

    const slotId = Number(req.params.id);

    if (!Number.isInteger(slotId) || slotId <= 0) {
        return res.status(400).json({ error: "Érvénytelen időpont." });
    }

    try {

        // Csak a saját, még le nem foglalt időpont törölhető
        const [result] = await db.promise().query(
            "DELETE FROM availabilities WHERE id = ? AND tutor_id = ? AND is_booked = 0",
            [slotId, req.session.user.id]
        );

        if (result.affectedRows === 0) {
            return res.status(404).json({
                error: "Az időpont nem található, vagy már le van foglalva."
            });
        }

        res.json({ success: true });

    } catch (err) {
        console.error("❌ DELETE /api/availability error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


// ---------------- diák: szabad időpontok és tantárgyak ----------------

app.get("/api/tutors/:id/availability", async (req, res) => {

    if (!requireLogin(req, res)) return;

    const tutorId = Number(req.params.id);

    if (!Number.isInteger(tutorId) || tutorId <= 0) {
        return res.status(400).json({ error: "Érvénytelen oktató." });
    }

    try {

        const pool = db.promise();

        const [tutor] = await pool.query(
            "SELECT id FROM users WHERE id = ? AND role = 'TUTOR' AND deleted_at IS NULL",
            [tutorId]
        );

        if (tutor.length === 0) {
            return res.status(404).json({ error: "Az oktató nem található." });
        }

        const [slots] = await pool.query(
            `SELECT id, start_time, end_time
             FROM availabilities
             WHERE tutor_id = ? AND is_booked = 0 AND start_time > NOW()
             ORDER BY start_time
             LIMIT 100`,
            [tutorId]
        );

        const [subjects] = await pool.query(
            `SELECT s.id, s.name, ts.hourly_rate
             FROM tutor_subjects ts
             INNER JOIN subjects s ON s.id = ts.subject_id
             WHERE ts.tutor_id = ?
             ORDER BY s.name`,
            [tutorId]
        );

        res.json({ slots, subjects });

    } catch (err) {
        console.error("❌ GET /api/tutors/:id/availability error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


// ---------------- diák: foglalás létrehozása ----------------

app.post("/api/bookings", async (req, res) => {

    if (!requireLogin(req, res)) return;

    if (req.session.user.role !== "STUDENT") {
        return res.status(403).json({ error: "Csak diák foglalhat időpontot." });
    }

    const studentId = req.session.user.id;

    const slotId = Number(req.body.availability_id);
    const subjectId = Number(req.body.subject_id);
    const note = String(req.body.note || "").trim();

    if (!Number.isInteger(slotId) || slotId <= 0) {
        return res.status(400).json({ error: "Válassz időpontot." });
    }

    if (!Number.isInteger(subjectId) || subjectId <= 0) {
        return res.status(400).json({ error: "Válassz tantárgyat." });
    }

    if (note.length > MAX_BOOKING_NOTE) {
        return res.status(400).json({
            error: "Az üzenet legfeljebb " + MAX_BOOKING_NOTE + " karakter lehet."
        });
    }

    try {

        const pool = db.promise();

        const [slots] = await pool.query(
            `SELECT a.id, a.tutor_id, a.start_time, a.end_time
             FROM availabilities a
             INNER JOIN users u ON u.id = a.tutor_id
             WHERE a.id = ? AND u.role = 'TUTOR' AND u.deleted_at IS NULL`,
            [slotId]
        );

        if (slots.length === 0) {
            return res.status(404).json({ error: "Az időpont nem található." });
        }

        const slot = slots[0];

        // Az oktató tanítja-e ezt a tantárgyat, és mennyiért?
        const [rates] = await pool.query(
            "SELECT hourly_rate FROM tutor_subjects WHERE tutor_id = ? AND subject_id = ?",
            [slot.tutor_id, subjectId]
        );

        if (rates.length === 0) {
            return res.status(400).json({ error: "Az oktató nem tanítja ezt a tantárgyat." });
        }

        // A diáknak nem lehet két foglalása egy időben
        const [clash] = await pool.query(
            `SELECT id FROM bookings
             WHERE student_id = ?
               AND status_ IN ('PENDING', 'CONFIRMED')
               AND start_time < ? AND end_time > ?
             LIMIT 1`,
            [studentId, slot.end_time, slot.start_time]
        );

        if (clash.length > 0) {
            return res.status(409).json({
                error: "Ebben az idősávban már van egy foglalásod."
            });
        }

        // Az időpont lefoglalása: csak akkor sikerül, ha még szabad.
        const [claim] = await pool.query(
            `UPDATE availabilities SET is_booked = 1
             WHERE id = ? AND is_booked = 0 AND start_time > NOW()`,
            [slotId]
        );

        if (claim.affectedRows === 0) {
            return res.status(409).json({
                error: "Ezt az időpontot már lefoglalták, vagy lejárt."
            });
        }

        const minutes = (new Date(slot.end_time) - new Date(slot.start_time)) / 60000;
        const price = Math.round(Number(rates[0].hourly_rate) * minutes / 60);

        let bookingId;

        try {

            const [result] = await pool.query(
                `INSERT INTO bookings
                    (student_id, tutor_id, subject_id, start_time, end_time,
                     status_, created_at, price, note)
                 VALUES (?, ?, ?, ?, ?, 'PENDING', NOW(), ?, ?)`,
                [
                    studentId, slot.tutor_id, subjectId,
                    slot.start_time, slot.end_time,
                    price, note || null
                ]
            );

            bookingId = result.insertId;

        } catch (insertError) {

            // Sikertelen mentés: az időpont újra szabad
            await pool.query(
                "UPDATE availabilities SET is_booked = 0 WHERE id = ?",
                [slotId]
            );

            throw insertError;
        }

        console.log("📅 Új foglalás:", bookingId);

        sendToUser(slot.tutor_id, { type: "booking_update" });

        res.status(201).json({ id: bookingId, status: "PENDING", price });

    } catch (err) {
        console.error("❌ POST /api/bookings error:", err);
        res.status(500).json({ error: "Nem sikerült a foglalás." });
    }
});


// ---------------- foglalások listája ----------------

app.get("/api/bookings/summary", async (req, res) => {

    if (!requireLogin(req, res)) return;

    try {

        await refreshBookingStatuses();

        const [rows] = await db.promise().query(
            `SELECT
                SUM(b.status_ = 'PENDING' AND b.start_time >= NOW())   AS pending,
                SUM(b.status_ = 'CONFIRMED' AND b.end_time >= NOW())   AS upcoming,
                SUM(b.status_ IN ('REJECTED', 'CANCELLED'))            AS cancelled,
                SUM(b.status_ = 'COMPLETED')                           AS past
             FROM bookings b
             WHERE b.student_id = ? OR b.tutor_id = ?`,
            [req.session.user.id, req.session.user.id]
        );

        const row = rows[0];

        res.json({
            pending: Number(row.pending) || 0,
            upcoming: Number(row.upcoming) || 0,
            cancelled: Number(row.cancelled) || 0,
            past: Number(row.past) || 0
        });

    } catch (err) {
        console.error("❌ GET /api/bookings/summary error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


app.get("/api/bookings", async (req, res) => {

    if (!requireLogin(req, res)) return;

    const filter = String(req.query.status || "upcoming");

    if (!Object.prototype.hasOwnProperty.call(BOOKING_FILTERS, filter)) {
        return res.status(400).json({ error: "Érvénytelen szűrő." });
    }

    const userId = req.session.user.id;

    // A közelgők időrendben, a régiek visszafelé
    const order = (filter === "pending" || filter === "upcoming") ? "ASC" : "DESC";

    try {

        await refreshBookingStatuses();

        // A szűrő fix listából jön, nem a felhasználótól
        const [rows] = await db.promise().query(
            `SELECT
                b.id,
                b.status_ AS status,
                b.start_time,
                b.end_time,
                b.price,
                b.note,
                (b.student_id = ?) AS as_student,
                CASE WHEN b.student_id = ? THEN b.tutor_id ELSE b.student_id END AS other_id,
                CASE WHEN b.student_id = ? THEN tutor.full_name ELSE student.full_name END AS other_name,
                subject.name AS subject_name,
                (b.start_time > NOW()) AS in_future,
                EXISTS (SELECT 1 FROM reviews r WHERE r.booking_id = b.id) AS has_review,
                (SELECT r.rating FROM reviews r WHERE r.booking_id = b.id LIMIT 1) AS review_rating,
                (SELECT r.comment_ FROM reviews r WHERE r.booking_id = b.id LIMIT 1) AS review_comment

             FROM bookings b
             LEFT JOIN users tutor ON tutor.id = b.tutor_id
             LEFT JOIN users student ON student.id = b.student_id
             LEFT JOIN subjects subject ON subject.id = b.subject_id

             WHERE (b.student_id = ? OR b.tutor_id = ?)
               AND ${BOOKING_FILTERS[filter]}

             ORDER BY b.start_time ${order}
             LIMIT 200`,
            [userId, userId, userId, userId, userId]
        );

        res.json(rows.map((row) => ({
            id: row.id,
            status: row.status,
            start_time: row.start_time,
            end_time: row.end_time,
            price: row.price,
            note: row.note,
            as_student: Boolean(row.as_student),
            other_id: row.other_id,
            other_name: row.other_name,
            subject_name: row.subject_name,
            has_review: Boolean(row.has_review),
            review: row.review_rating
                ? { rating: Number(row.review_rating), comment: row.review_comment || "" }
                : null,
            // A művelet-gombokhoz: mit tehet a felhasználó ezzel a foglalással
            can_confirm: !row.as_student && row.status === "PENDING" && Boolean(row.in_future),
            can_reject: !row.as_student && row.status === "PENDING" && Boolean(row.in_future),
            can_cancel: (row.status === "PENDING" || row.status === "CONFIRMED") && Boolean(row.in_future)
        })));

    } catch (err) {
        console.error("❌ GET /api/bookings error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


// ---------------- értékelés írása ----------------
//
// A teljesített óra után a diák egyszer értékelheti az oktatót
// (1-5 csillag + opcionális szöveg). Az értékelés nem szerkeszthető
// és nem törölhető, így hiteles marad.
//
// FONTOS: ennek az alábbi általános "/:id/:action" útvonal előtt kell
// szerepelnie, különben az "review" érvénytelen műveletként elbukna.

const MAX_REVIEW_COMMENT = 1000;

app.post("/api/bookings/:id/review", async (req, res) => {

    if (!requireLogin(req, res)) return;

    if (req.session.user.role !== "STUDENT") {
        return res.status(403).json({ error: "Csak diák értékelhet." });
    }

    const userId = req.session.user.id;
    const bookingId = Number(req.params.id);
    const rating = Number(req.body.rating);
    const comment = String(req.body.comment || "").trim();

    if (!Number.isInteger(bookingId) || bookingId <= 0) {
        return res.status(400).json({ error: "Érvénytelen foglalás." });
    }

    if (!Number.isInteger(rating) || rating < 1 || rating > 5) {
        return res.status(400).json({ error: "Az értékelés 1 és 5 csillag között legyen." });
    }

    if (comment.length > MAX_REVIEW_COMMENT) {
        return res.status(400).json({
            error: "A vélemény legfeljebb " + MAX_REVIEW_COMMENT + " karakter lehet."
        });
    }

    try {

        const pool = db.promise();

        await refreshBookingStatuses();

        // Csak a saját foglalás
        const [rows] = await pool.query(
            "SELECT id, tutor_id, status_ AS status FROM bookings WHERE id = ? AND student_id = ?",
            [bookingId, userId]
        );

        if (rows.length === 0) {
            return res.status(404).json({ error: "A foglalás nem található." });
        }

        if (rows[0].status !== "COMPLETED") {
            return res.status(409).json({
                error: "Csak a teljesített órát lehet értékelni."
            });
        }

        const [existing] = await pool.query(
            "SELECT id FROM reviews WHERE booking_id = ? LIMIT 1",
            [bookingId]
        );

        if (existing.length > 0) {
            return res.status(409).json({ error: "Ezt az órát már értékelted." });
        }

        const [result] = await pool.query(
            `INSERT INTO reviews (booking_id, rating, comment_, crated_at)
             VALUES (?, ?, ?, NOW())`,
            [bookingId, String(rating), comment || null]
        );

        console.log("⭐ Új értékelés:", result.insertId, "foglalás:", bookingId);

        sendToUser(rows[0].tutor_id, { type: "booking_update" });

        res.status(201).json({ id: result.insertId, rating });

    } catch (err) {

        // Két egyszerre elküldött értékelést az egyedi index fogja meg
        if (err.code === "ER_DUP_ENTRY") {
            return res.status(409).json({ error: "Ezt az órát már értékelted." });
        }

        console.error("❌ POST /api/bookings/:id/review error:", err);
        res.status(500).json({ error: "Nem sikerült menteni az értékelést." });
    }
});


// ---------------- elfogadás, elutasítás, lemondás ----------------

const BOOKING_ACTIONS = {
    confirm: { from: "PENDING",   to: "CONFIRMED", tutorOnly: true,  frees: false },
    reject:  { from: "PENDING",   to: "REJECTED",  tutorOnly: true,  frees: true  },
    cancel:  { from: null,        to: "CANCELLED", tutorOnly: false, frees: true  }
};

app.post("/api/bookings/:id/:action", async (req, res) => {

    if (!requireLogin(req, res)) return;

    const action = BOOKING_ACTIONS[req.params.action];
    const bookingId = Number(req.params.id);

    if (!action || !Number.isInteger(bookingId) || bookingId <= 0) {
        return res.status(400).json({ error: "Érvénytelen kérés." });
    }

    const userId = req.session.user.id;

    try {

        const pool = db.promise();

        await refreshBookingStatuses();

        const [rows] = await pool.query(
            `SELECT id, student_id, tutor_id, start_time, end_time, status_ AS status,
                    (start_time > NOW()) AS in_future
             FROM bookings
             WHERE id = ? AND (student_id = ? OR tutor_id = ?)`,
            [bookingId, userId, userId]
        );

        if (rows.length === 0) {
            return res.status(404).json({ error: "A foglalás nem található." });
        }

        const booking = rows[0];
        const isTutor = Number(booking.tutor_id) === Number(userId);

        if (action.tutorOnly && !isTutor) {
            return res.status(403).json({ error: "Ezt csak az oktató teheti meg." });
        }

        // Lemondani csak függő vagy elfogadott, még el nem kezdődött órát lehet
        const allowedFrom = action.from
            ? [action.from]
            : ["PENDING", "CONFIRMED"];

        if (!allowedFrom.includes(booking.status) || !booking.in_future) {
            return res.status(409).json({
                error: "A foglalás jelenlegi állapotában ez nem lehetséges."
            });
        }

        // A státuszt csak akkor írjuk át, ha közben nem változott
        const [result] = await pool.query(
            "UPDATE bookings SET status_ = ? WHERE id = ? AND status_ = ?",
            [action.to, bookingId, booking.status]
        );

        if (result.affectedRows === 0) {
            return res.status(409).json({ error: "A foglalás időközben megváltozott." });
        }

        // Elutasításkor és lemondáskor az időpont újra szabad
        if (action.frees) {

            await pool.query(
                `UPDATE availabilities SET is_booked = 0
                 WHERE tutor_id = ? AND start_time = ? AND end_time = ?`,
                [booking.tutor_id, booking.start_time, booking.end_time]
            );
        }

        console.log("📅 Foglalás", bookingId, "->", action.to);

        const otherId = isTutor ? booking.student_id : booking.tutor_id;

        sendToUser(otherId, { type: "booking_update" });

        res.json({ success: true, status: action.to });

    } catch (err) {
        console.error("❌ POST /api/bookings/:id/:action error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


// ============================================================
// STUDENT PROFILE (az oktatónak, akivel a diák beszélget)
// ============================================================
//
// Csak az a TUTOR kérheti le, akinek van beszélgetése az adott diákkal.
// E-mail cím nem megy ki.

app.get("/api/students/:id/profile", async (req, res) => {

    if (!req.session.user) {
        return res.status(401).json({ error: "Nem vagy bejelentkezve." });
    }

    if (req.session.user.role !== "TUTOR") {
        return res.status(403).json({ error: "Csak oktató nézheti meg a diák profilját." });
    }

    const studentId = Number(req.params.id);

    if (!Number.isInteger(studentId) || studentId <= 0) {
        return res.status(400).json({ error: "Érvénytelen diák." });
    }

    try {

        const [shared] = await db.promise().query(
            "SELECT id FROM conversations WHERE tutor_id = ? AND student_id = ? LIMIT 1",
            [req.session.user.id, studentId]
        );

        if (shared.length === 0) {
            return res.status(403).json({ error: "Nincs közös beszélgetésetek." });
        }

        const [rows] = await db.promise().query(
            `SELECT id, full_name, school_level, grade, deleted_at
             FROM users
             WHERE id = ? AND role = 'STUDENT'
             LIMIT 1`,
            [studentId]
        );

        if (rows.length === 0) {
            return res.status(404).json({ error: "A diák nem található." });
        }

        const student = rows[0];

        const [learning] = await db.promise().query(
            `SELECT s.name
             FROM student_subjects ss
             INNER JOIN subjects s ON s.id = ss.subject_id
             WHERE ss.student_id = ?
             ORDER BY s.name`,
            [studentId]
        );

        res.json({
            id: student.id,
            name: student.full_name,
            school_level: student.school_level || "",
            grade: student.grade || "",
            subjects: learning.map((item) => item.name),
            deleted: Boolean(student.deleted_at)
        });

    } catch (err) {
        console.error("❌ GET /api/students/:id/profile error:", err);
        res.status(500).json({ error: "Adatbázis hiba." });
    }
});


// ============================================================
// DELETE ACCOUNT (anonimizálás)
// ============================================================
//
// A fiók nem törlődik fizikailag, mert a beszélgetések, foglalások és
// értékelések hivatkoznak rá. Helyette anonimizáljuk:
//   - a név "Törölt felhasználó", az e-mail egy értelmetlen cím,
//     a jelszó érvénytelen, a bemutatkozás, szint, osztály törlődik
//   - a tantárgyak (és oktatónak az óradíjak) törlődnek
//   - a csatolmányos üzenetei visszavonódnak (a fájl is törlődik)
//   - a szöveges üzenetei a beszélgetés másik tagjánál megmaradnak,
//     de már nem köthetők névhez
//   - az oktató eltűnik az oktatók listájából
// A művelethez a jelenlegi jelszó kell, és nem vonható vissza.

app.delete("/api/account", async (req, res) => {

    if (!req.session.user) {
        return res.status(401).json({ error: "Nem vagy bejelentkezve." });
    }

    const userId = req.session.user.id;
    const currentPassword = String(req.body.currentPassword || "");

    if (!currentPassword) {
        return res.status(400).json({
            error: "A fiók törléséhez add meg a jelenlegi jelszavad."
        });
    }

    try {

        const pool = db.promise();

        const [rows] = await pool.query(
            "SELECT password_hash, role, deleted_at FROM users WHERE id = ? LIMIT 1",
            [userId]
        );

        if (rows.length === 0 || rows[0].deleted_at) {
            return res.status(404).json({ error: "A felhasználó nem található." });
        }

        if (rows[0].role === "ADMIN") {
            return res.status(403).json({ error: "Admin fiók itt nem törölhető." });
        }

        const ok = await verifyPassword(rows[0].password_hash, currentPassword);

        if (!ok) {
            return res.status(403).json({ error: "A jelenlegi jelszó hibás." });
        }

        // 1) a csatolmányos üzenetei visszavonódnak, a fájlok törlődnek
        const [files] = await pool.query(
            `SELECT attachment_path FROM messages
             WHERE sender_id = ? AND attachment_path IS NOT NULL`,
            [userId]
        );

        await pool.query(
            `UPDATE messages
             SET content = '',
                 deleted_at = NOW(),
                 attachment_name = NULL,
                 attachment_path = NULL,
                 attachment_type = NULL,
                 attachment_size = NULL
             WHERE sender_id = ? AND attachment_path IS NOT NULL`,
            [userId]
        );

        files.forEach((file) => {
            fs.unlink(
                path.join(CHAT_UPLOAD_DIR, path.basename(file.attachment_path)),
                () => {}
            );
        });

        // 2) tantárgyak, óradíjak
        await pool.query("DELETE FROM tutor_subjects WHERE tutor_id = ?", [userId]);
        await pool.query("DELETE FROM student_subjects WHERE student_id = ?", [userId]);

        // 3) a felhasználó sora anonimizálva
        await pool.query(
            `UPDATE users
             SET full_name = 'Törölt felhasználó',
                 email = ?,
                 password_hash = '!deleted',
                 bio = '',
                 school_level = NULL,
                 grade = NULL,
                 deleted_at = NOW()
             WHERE id = ?`,
            ["deleted-" + userId + "@deleted.invalid", userId]
        );

        deletedUserIds.add(Number(userId));

        console.log("🗑️ Fiók anonimizálva:", userId);

        // 4) nyitott websocket kapcsolatai bezárulnak
        const sockets = clients.get(Number(userId));

        if (sockets) {
            sockets.forEach((ws) => ws.close());
        }

        // 5) kijelentkeztetés
        req.session.destroy(() => {
            res.clearCookie("connect.sid");
            res.json({ success: true });
        });

    } catch (err) {

        console.error("❌ DELETE /api/account error:", err);
        res.status(500).json({ error: "Nem sikerült törölni a fiókot." });
    }
});



// ============================================================
// START SERVER
// ============================================================

server.listen(3000, () => {
    console.log("\n==========================================");
    console.log("🌍 SERVER RUNNING");
    console.log("➡️ http://localhost:3000");
    console.log(
        DEBUG_LOGS
            ? "🐞 Teszt-naplózás: BE (a jelszavak is kiíródnak, élesben ne használd)"
            : "🔒 Teszt-naplózás: KI"
    );
    console.log("==========================================\n");
});