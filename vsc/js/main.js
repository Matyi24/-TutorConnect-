const express = require("express");
const app = express();
const path = require("path");
const mysql = require("mysql2");

const { hashPassword, verifyPassword } = require("./auth");


// ============================================================
// EXPRESS SETUP
// ============================================================

console.log("==========================================");
console.log("🚀 TUTOR CONNECT SERVER STARTING");
console.log("==========================================");

app.use(express.urlencoded({ extended: true }));
app.use(express.json());

app.use("/css", express.static(path.join(__dirname, "../css")));
app.use("/js", express.static(path.join(__dirname, "../js")));
app.use("/html", express.static(path.join(__dirname, "../html")));
app.use("/images", express.static(path.join(__dirname, "../images")));

app.use(express.static("vsc"));


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
    res.sendFile(path.join(__dirname, "../html/index.html"));
});

app.get("/index", (req, res) => {
    console.log("🌐 GET /index");
    res.sendFile(path.join(__dirname, "../html/index.html"));
});

app.get("/subs", (req, res) => {
    console.log("🌐 GET /subs");
    res.sendFile(path.join(__dirname, "../html/subs.html"));
});

app.get("/login", (req, res) => {
    console.log("🌐 GET /login");
    res.sendFile(path.join(__dirname, "../html/login.html"));
});

app.get("/register", (req, res) => {
    console.log("🌐 GET /register");
    res.sendFile(path.join(__dirname, "../html/register.html"));
});
app.get("/gyik", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/gyik.html"));                                                                                                                                                                                                                                                                  
});
app.get("/chat", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/chat.html"));                                                                                                                                                                                                                                                                  
});
app.get("/oktatok", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/oktatok.html"));                                                                                                                                                                                                                                                                  
});
app.get("/foglalas", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/foglalas.html"));                                                                                                                                                                                                                                                                  
});
app.get("/foglalasutan", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/foglalasutan.html"));
});


// ============================================================
// REGISTER
// ============================================================

app.post("/register", async (req, res) => {

    console.log("\n");
    console.log("==========================================");
    console.log("📝 REGISTRATION REQUEST RECEIVED");
    console.log("==========================================");

    console.log("\n📥 RAW DATA RECEIVED FROM BROWSER:");
    console.log(req.body);

    const {
        name,
        email,
        password,
        password_confirm,
        role
    } = req.body;


    // --------------------------------------------------------
    // STEP 1 - SHOW RECEIVED DATA
    // --------------------------------------------------------

    console.log("\n🔎 STEP 1 - DATA EXTRACTED FROM REQUEST");

    console.log("👤 Name:", name);
    console.log("📧 Email:", email);
    console.log("🔑 Raw password:", password);
    console.log("🔑 Password confirmation:", password_confirm);
    console.log("🎭 Role:", role);


    // --------------------------------------------------------
    // STEP 2 - VALIDATION
    // --------------------------------------------------------

    console.log("\n🧪 STEP 2 - VALIDATING DATA...");

    if (!name || !email || !password || !password_confirm || !role) {

        console.log("❌ Validation failed: missing data");

        return res.status(400).send(
            "Minden mezőt ki kell tölteni."
        );
    }

    console.log("✅ All required fields exist");


    if (password !== password_confirm) {

        console.log("❌ Passwords do NOT match");

        return res.status(400).send(
            "A két jelszó nem egyezik."
        );
    }

    console.log("✅ Password confirmation matches");


    const allowedRoles = ["STUDENT", "TUTOR"];

    if (!allowedRoles.includes(role)) {

        console.log("❌ Invalid role:", role);

        return res.status(400).send(
            "Érvénytelen szerepkör."
        );
    }

    console.log("✅ Role is valid:", role);


    // --------------------------------------------------------
    // STEP 3 - SEND RAW PASSWORD TO AUTH.JS
    // --------------------------------------------------------

    console.log("\n🔐 STEP 3 - SENDING PASSWORD TO AUTH.JS");
    console.log("📤 Password being sent to hashPassword():");
    console.log(password);


    let passwordHash;

    try {

        passwordHash = await hashPassword(password);

    } catch (error) {

        console.error("\n❌ HASHING FAILED");
        console.error(error);

        return res.status(500).send(
            "Nem sikerült feldolgozni a jelszót."
        );
    }


    // --------------------------------------------------------
    // STEP 4 - RECEIVE HASH FROM AUTH.JS
    // --------------------------------------------------------

    console.log("\n🔐 STEP 4 - HASH RECEIVED FROM AUTH.JS");

    console.log("📥 Password hash:");
    console.log(passwordHash);

    console.log("\n📊 HASH INFORMATION:");
    console.log("Type:", typeof passwordHash);
    console.log("Length:", passwordHash.length);


    // --------------------------------------------------------
    // STEP 5 - PREPARE MYSQL DATA
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


    console.log("\n🗄️ STEP 5 - PREPARING MYSQL INSERT");

    console.log("SQL:");
    console.log(sql);

    console.log("DATA GOING TO MYSQL:");

    console.log({
        full_name: name,
        email: email,
        password_hash: passwordHash,
        role: role,
        bio: "",
        hourly_rate: 0
    });


    // --------------------------------------------------------
    // STEP 6 - INSERT INTO MYSQL
    // --------------------------------------------------------

    console.log("\n💾 STEP 6 - SENDING DATA TO MYSQL...");

    db.query(sql, values, (err, result) => {

        if (err) {

            console.error("\n❌ MYSQL ERROR");
            console.error("Error code:", err.code);
            console.error("Error message:", err.message);
            console.error("SQL message:", err.sqlMessage);

            // Only say "email already exists" if MySQL
            // actually reports the email unique key.
            if (
                err.code === "ER_DUP_ENTRY" &&
                err.sqlMessage &&
                err.sqlMessage.includes("email")
            ) {

                console.log("⚠️ EMAIL ALREADY EXISTS");

                return res.status(409).send(
                    "Ez az e-mail cím már regisztrálva van."
                );
            }

            return res.status(500).send(
                "Adatbázis hiba: " + err.message
            );
        }


        // ----------------------------------------------------
        // STEP 7 - MYSQL SUCCESS
        // ----------------------------------------------------

        console.log("\n✅ MYSQL INSERT SUCCESSFUL");

        console.log("Inserted ID:", result.insertId);
        console.log("Affected rows:", result.affectedRows);

        console.log("\n🎉 REGISTRATION COMPLETE");
        console.log("==========================================\n");


        res.send(`
            <h1>Sikeres regisztráció!</h1>
            <p>Üdvözlünk, ${name}!</p>
            <p>Az adataid sikeresen bekerültek az adatbázisba.</p>
            <a href="/index">Vissza a főoldalra</a>
        `);
    });
});


// ============================================================
// LOGIN
// ============================================================

app.post("/login", async (req, res) => {

    console.log("\n");
    console.log("==========================================");
    console.log("🔑 LOGIN REQUEST RECEIVED");
    console.log("==========================================");

    console.log("\n📥 RAW LOGIN DATA:");
    console.log(req.body);

    const {
        email,
        password
    } = req.body;


    console.log("\n👤 Login email:", email);
    console.log("🔑 Login raw password:", password);


    if (!email || !password) {

        console.log("❌ Missing login data");

        return res.status(400).send(
            "E-mail és jelszó szükséges."
        );
    }


    // --------------------------------------------------------
    // FIND USER
    // --------------------------------------------------------

    console.log("\n🔎 Searching MySQL for email...");

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


    db.query(sql, [email], async (err, results) => {

        if (err) {

            console.error("❌ MYSQL LOGIN ERROR");
            console.error(err);

            return res.status(500).send(
                "Adatbázis hiba."
            );
        }


        console.log("📊 Users found:", results.length);


        if (results.length === 0) {

            console.log("❌ No user found with this email");

            return res.status(401).send(
                "Hibás e-mail vagy jelszó."
            );
        }


        const user = results[0];


        console.log("\n👤 USER FOUND:");
        console.log("ID:", user.id);
        console.log("Name:", user.full_name);
        console.log("Email:", user.email);
        console.log("Role:", user.role);

        console.log("\n🔐 HASH FROM MYSQL:");
        console.log(user.password_hash);


        // ----------------------------------------------------
        // VERIFY PASSWORD
        // ----------------------------------------------------

        console.log("\n🔍 Sending password + hash to auth.js...");

        const validPassword = await verifyPassword(
            user.password_hash,
            password
        );


        console.log("🔎 Password verification result:", validPassword);


        if (!validPassword) {

            console.log("❌ LOGIN FAILED - WRONG PASSWORD");

            return res.status(401).send(
                "Hibás e-mail vagy jelszó."
            );
        }


        console.log("\n✅ LOGIN SUCCESSFUL");
        console.log("==========================================\n");


        res.send(`
            <h1>Sikeres bejelentkezés!</h1>
            <p>Üdv, ${user.full_name}!</p>
        `);
    });
});


// ============================================================
// SUBJECTS API
// ============================================================

app.get("/api/subjects", (req, res) => {

    console.log("📚 GET /api/subjects");

    const sql = "SELECT * FROM subjects";

    db.query(sql, (err, results) => {

        if (err) {

            console.error("❌ SQL hiba:", err);

            return res.status(500).json({
                error: "Adatbázis hiba"
            });
        }

        console.log("✅ Tantárgyak lekérve:", results.length);

        res.json(results);
    });
});


// ============================================================
// MYSQL CONNECT
// ============================================================

db.connect((err) => {

    if (err) {

        console.error("❌ MYSQL CONNECTION ERROR");
        console.error(err);

        return;
    }

    console.log("✅ Sikeresen csatlakozva a MySQL-hez!");
    console.log("📊 Database: zsamo");
    console.log("🖥️ Host: localhost");
});


// ============================================================
// START SERVER
// ============================================================

app.listen(3000, () => {

    console.log("\n==========================================");
    console.log("🌍 SERVER RUNNING");
    console.log("➡️ http://localhost:3000");
    console.log("==========================================\n");

});