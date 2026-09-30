const express = require("express");
const path = require("path");
const mysql = require("mysql2");
const session = require("express-session");

const { hashPassword, verifyPassword } = require("./auth");

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

app.use(
    session({
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

            // 1000 ms * 60 s * 60 m * 24 h * 7 days = 7 days
            maxAge: 1000 * 60 * 60 * 24 * 7
        }
    })
);

// ============================================================
// STATIC FILES
// ============================================================

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


    console.log("\n📥 RAW DATA RECEIVED:");
    console.log(req.body);


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
            u.hourly_rate,

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
            ) AS subjects

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

        GROUP BY
            u.id,
            u.full_name,
            u.email,
            u.bio,
            u.hourly_rate,
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
            (SELECT COUNT(*) FROM users WHERE role = 'TUTOR') AS tutor_count,
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
            ) AS last_message_time

        FROM conversations

        JOIN users AS student
            ON conversations.student_id = student.id

        JOIN users AS tutor
            ON conversations.tutor_id = tutor.id

        WHERE conversations.student_id = ?
           OR conversations.tutor_id = ?
    `;

    db.query(
        sql,
        [userId, userId, userId],
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
            messages.created_at

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

                    console.log("✅ Üzenet elmentve:", result.insertId);

                    res.status(201).json(saved[0]);
                }
            );
        });
    });
});
// ============================================================
// START SERVER
// ============================================================

app.listen(
    3000,
    () => {

        console.log("\n==========================================");
        console.log("🌍 SERVER RUNNING");
        console.log("➡️ http://localhost:3000");
        console.log("==========================================\n");

    });
