// Tests for vsc/js/main.js (the Express + WebSocket server).
// Run with:  npm test
//
// No MySQL server is needed: helpers/server.js swaps the "mysql2" module for an
// in-memory fake and starts the real server on a random port. In each test we
// tell the fake database what to answer with  db.when(/sql regex/, () => rows).

const { describe, it, before, after, beforeEach, mock } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const path = require("path");
const argon2 = require("argon2");
const WebSocket = require("ws");

const { start, Client } = require("./helpers/server");
const { hashPassword } = require("../vsc/js/auth");

// main.js and auth.js print a lot (even passwords) - keep the test output readable.
console.log = () => {};
console.error = () => {};

const UPLOAD_DIR = path.join(__dirname, "../vsc/js/uploads/chat");

let app;          // { db, baseUrl, stop, ... }
let db;           // the fake database
let passwordHash; // hash of PASSWORD, used as the "stored" hash of every test user
let uploadsBefore;

const PASSWORD = "Jelszo#1";

const STUDENT = { id: 1, full_name: "Diák Dóra", email: "dora@example.com", role: "STUDENT" };
const TUTOR = { id: 2, full_name: "Tanár Tamás", email: "tamas@example.com", role: "TUTOR" };

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// ------------------------------------------------------------
// helpers
// ------------------------------------------------------------

function newClient() {
    return new Client(app.baseUrl);
}

// Logs a fresh simulated browser in as the given user (through the real /login route).
async function loginAs(user) {
    const client = newClient();

    db.when(/FROM users\s+WHERE email = \?/, () => [{ ...user, password_hash: passwordHash }]);

    const res = await client.post("/login", { form: { email: user.email, password: PASSWORD } });
    assert.equal(res.status, 302, "test login should succeed");

    return client;
}

function multipart(fileName, content, fields = {}) {
    const form = new FormData();
    for (const [key, value] of Object.entries(fields)) form.append(key, value);
    form.append("file", new Blob([content]), fileName);
    return form;
}

function listUploads() {
    return fs.existsSync(UPLOAD_DIR) ? fs.readdirSync(UPLOAD_DIR) : [];
}

async function waitUntil(condition, timeout = 1000) {
    const end = Date.now() + timeout;
    while (Date.now() < end) {
        if (await condition()) return true;
        await sleep(20);
    }
    return false;
}

// --- WebSocket helpers ---

function openSocket(client, cookieHeader = client.cookieHeader) {
    return new Promise((resolve, reject) => {
        const ws = new WebSocket(app.baseUrl.replace("http", "ws") + "/", {
            headers: { Cookie: cookieHeader }
        });

        ws.queue = [];
        ws.on("message", (raw) => {
            ws.queue.push(JSON.parse(raw));
            ws.emit("queued");
        });
        ws.once("open", () => resolve(ws));
        ws.once("error", reject);
    });
}

function nextMessage(ws, timeout = 1000) {
    return new Promise((resolve, reject) => {
        const timer = setTimeout(() => {
            ws.off("queued", check);
            reject(new Error("Timed out waiting for a WebSocket message"));
        }, timeout);

        function check() {
            if (!ws.queue.length) return;
            clearTimeout(timer);
            ws.off("queued", check);
            resolve(ws.queue.shift());
        }

        ws.on("queued", check);
        check();
    });
}

async function expectNoMessage(ws, ms = 150) {
    await sleep(ms);
    assert.deepEqual(ws.queue, []);
}

// ------------------------------------------------------------
// setup / teardown
// ------------------------------------------------------------

before(async () => {
    passwordHash = await hashPassword(PASSWORD);
    uploadsBefore = new Set(listUploads());
    app = await start();
    db = app.db;
});

beforeEach(() => {
    db.reset();
});

after(async () => {
    await app.stop();

    // remove any file the tests left in the real upload folder
    for (const file of listUploads()) {
        if (!uploadsBefore.has(file)) fs.rmSync(path.join(UPLOAD_DIR, file), { force: true });
    }
});

// ============================================================
// STATIC FILES AND PAGES
// ============================================================

describe("pages and static files", () => {

    const pages = {
        "/": "index.html",
        "/index": "index.html",
        "/subs": "subs.html",
        "/login": "login.html",
        "/register": "register.html",
        "/gyik": "gyik.html",
        "/chat": "chat.html",
        "/oktatok": "oktatok.html",
        "/foglalas": "foglalas.html",
        "/foglalasutan": "foglalasutan.html"
    };

    for (const [url, file] of Object.entries(pages)) {
        it(`GET ${url} serves ${file}`, async () => {
            const res = await newClient().get(url);

            assert.equal(res.status, 200);
            assert.match(res.headers.get("content-type"), /text\/html/);
            assert.equal(
                res.text,
                fs.readFileSync(path.join(__dirname, "../vsc/html", file), "utf8")
            );
        });
    }

    it("serves css files", async () => {
        const res = await newClient().get("/css/style.css");

        assert.equal(res.status, 200);
        assert.match(res.headers.get("content-type"), /text\/css/);
    });

    it("serves images", async () => {
        const res = await newClient().get("/images/pupel.png");

        assert.equal(res.status, 200);
        assert.match(res.headers.get("content-type"), /image\/png/);
    });

    it("serves the front-end scripts", async () => {
        const res = await newClient().get("/js/chat.js");

        assert.equal(res.status, 200);
    });

    it("does not expose node_modules", async () => {
        const res = await newClient().get("/node_modules/express/package.json");

        assert.equal(res.status, 404);
    });

    it("answers 400 for a malformed URL", async () => {
        const res = await newClient().get("/%E0%A4%A");

        assert.equal(res.status, 400);
    });

    it("returns 404 for unknown pages", async () => {
        const res = await newClient().get("/nincs-ilyen-oldal");

        assert.equal(res.status, 404);
    });

    // KNOWN PROBLEM: the "/js" static route is registered BEFORE the middleware that
    // is meant to hide the server folder, so the server source code (database
    // credentials, session secret fallback) and uploaded chat files can be
    // downloaded by anyone. These are marked `todo`: they document the intended
    // behaviour, do not fail the run, and will start passing once it is fixed.
    it("does not expose the server source (main.js)", { todo: "/js static route leaks server code" }, async () => {
        const res = await newClient().get("/js/main.js");

        assert.equal(res.status, 404);
    });

    it("does not expose the server source (auth.js)", { todo: "/js static route leaks server code" }, async () => {
        const res = await newClient().get("/js/auth.js");

        assert.equal(res.status, 404);
    });

    it("does not expose uploaded chat files directly", { todo: "/js static route leaks uploads" }, async () => {
        const name = "static-leak-test.txt";
        fs.writeFileSync(path.join(UPLOAD_DIR, name), "secret");

        const res = await newClient().get("/js/uploads/chat/" + name);

        assert.equal(res.status, 404);
    });
});

// ============================================================
// CURRENT USER / LOGOUT
// ============================================================

describe("GET /api/me", () => {

    it("reports loggedIn:false without a session", async () => {
        const res = await newClient().get("/api/me");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, { loggedIn: false });
    });

    it("returns only the safe user fields after login", async () => {
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/me");

        assert.deepEqual(res.json, {
            loggedIn: true,
            user: { id: 1, name: "Diák Dóra", email: "dora@example.com", role: "STUDENT" }
        });
        assert.ok(!res.text.includes("password"));
    });
});

describe("POST /logout", () => {

    it("ends the session and clears the cookie", async () => {
        const client = await loginAs(STUDENT);
        assert.equal((await client.get("/api/me")).json.loggedIn, true);

        const res = await client.post("/logout");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, { success: true });
        assert.equal(client.cookies.has("connect.sid"), false);
        assert.equal((await client.get("/api/me")).json.loggedIn, false);
    });

    it("also works when nobody is logged in", async () => {
        const res = await newClient().post("/logout");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, { success: true });
    });

    it("an old cookie no longer works after logout", async () => {
        const client = await loginAs(STUDENT);
        const oldCookie = client.cookieHeader;

        await client.post("/logout");

        const stolen = newClient();
        stolen.cookies.set("connect.sid", oldCookie.split("=")[1]);

        assert.equal((await stolen.get("/api/me")).json.loggedIn, false);
    });
});

// ============================================================
// REGISTER
// ============================================================

describe("POST /register", () => {

    const valid = {
        name: "Új Felhasználó",
        email: "uj@example.com",
        password: "Titkos#123",
        password_confirm: "Titkos#123",
        role: "STUDENT"
    };

    it("requires every field", async () => {
        for (const field of Object.keys(valid)) {
            const form = { ...valid };
            delete form[field];

            const res = await newClient().post("/register", { form });

            assert.equal(res.status, 400, `missing ${field}`);
            assert.equal(res.text, "Minden mezőt ki kell tölteni.");
        }

        assert.equal(db.queries.length, 0);
    });

    it("rejects two different passwords", async () => {
        const res = await newClient().post("/register", {
            form: { ...valid, password_confirm: "Mas#123" }
        });

        assert.equal(res.status, 400);
        assert.equal(res.text, "A két jelszó nem egyezik.");
    });

    it("rejects roles other than STUDENT and TUTOR (no self-made admins)", async () => {
        for (const role of ["ADMIN", "student", "", "tutor"]) {
            const res = await newClient().post("/register", { form: { ...valid, role } });

            assert.equal(res.status, 400, `role "${role}"`);
        }

        const res = await newClient().post("/register", { form: { ...valid, role: "ADMIN" } });
        assert.equal(res.text, "Érvénytelen szerepkör.");
        assert.equal(db.queries.length, 0);
    });

    it("creates the account, stores only a hash and logs the user in", async () => {
        db.when(/INSERT INTO users/, () => ({ insertId: 42 }));
        const client = newClient();

        const res = await client.post("/register", { form: valid });

        assert.equal(res.status, 302);
        assert.equal(res.headers.get("location"), "/index");

        const [insert] = db.queriesMatching(/INSERT INTO users/);
        const [name, email, hash, role, bio, rate] = insert.params;
        assert.equal(name, valid.name);
        assert.equal(email, valid.email);
        assert.match(hash, /^\$argon2id\$/);
        assert.notEqual(hash, valid.password);
        assert.equal(await argon2.verify(hash, valid.password), true);
        assert.equal(role, "STUDENT");
        assert.equal(bio, "");
        assert.equal(rate, 0);

        const me = await client.get("/api/me");
        assert.deepEqual(me.json, {
            loggedIn: true,
            user: { id: 42, name: valid.name, email: valid.email, role: "STUDENT" }
        });
    });

    it("can register a TUTOR", async () => {
        db.when(/INSERT INTO users/, () => ({ insertId: 7 }));
        const client = newClient();

        await client.post("/register", { form: { ...valid, role: "TUTOR" } });

        assert.equal((await client.get("/api/me")).json.user.role, "TUTOR");
    });

    it("answers 409 for an e-mail that is already registered", async () => {
        db.when(/INSERT INTO users/, () => {
            throw Object.assign(new Error("dup"), {
                code: "ER_DUP_ENTRY",
                sqlMessage: "Duplicate entry 'uj@example.com' for key 'users.email'"
            });
        });

        const res = await newClient().post("/register", { form: valid });

        assert.equal(res.status, 409);
        assert.equal(res.text, "Ez az e-mail cím már regisztrálva van.");
    });

    it("answers 500 on any other database error", async () => {
        db.when(/INSERT INTO users/, () => {
            throw new Error("connection lost");
        });

        const res = await newClient().post("/register", { form: valid });

        assert.equal(res.status, 500);
        assert.match(res.text, /^Adatbázis hiba/);
    });

    it("answers 500 when hashing fails and does not touch the database", async () => {
        mock.method(argon2, "hash", async () => {
            throw new Error("no memory");
        });

        const res = await newClient().post("/register", { form: valid });
        mock.restoreAll();

        assert.equal(res.status, 500);
        assert.equal(res.text, "Nem sikerült feldolgozni a jelszót.");
        assert.equal(db.queries.length, 0);
    });
});

// ============================================================
// LOGIN
// ============================================================

describe("POST /login", () => {

    it("requires e-mail and password", async () => {
        for (const form of [{}, { email: "a@b.hu" }, { password: "x" }]) {
            const res = await newClient().post("/login", { form });

            assert.equal(res.status, 400);
            assert.equal(res.text, "E-mail és jelszó szükséges.");
        }

        assert.equal(db.queries.length, 0);
    });

    it("looks the user up with a parameterised query (no SQL injection)", async () => {
        db.when(/FROM users\s+WHERE email = \?/, () => []);
        const evil = "x' OR '1'='1";

        await newClient().post("/login", { form: { email: evil, password: "x" } });

        const [query] = db.queries;
        assert.deepEqual(query.params, [evil]);
        assert.ok(!query.sql.includes(evil));
    });

    it("rejects an unknown e-mail with 401", async () => {
        db.when(/FROM users\s+WHERE email = \?/, () => []);

        const res = await newClient().post("/login", {
            form: { email: "senki@example.com", password: PASSWORD }
        });

        assert.equal(res.status, 401);
        assert.equal(res.text, "Hibás e-mail vagy jelszó.");
    });

    it("rejects a wrong password with the same message (no user enumeration)", async () => {
        db.when(/FROM users\s+WHERE email = \?/, () => [{ ...STUDENT, password_hash: passwordHash }]);
        const client = newClient();

        const wrong = await client.post("/login", {
            form: { email: STUDENT.email, password: "Rossz#999" }
        });

        db.when(/FROM users\s+WHERE email = \?/, () => []);
        const unknown = await client.post("/login", {
            form: { email: "senki@example.com", password: "Rossz#999" }
        });

        assert.equal(wrong.status, 401);
        assert.equal(wrong.text, unknown.text);
        assert.equal((await client.get("/api/me")).json.loggedIn, false);
    });

    it("logs in with the right password and redirects to /index", async () => {
        db.when(/FROM users\s+WHERE email = \?/, () => [{ ...TUTOR, password_hash: passwordHash }]);
        const client = newClient();

        const res = await client.post("/login", {
            form: { email: TUTOR.email, password: PASSWORD }
        });

        assert.equal(res.status, 302);
        assert.equal(res.headers.get("location"), "/index");
        assert.ok(client.cookies.has("connect.sid"));

        const me = await client.get("/api/me");
        assert.deepEqual(me.json.user, {
            id: 2,
            name: "Tanár Tamás",
            email: "tamas@example.com",
            role: "TUTOR"
        });
    });

    it("sets a HttpOnly, SameSite=Lax session cookie that lasts 7 days", async () => {
        db.when(/FROM users\s+WHERE email = \?/, () => [{ ...STUDENT, password_hash: passwordHash }]);

        const res = await newClient().post("/login", {
            form: { email: STUDENT.email, password: PASSWORD }
        });

        const cookie = res.headers.getSetCookie().find((c) => c.startsWith("connect.sid="));
        assert.match(cookie, /HttpOnly/i);
        assert.match(cookie, /SameSite=Lax/i);
        assert.match(cookie, /Max-Age=604800|Expires=/i);
    });

    it("does not create a session for a visitor who fails to log in", async () => {
        db.when(/FROM users\s+WHERE email = \?/, () => []);
        const client = newClient();

        await client.post("/login", { form: { email: "a@b.hu", password: "x" } });

        assert.equal(client.cookies.size, 0);
    });

    it("answers 500 on a database error", async () => {
        db.when(/FROM users\s+WHERE email = \?/, () => {
            throw new Error("db down");
        });

        const res = await newClient().post("/login", {
            form: { email: STUDENT.email, password: PASSWORD }
        });

        assert.equal(res.status, 500);
        assert.equal(res.text, "Adatbázis hiba.");
    });
});

// ============================================================
// PUBLIC DATA: subjects, tutors, reviews, stats
// ============================================================

describe("GET /api/subjects", () => {

    it("returns every subject", async () => {
        const subjects = [{ id: 1, name: "Matek" }, { id: 2, name: "Angol" }];
        db.when(/SELECT \* FROM subjects/, () => subjects);

        const res = await newClient().get("/api/subjects");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, subjects);
    });

    it("answers 500 on a database error", async () => {
        db.when(/FROM subjects/, () => {
            throw new Error("boom");
        });

        const res = await newClient().get("/api/subjects");

        assert.equal(res.status, 500);
        assert.deepEqual(res.json, { error: "Adatbázis hiba" });
    });
});

describe("GET /api/tutors", () => {

    it("returns the tutor list from the database", async () => {
        const tutors = [{ id: 2, full_name: "Tanár Tamás", subjects: "Matek", hourly_rate: 4000 }];
        db.when(/FROM users u/, () => tutors);

        const res = await newClient().get("/api/tutors");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, tutors);
        assert.match(db.queries[0].sql, /WHERE u\.role = 'TUTOR'/);
    });

    it("works without being logged in", async () => {
        db.when(/FROM users u/, () => []);

        assert.equal((await newClient().get("/api/tutors")).status, 200);
    });

    it("answers 500 on a database error", async () => {
        db.when(/FROM users u/, () => {
            throw new Error("boom");
        });

        const res = await newClient().get("/api/tutors");

        assert.equal(res.status, 500);
        assert.deepEqual(res.json, { error: "Adatbázis hiba" });
    });
});

describe("GET /api/tutors/:id/reviews", () => {

    it("returns the reviews of the tutor", async () => {
        const reviews = [{ id: 1, rating: "5", comment: "Szuper", reviewer_name: "Dóra" }];
        db.when(/FROM reviews r/, () => reviews);

        const res = await newClient().get("/api/tutors/2/reviews");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, reviews);
        assert.deepEqual(db.queries[0].params, [2]);
    });

    it("rejects an id that is not an integer", async () => {
        for (const id of ["abc", "1.5", "NaN"]) {
            const res = await newClient().get(`/api/tutors/${id}/reviews`);

            assert.equal(res.status, 400, id);
            assert.deepEqual(res.json, { error: "Érvénytelen oktató ID." });
        }

        assert.equal(db.queries.length, 0);
    });

    it("answers 500 on a database error", async () => {
        db.when(/FROM reviews r/, () => {
            throw new Error("boom");
        });

        const res = await newClient().get("/api/tutors/2/reviews");

        assert.equal(res.status, 500);
        assert.equal(res.json.error, "Adatbázis hiba.");
    });
});

describe("GET /api/stats", () => {

    it("returns the counts and the satisfaction percentage", async () => {
        db.when(/tutor_count/, () => [{ tutor_count: 12, booking_count: 80, avg_rating: "4.5" }]);

        const res = await newClient().get("/api/stats");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, { tutors: 12, bookings: 80, satisfaction: 90 });
    });

    it("rounds the satisfaction to a whole percent", async () => {
        db.when(/tutor_count/, () => [{ tutor_count: 1, booking_count: 1, avg_rating: "4.33" }]);

        assert.equal((await newClient().get("/api/stats")).json.satisfaction, 87);
    });

    it("is 0% when there are no reviews", async () => {
        db.when(/tutor_count/, () => [{ tutor_count: 0, booking_count: 0, avg_rating: 0 }]);

        assert.equal((await newClient().get("/api/stats")).json.satisfaction, 0);
    });

    it("answers 500 on a database error", async () => {
        db.when(/tutor_count/, () => {
            throw new Error("boom");
        });

        const res = await newClient().get("/api/stats");

        assert.equal(res.status, 500);
        assert.deepEqual(res.json, { error: "Adatbázis hiba" });
    });
});

// ============================================================
// CONVERSATIONS
// ============================================================

describe("GET /api/conversations", () => {

    it("requires login", async () => {
        const res = await newClient().get("/api/conversations");

        assert.equal(res.status, 401);
        assert.deepEqual(res.json, { error: "Nincs bejelentkezve" });
    });

    it("returns the conversations of the logged-in user", async () => {
        const rows = [{ id: 9, student_id: 1, tutor_id: 2, other_user_name: "Tanár Tamás", unread_count: 3 }];
        db.when(/WHERE conversations\.student_id = \?/, () => rows);
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/conversations");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, rows);

        // the user id always comes from the session
        const [query] = db.queriesMatching(/WHERE conversations\.student_id/);
        assert.deepEqual(query.params, [1, 1, 1, 1]);
    });

    it("answers 500 on a database error", async () => {
        db.when(/WHERE conversations\.student_id = \?/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/conversations");

        assert.equal(res.status, 500);
    });
});

describe("POST /api/conversations", () => {

    const conversation = { id: 9, student_id: 1, tutor_id: 2, other_user_name: "Tanár Tamás" };

    it("requires login", async () => {
        const res = await newClient().post("/api/conversations", { json: { tutor_id: 2 } });

        assert.equal(res.status, 401);
    });

    it("only lets students start a conversation", async () => {
        const client = await loginAs(TUTOR);

        const res = await client.post("/api/conversations", { json: { tutor_id: 3 } });

        assert.equal(res.status, 403);
        assert.deepEqual(res.json, { error: "Csak diák indíthat új beszélgetést" });
    });

    it("rejects an invalid tutor id", async () => {
        const client = await loginAs(STUDENT);

        for (const tutor_id of [undefined, "abc", 0, -3, 1.5]) {
            const res = await client.post("/api/conversations", { json: { tutor_id } });

            assert.equal(res.status, 400, String(tutor_id));
            assert.deepEqual(res.json, { error: "Érvénytelen oktató" });
        }
    });

    it("answers 404 when the tutor does not exist", async () => {
        db.when(/role = 'TUTOR'/, () => []);
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations", { json: { tutor_id: 99 } });

        assert.equal(res.status, 404);
        assert.deepEqual(res.json, { error: "Az oktató nem található" });
    });

    it("creates a new conversation (201)", async () => {
        db.when(/role = 'TUTOR'/, () => [{ id: 2 }]);
        db.when(/WHERE student_id = \? AND tutor_id = \?/, () => []);
        db.when(/INSERT INTO conversations/, () => ({ insertId: 9 }));
        db.when(/WHERE conversations\.id = \?/, () => [conversation]);
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations", { json: { tutor_id: 2 } });

        assert.equal(res.status, 201);
        assert.deepEqual(res.json, conversation);
        assert.deepEqual(db.queriesMatching(/INSERT INTO conversations/)[0].params, [1, 2]);
    });

    it("returns the existing conversation instead of creating a duplicate (200)", async () => {
        db.when(/role = 'TUTOR'/, () => [{ id: 2 }]);
        db.when(/WHERE student_id = \? AND tutor_id = \?/, () => [{ id: 9 }]);
        db.when(/WHERE conversations\.id = \?/, () => [conversation]);
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations", { json: { tutor_id: 2 } });

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, conversation);
        assert.equal(db.queriesMatching(/INSERT INTO conversations/).length, 0);
    });

    it("answers 500 when the database fails", async () => {
        db.when(/role = 'TUTOR'/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations", { json: { tutor_id: 2 } });

        assert.equal(res.status, 500);
    });
});

describe("POST /api/conversations/:id/read", () => {

    it("requires login", async () => {
        assert.equal((await newClient().post("/api/conversations/9/read")).status, 401);
    });

    it("refuses users who are not part of the conversation", async () => {
        db.when(/SELECT id FROM conversations\s+WHERE id = \?/, () => []);
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations/9/read");

        assert.equal(res.status, 403);
        assert.equal(db.queriesMatching(/UPDATE messages/).length, 0);
    });

    it("marks only the other person's messages as read", async () => {
        db.when(/SELECT id FROM conversations\s+WHERE id = \?/, () => [{ id: 9 }]);
        db.when(/UPDATE messages\s+SET is_read = 1/, () => ({ affectedRows: 3 }));
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations/9/read");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, { updated: 3 });

        const [update] = db.queriesMatching(/UPDATE messages/);
        assert.match(update.sql, /sender_id <> \?/);
        assert.deepEqual(update.params, ["9", 1]);
    });

    it("answers 500 when the database fails", async () => {
        db.when(/SELECT id FROM conversations\s+WHERE id = \?/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        assert.equal((await client.post("/api/conversations/9/read")).status, 500);
    });
});

// ============================================================
// MESSAGES
// ============================================================

describe("GET /api/conversations/:id/messages", () => {

    it("requires login", async () => {
        assert.equal((await newClient().get("/api/conversations/9/messages")).status, 401);
    });

    it("returns the messages, restricted to conversations of the user", async () => {
        const rows = [{ id: 1, content: "Szia" }, { id: 2, content: "Szervusz" }];
        db.when(/ORDER BY messages\.created_at ASC/, () => rows);
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/conversations/9/messages");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, rows);
        assert.deepEqual(db.queriesMatching(/ORDER BY messages/)[0].params, ["9", 1, 1]);
    });

    it("answers 500 when the database fails", async () => {
        db.when(/ORDER BY messages\.created_at ASC/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        assert.equal((await client.get("/api/conversations/9/messages")).status, 500);
    });
});

describe("POST /api/conversations/:id/messages", () => {

    const saved = { id: 50, conversation_id: 9, sender_id: 1, content: "Szia!" };

    function mockSaving() {
        db.when(/SELECT id, student_id, tutor_id FROM conversations/, () => [
            { id: 9, student_id: 1, tutor_id: 2 }
        ]);
        db.when(/INSERT INTO messages/, () => ({ insertId: 50 }));
        db.when(/SELECT \* FROM messages WHERE id = \?/, () => [saved]);
    }

    it("requires login", async () => {
        const res = await newClient().post("/api/conversations/9/messages", { json: { content: "x" } });

        assert.equal(res.status, 401);
    });

    it("rejects empty and whitespace-only messages", async () => {
        const client = await loginAs(STUDENT);

        for (const content of [undefined, "", "   \n\t "]) {
            const res = await client.post("/api/conversations/9/messages", { json: { content } });

            assert.equal(res.status, 400);
            assert.deepEqual(res.json, { error: "Az üzenet nem lehet üres" });
        }
    });

    it("rejects messages longer than 2000 characters but accepts exactly 2000", async () => {
        mockSaving();
        const client = await loginAs(STUDENT);

        const tooLong = await client.post("/api/conversations/9/messages", {
            json: { content: "a".repeat(2001) }
        });
        const maxLength = await client.post("/api/conversations/9/messages", {
            json: { content: "a".repeat(2000) }
        });

        assert.equal(tooLong.status, 400);
        assert.deepEqual(tooLong.json, { error: "Az üzenet túl hosszú" });
        assert.equal(maxLength.status, 201);
    });

    it("refuses users who are not part of the conversation", async () => {
        db.when(/SELECT id, student_id, tutor_id FROM conversations/, () => []);
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations/9/messages", { json: { content: "Szia" } });

        assert.equal(res.status, 403);
        assert.equal(db.queriesMatching(/INSERT INTO messages/).length, 0);
    });

    it("saves the trimmed message with the sender taken from the session", async () => {
        mockSaving();
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations/9/messages", {
            json: { content: "  Szia!  ", sender_id: 999 }
        });

        assert.equal(res.status, 201);
        assert.deepEqual(res.json, saved);
        assert.deepEqual(db.queriesMatching(/INSERT INTO messages/)[0].params, ["9", 1, "Szia!"]);
    });

    it("pushes the new message to the other participant over WebSocket", async () => {
        mockSaving();
        const student = await loginAs(STUDENT);
        const tutor = await loginAs(TUTOR);
        const tutorSocket = await openSocket(tutor);
        const studentSocket = await openSocket(student);

        try {
            await student.post("/api/conversations/9/messages", { json: { content: "Szia!" } });

            assert.deepEqual(await nextMessage(tutorSocket), { type: "new_message", message: saved });
            await expectNoMessage(studentSocket);
        } finally {
            tutorSocket.close();
            studentSocket.close();
        }
    });

    it("answers 500 when saving fails", async () => {
        db.when(/SELECT id, student_id, tutor_id FROM conversations/, () => [
            { id: 9, student_id: 1, tutor_id: 2 }
        ]);
        db.when(/INSERT INTO messages/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations/9/messages", { json: { content: "Szia" } });

        assert.equal(res.status, 500);
    });
});

describe("DELETE /api/messages/:id (retract)", () => {

    const row = (extra = {}) => ({
        id: 50,
        conversation_id: 9,
        attachment_path: null,
        student_id: 1,
        tutor_id: 2,
        ...extra
    });

    it("requires login", async () => {
        assert.equal((await newClient().delete("/api/messages/50")).status, 401);
    });

    it("rejects an invalid id", async () => {
        const client = await loginAs(STUDENT);

        for (const id of ["abc", "0", "-4", "1.5"]) {
            const res = await client.delete("/api/messages/" + id);

            assert.equal(res.status, 400, id);
            assert.deepEqual(res.json, { error: "Érvénytelen üzenet" });
        }
    });

    it("answers 404 for a message that is not yours or already retracted", async () => {
        db.when(/messages\.attachment_path,\s+conversations\.student_id/, () => []);
        const client = await loginAs(STUDENT);

        const res = await client.delete("/api/messages/50");

        assert.equal(res.status, 404);
        // the lookup is restricted to the sender
        assert.deepEqual(db.queries[db.queries.length - 1].params, [50, 1]);
    });

    it("blanks the message, keeps the row and notifies both sides", async () => {
        db.when(/messages\.attachment_path,\s+conversations\.student_id/, () => [row()]);
        db.when(/UPDATE messages\s+SET content = ''/, () => ({ affectedRows: 1 }));
        const student = await loginAs(STUDENT);
        const tutor = await loginAs(TUTOR);
        const tutorSocket = await openSocket(tutor);
        const studentSocket = await openSocket(student);

        try {
            const res = await student.delete("/api/messages/50");

            assert.equal(res.status, 200);
            assert.deepEqual(res.json, { success: true, message_id: 50 });

            const [update] = db.queriesMatching(/UPDATE messages/);
            assert.match(update.sql, /deleted_at = NOW\(\)/);
            assert.deepEqual(update.params, [50, 1]);

            const event = { type: "message_retracted", conversation_id: 9, message_id: 50 };
            assert.deepEqual(await nextMessage(tutorSocket), event);
            assert.deepEqual(await nextMessage(studentSocket), event);
        } finally {
            tutorSocket.close();
            studentSocket.close();
        }
    });

    it("also deletes the attached file from the disk", async () => {
        const file = "retract-test-file.txt";
        const filePath = path.join(UPLOAD_DIR, file);
        fs.writeFileSync(filePath, "bye");

        db.when(/messages\.attachment_path,\s+conversations\.student_id/, () => [
            row({ attachment_path: file })
        ]);
        db.when(/UPDATE messages\s+SET content = ''/, () => ({ affectedRows: 1 }));
        const client = await loginAs(STUDENT);

        const res = await client.delete("/api/messages/50");

        assert.equal(res.status, 200);
        assert.ok(await waitUntil(() => !fs.existsSync(filePath)), "file should be removed");
    });

    it("answers 500 when the database fails", async () => {
        db.when(/messages\.attachment_path,\s+conversations\.student_id/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        assert.equal((await client.delete("/api/messages/50")).status, 500);
    });
});

describe("PATCH /api/messages/:id (edit)", () => {

    const row = { id: 50, conversation_id: 9, student_id: 1, tutor_id: 2 };

    it("requires login", async () => {
        const res = await newClient().patch("/api/messages/50", { json: { content: "x" } });

        assert.equal(res.status, 401);
    });

    it("rejects an invalid id", async () => {
        const client = await loginAs(STUDENT);

        const res = await client.patch("/api/messages/abc", { json: { content: "x" } });

        assert.equal(res.status, 400);
        assert.deepEqual(res.json, { error: "Érvénytelen üzenet" });
    });

    it("rejects empty and too long content", async () => {
        const client = await loginAs(STUDENT);

        const empty = await client.patch("/api/messages/50", { json: { content: "  " } });
        const long = await client.patch("/api/messages/50", { json: { content: "a".repeat(2001) } });

        assert.equal(empty.status, 400);
        assert.deepEqual(empty.json, { error: "Az üzenet nem lehet üres" });
        assert.equal(long.status, 400);
        assert.deepEqual(long.json, { error: "Az üzenet túl hosszú" });
    });

    it("answers 404 when the message is not yours, retracted or has an attachment", async () => {
        db.when(/messages\.conversation_id,\s+conversations\.student_id/, () => []);
        const client = await loginAs(STUDENT);

        const res = await client.patch("/api/messages/50", { json: { content: "Új" } });

        assert.equal(res.status, 404);
        assert.match(db.queries[db.queries.length - 1].sql, /attachment_path IS NULL/);
    });

    it("updates the text, stamps edited_at and notifies both sides", async () => {
        db.when(/messages\.conversation_id,\s+conversations\.student_id/, () => [row]);
        db.when(/UPDATE messages\s+SET content = \?, edited_at = NOW\(\)/, () => ({ affectedRows: 1 }));
        const student = await loginAs(STUDENT);
        const tutor = await loginAs(TUTOR);
        const tutorSocket = await openSocket(tutor);
        const studentSocket = await openSocket(student);

        try {
            const res = await student.patch("/api/messages/50", { json: { content: "  Javított  " } });

            assert.equal(res.status, 200);
            assert.deepEqual(res.json, { success: true, message_id: 50, content: "Javított" });
            assert.deepEqual(db.queriesMatching(/UPDATE messages/)[0].params, ["Javított", 50, 1]);

            const event = { type: "message_edited", conversation_id: 9, message_id: 50 };
            assert.deepEqual(await nextMessage(tutorSocket), event);
            assert.deepEqual(await nextMessage(studentSocket), event);
        } finally {
            tutorSocket.close();
            studentSocket.close();
        }
    });

    it("answers 500 when the update fails", async () => {
        db.when(/messages\.conversation_id,\s+conversations\.student_id/, () => [row]);
        db.when(/UPDATE messages/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        const res = await client.patch("/api/messages/50", { json: { content: "Új" } });

        assert.equal(res.status, 500);
    });
});

// ============================================================
// ATTACHMENTS
// ============================================================

describe("POST /api/conversations/:id/attachments", () => {

    const savedMessage = { id: 60, conversation_id: 9, sender_id: 1, content: "jegyzet.pdf" };

    function mockSaving() {
        db.when(/SELECT id, student_id, tutor_id FROM conversations/, () => [
            { id: 9, student_id: 1, tutor_id: 2 }
        ]);
        db.when(/INSERT INTO messages/, () => ({ insertId: 60 }));
        db.when(/SELECT id, conversation_id, sender_id, content, is_read/, () => [savedMessage]);
    }

    it("requires login", async () => {
        const res = await newClient().post("/api/conversations/9/attachments", {
            body: multipart("a.txt", "x")
        });

        assert.equal(res.status, 401);
    });

    it("answers 400 when no file is sent", async () => {
        const client = await loginAs(STUDENT);

        const res = await client.post("/api/conversations/9/attachments", { body: new FormData() });

        assert.equal(res.status, 400);
        assert.deepEqual(res.json, { error: "Nincs kiválasztott fájl." });
    });

    it("refuses file types that are not on the allow-list", async () => {
        const client = await loginAs(STUDENT);
        const before = listUploads();

        for (const name of ["virus.exe", "script.js", "page.html", "noextension"]) {
            const res = await client.post("/api/conversations/9/attachments", {
                body: multipart(name, "data")
            });

            assert.equal(res.status, 400, name);
            assert.deepEqual(res.json, { error: "Ez a fájltípus nem engedélyezett." });
        }

        assert.deepEqual(listUploads(), before);
    });

    it("answers 413 for files bigger than 10 MB and leaves nothing on disk", async () => {
        const client = await loginAs(STUDENT);
        const before = listUploads();

        const res = await client.post("/api/conversations/9/attachments", {
            body: multipart("nagy.pdf", Buffer.alloc(10 * 1024 * 1024 + 1024))
        });

        assert.equal(res.status, 413);
        assert.equal(res.json.error, "A fájl túl nagy (legfeljebb 10 MB).");
        assert.deepEqual(listUploads(), before);
    });

    it("refuses users who are not part of the conversation and deletes the upload", async () => {
        db.when(/SELECT id, student_id, tutor_id FROM conversations/, () => []);
        const client = await loginAs(STUDENT);
        const before = listUploads();

        const res = await client.post("/api/conversations/9/attachments", {
            body: multipart("jegyzet.pdf", "pdf-bytes")
        });

        assert.equal(res.status, 403);
        assert.ok(await waitUntil(() => listUploads().length === before.length));
    });

    it("stores the file under a random name and returns the message without the server path", async () => {
        mockSaving();
        const client = await loginAs(STUDENT);
        const before = listUploads();

        const res = await client.post("/api/conversations/9/attachments", {
            body: multipart("jegyzet.pdf", "pdf-bytes")
        });

        assert.equal(res.status, 201);
        assert.deepEqual(res.json, savedMessage);
        assert.ok(!res.text.includes("attachment_path"));

        const created = listUploads().filter((f) => !before.includes(f));
        assert.equal(created.length, 1);
        assert.match(created[0], /^[0-9a-f]{32}\.pdf$/);
        assert.equal(fs.readFileSync(path.join(UPLOAD_DIR, created[0]), "utf8"), "pdf-bytes");

        const [, sender, content, name, storedAs, type, size] =
            db.queriesMatching(/INSERT INTO messages/)[0].params;
        assert.equal(sender, 1);
        assert.equal(content, "jegyzet.pdf");          // no caption -> file name
        assert.equal(name, "jegyzet.pdf");
        assert.equal(storedAs, created[0]);
        assert.equal(type, "application/pdf");          // decided by extension, not by the browser
        assert.equal(size, 9);
    });

    it("uses the caption as the message text when there is one", async () => {
        mockSaving();
        const client = await loginAs(STUDENT);

        await client.post("/api/conversations/9/attachments", {
            body: multipart("kep.png", "png", { content: "  Nézd meg!  " })
        });

        assert.equal(db.queriesMatching(/INSERT INTO messages/)[0].params[2], "Nézd meg!");
    });

    it("keeps accents in the file name and strips path parts and dangerous characters", async () => {
        mockSaving();
        const client = await loginAs(STUDENT);

        await client.post("/api/conversations/9/attachments", {
            body: multipart("..\\..\\árvíztűrő<>.txt", "x")
        });

        const name = db.queriesMatching(/INSERT INTO messages/)[0].params[3];
        assert.equal(name, "árvíztűrő__.txt");
    });

    it("tells the other participant about the attachment over WebSocket", async () => {
        mockSaving();
        const student = await loginAs(STUDENT);
        const tutor = await loginAs(TUTOR);
        const tutorSocket = await openSocket(tutor);

        try {
            await student.post("/api/conversations/9/attachments", {
                body: multipart("jegyzet.pdf", "x")
            });

            assert.deepEqual(await nextMessage(tutorSocket), {
                type: "new_message",
                message: savedMessage
            });
        } finally {
            tutorSocket.close();
        }
    });

    it("deletes the file again when the database insert fails", async () => {
        db.when(/SELECT id, student_id, tutor_id FROM conversations/, () => [
            { id: 9, student_id: 1, tutor_id: 2 }
        ]);
        db.when(/INSERT INTO messages/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);
        const before = listUploads();

        const res = await client.post("/api/conversations/9/attachments", {
            body: multipart("jegyzet.pdf", "x")
        });

        assert.equal(res.status, 500);
        assert.ok(await waitUntil(() => listUploads().length === before.length));
    });
});

describe("GET /api/messages/:id/attachment", () => {

    const stored = "download-test-file.bin";
    const storedPath = path.join(UPLOAD_DIR, stored);

    before(() => fs.writeFileSync(storedPath, "file-content"));
    after(() => fs.rmSync(storedPath, { force: true }));

    function mockRow(extra) {
        db.when(/attachment_path IS NOT NULL/, () => [
            { attachment_name: "jegyzet.pdf", attachment_path: stored, attachment_type: "application/pdf", ...extra }
        ]);
    }

    it("requires login", async () => {
        assert.equal((await newClient().get("/api/messages/60/attachment")).status, 401);
    });

    it("answers 404 when the message has no attachment or the user is not a participant", async () => {
        db.when(/attachment_path IS NOT NULL/, () => []);
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/messages/60/attachment");

        assert.equal(res.status, 404);
        assert.deepEqual(res.json, { error: "A fájl nem található" });
        assert.deepEqual(db.queriesMatching(/attachment_path IS NOT NULL/)[0].params, ["60", 1, 1]);
    });

    it("sends documents as a download with safe headers", async () => {
        mockRow();
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/messages/60/attachment");

        assert.equal(res.status, 200);
        assert.equal(res.text, "file-content");
        assert.equal(res.headers.get("content-type"), "application/pdf");
        assert.equal(res.headers.get("x-content-type-options"), "nosniff");
        assert.equal(
            res.headers.get("content-disposition"),
            "attachment; filename*=UTF-8''jegyzet.pdf"
        );
    });

    it("shows images inline", async () => {
        mockRow({ attachment_name: "kép.png", attachment_type: "image/png" });
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/messages/60/attachment");

        assert.equal(res.headers.get("content-type"), "image/png");
        assert.equal(
            res.headers.get("content-disposition"),
            "inline; filename*=UTF-8''k%C3%A9p.png"
        );
    });

    it("percent-encodes characters that could break the header", async () => {
        mockRow({ attachment_name: "a'b(c)*d.pdf" });
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/messages/60/attachment");

        assert.equal(
            res.headers.get("content-disposition"),
            "attachment; filename*=UTF-8''a%27b%28c%29%2Ad.pdf"
        );
    });

    it("cannot be tricked into reading files outside the upload folder", async () => {
        mockRow({ attachment_path: "../../../../etc/passwd" });
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/messages/60/attachment");

        assert.equal(res.status, 404);
        assert.ok(!res.text.includes("root:"));
    });

    it("answers 404 when the file has vanished from the disk", async () => {
        mockRow({ attachment_path: "does-not-exist.pdf" });
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/messages/60/attachment");

        assert.equal(res.status, 404);
        assert.deepEqual(res.json, { error: "A fájl nem található" });
    });

    it("answers 500 when the database fails", async () => {
        db.when(/attachment_path IS NOT NULL/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        assert.equal((await client.get("/api/messages/60/attachment")).status, 500);
    });
});

// ============================================================
// WEBSOCKET
// ============================================================

describe("WebSocket", () => {

    it("rejects a connection without a session (401)", async () => {
        const status = await new Promise((resolve) => {
            const ws = new WebSocket(app.baseUrl.replace("http", "ws") + "/");
            ws.on("unexpected-response", (req, res) => resolve(res.statusCode));
            ws.on("error", () => {});
        });

        assert.equal(status, 401);
    });

    it("rejects a connection with a bogus session cookie", async () => {
        const status = await new Promise((resolve) => {
            const ws = new WebSocket(app.baseUrl.replace("http", "ws") + "/", {
                headers: { Cookie: "connect.sid=s%3Anonsense.invalid" }
            });
            ws.on("unexpected-response", (req, res) => resolve(res.statusCode));
            ws.on("error", () => {});
        });

        assert.equal(status, 401);
    });

    it("accepts a logged-in user", async () => {
        const client = await loginAs(STUDENT);

        const ws = await openSocket(client);

        assert.equal(ws.readyState, WebSocket.OPEN);
        ws.close();
    });

    it("forwards the typing indicator to the other participant only", async () => {
        db.when(/SELECT student_id, tutor_id FROM conversations/, () => [{ student_id: 1, tutor_id: 2 }]);
        const student = await loginAs(STUDENT);
        const tutor = await loginAs(TUTOR);
        const studentSocket = await openSocket(student);
        const tutorSocket = await openSocket(tutor);

        try {
            studentSocket.send(JSON.stringify({ type: "typing", conversation_id: 9 }));

            assert.deepEqual(await nextMessage(tutorSocket), { type: "typing", conversation_id: 9 });
            await expectNoMessage(studentSocket);

            // the permission check uses the user from the session
            assert.deepEqual(db.queriesMatching(/SELECT student_id, tutor_id/)[0].params, [9, 1, 1]);
        } finally {
            studentSocket.close();
            tutorSocket.close();
        }
    });

    it("delivers to every open tab of the recipient", async () => {
        db.when(/SELECT student_id, tutor_id FROM conversations/, () => [{ student_id: 1, tutor_id: 2 }]);
        const student = await loginAs(STUDENT);
        const tutor = await loginAs(TUTOR);
        const studentSocket = await openSocket(student);
        const tab1 = await openSocket(tutor);
        const tab2 = await openSocket(tutor);

        try {
            studentSocket.send(JSON.stringify({ type: "typing", conversation_id: 9 }));

            assert.equal((await nextMessage(tab1)).type, "typing");
            assert.equal((await nextMessage(tab2)).type, "typing");
        } finally {
            studentSocket.close();
            tab1.close();
            tab2.close();
        }
    });

    it("ignores typing signals for conversations the user is not part of", async () => {
        db.when(/SELECT student_id, tutor_id FROM conversations/, () => []);
        const student = await loginAs(STUDENT);
        const tutor = await loginAs(TUTOR);
        const studentSocket = await openSocket(student);
        const tutorSocket = await openSocket(tutor);

        try {
            studentSocket.send(JSON.stringify({ type: "typing", conversation_id: 77 }));

            await expectNoMessage(tutorSocket);
        } finally {
            studentSocket.close();
            tutorSocket.close();
        }
    });

    it("ignores malformed, unknown and invalid messages without closing", async () => {
        const client = await loginAs(STUDENT);
        const ws = await openSocket(client);

        try {
            ws.send("this is not json");
            ws.send(JSON.stringify({ type: "something-else" }));
            ws.send(JSON.stringify({ type: "typing", conversation_id: "abc" }));
            ws.send(JSON.stringify(null));
            await sleep(150);

            assert.equal(ws.readyState, WebSocket.OPEN);
            assert.equal(db.queriesMatching(/SELECT student_id, tutor_id/).length, 0);
        } finally {
            ws.close();
        }
    });

    // An oversized frame makes `ws` emit an "error" event on the server-side
    // socket. main.js has no ws.on("error") handler, so in a real run this would be
    // an uncaught exception that kills the whole server. The helper below catches
    // such exceptions so the test process itself survives.
    async function sendOversizedFrame() {
        const client = await loginAs(STUDENT);
        const ws = await openSocket(client);
        const uncaught = [];

        const saved = process.listeners("uncaughtException");
        process.removeAllListeners("uncaughtException");
        process.on("uncaughtException", (err) => uncaught.push(err));

        try {
            const code = await new Promise((resolve) => {
                ws.on("close", resolve);
                ws.send("x".repeat(2048));
            });
            await sleep(100);

            return { code, uncaught };
        } finally {
            process.removeAllListeners("uncaughtException");
            saved.forEach((listener) => process.on("uncaughtException", listener));
        }
    }

    it("closes connections that send more than 1 KB", async () => {
        const { code } = await sendOversizedFrame();

        assert.equal(code, 1009);
    });

    it("survives an oversized message without an uncaught exception",
        { todo: "main.js has no ws.on('error') handler, so this crashes the server" },
        async () => {
            const { uncaught } = await sendOversizedFrame();

            assert.deepEqual(uncaught.map((e) => e.message), []);
        });

    it("stops delivering to a user after the socket is closed", async () => {
        db.when(/SELECT student_id, tutor_id FROM conversations/, () => [{ student_id: 1, tutor_id: 2 }]);
        const student = await loginAs(STUDENT);
        const tutor = await loginAs(TUTOR);
        const studentSocket = await openSocket(student);
        const tutorSocket = await openSocket(tutor);

        await new Promise((resolve) => {
            tutorSocket.on("close", resolve);
            tutorSocket.close();
        });
        await sleep(50);

        // must not throw or crash the server
        studentSocket.send(JSON.stringify({ type: "typing", conversation_id: 9 }));
        await sleep(100);

        assert.equal((await newClient().get("/api/me")).status, 200);
        studentSocket.close();
    });
});

// ============================================================
// ACCOUNT
// ============================================================

describe("GET /api/account", () => {

    it("requires login", async () => {
        const res = await newClient().get("/api/account");

        assert.equal(res.status, 401);
        assert.deepEqual(res.json, { error: "Nem vagy bejelentkezve." });
    });

    it("returns a student's data (no subjects, no password hash)", async () => {
        db.when(/SELECT id, full_name, email, role, bio/, () => [{ ...STUDENT, bio: null, password_hash: "secret" }]);
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/account");

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, {
            id: 1,
            name: "Diák Dóra",
            email: "dora@example.com",
            role: "STUDENT",
            bio: ""
        });
    });

    it("includes subjects and prices for a tutor", async () => {
        const subjects = [{ subject_id: 3, name: "Matek", hourly_rate: 4000 }];
        db.when(/SELECT id, full_name, email, role, bio/, () => [{ ...TUTOR, bio: "Sziasztok" }]);
        db.when(/SELECT ts\.subject_id/, () => subjects);
        const client = await loginAs(TUTOR);

        const res = await client.get("/api/account");

        assert.equal(res.status, 200);
        assert.equal(res.json.bio, "Sziasztok");
        assert.deepEqual(res.json.subjects, subjects);
    });

    it("answers 404 when the user no longer exists", async () => {
        db.when(/SELECT id, full_name, email, role, bio/, () => []);
        const client = await loginAs(STUDENT);

        assert.equal((await client.get("/api/account")).status, 404);
    });

    it("answers 500 when the database fails", async () => {
        db.when(/SELECT id, full_name, email, role, bio/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        const res = await client.get("/api/account");

        assert.equal(res.status, 500);
        assert.deepEqual(res.json, { error: "Adatbázis hiba." });
    });
});

describe("PUT /api/account", () => {

    const studentRow = { email: STUDENT.email, role: "STUDENT", password_hash: null };
    const tutorRow = { email: TUTOR.email, role: "TUTOR", password_hash: null };

    function mockCurrentUser(row) {
        db.when(/SELECT email, role, password_hash FROM users/, () => [
            { ...row, password_hash: passwordHash }
        ]);
        db.when(/UPDATE users SET/, () => ({ affectedRows: 1 }));
        db.when(/DELETE FROM tutor_subjects/, () => ({}));
        db.when(/INSERT INTO tutor_subjects/, () => ({}));
    }

    const studentBody = (extra = {}) => ({
        name: "Diák Dóra",
        email: STUDENT.email,
        ...extra
    });

    const tutorBody = (extra = {}) => ({
        name: "Tanár Tamás",
        email: TUTOR.email,
        bio: "Bemutatkozás",
        ...extra
    });

    it("requires login", async () => {
        const res = await newClient().put("/api/account", { json: studentBody() });

        assert.equal(res.status, 401);
    });

    describe("validation", () => {

        it("name must be 2-60 characters", async () => {
            const client = await loginAs(STUDENT);

            for (const name of ["", " a ", "x".repeat(61)]) {
                const res = await client.put("/api/account", { json: studentBody({ name }) });

                assert.equal(res.status, 400, JSON.stringify(name));
                assert.equal(res.json.error, "A név 2 és 60 karakter között legyen.");
            }
        });

        it("e-mail must be valid and at most 30 characters", async () => {
            const client = await loginAs(STUDENT);

            for (const email of ["", "nem-email", "a@b", "a b@c.hu", "x".repeat(25) + "@abc.hu"]) {
                const res = await client.put("/api/account", { json: studentBody({ email }) });

                assert.equal(res.status, 400, email);
                assert.match(res.json.error, /érvényes e-mail/);
            }
        });

        it("a new password needs 6+ characters, a digit and a special character", async () => {
            const client = await loginAs(STUDENT);

            for (const newPassword of ["Ab1!", "abcdefg!", "abcdefg1", "abc123def"]) {
                const res = await client.put("/api/account", {
                    json: studentBody({ newPassword, currentPassword: PASSWORD })
                });

                assert.equal(res.status, 400, newPassword);
                assert.match(res.json.error, /Az új jelszó legalább 6 karakter/);
            }
        });

        it("a tutor's bio is limited to 1000 characters", async () => {
            mockCurrentUser(tutorRow);
            const client = await loginAs(TUTOR);

            const res = await client.put("/api/account", {
                json: tutorBody({ bio: "a".repeat(1001) })
            });

            assert.equal(res.status, 400);
            assert.equal(res.json.error, "A bemutatkozás legfeljebb 1000 karakter lehet.");
        });

        it("a tutor can have at most 10 subjects", async () => {
            mockCurrentUser(tutorRow);
            const client = await loginAs(TUTOR);
            const subjects = Array.from({ length: 11 }, (_, i) => ({ subject_id: i + 1, hourly_rate: 1000 }));

            const res = await client.put("/api/account", { json: tutorBody({ subjects }) });

            assert.equal(res.status, 400);
            assert.match(res.json.error, /Legfeljebb 10 tantárgyat/);
        });

        it("rejects invalid subject ids, duplicates and bad prices", async () => {
            mockCurrentUser(tutorRow);
            db.when(/SELECT id FROM subjects/, () => [{ id: 1 }]);
            const client = await loginAs(TUTOR);

            const cases = [
                [[{ subject_id: "abc", hourly_rate: 1000 }], "Érvénytelen tantárgy."],
                [[{ subject_id: 0, hourly_rate: 1000 }], "Érvénytelen tantárgy."],
                [[null], "Érvénytelen tantárgy."],
                [[{ subject_id: 1, hourly_rate: 1000 }, { subject_id: 1, hourly_rate: 2000 }],
                    "Egy tantárgy csak egyszer szerepelhet."],
                [[{ subject_id: 1, hourly_rate: -1 }], "Az óradíj 0 és 100 000 Ft között legyen."],
                [[{ subject_id: 1, hourly_rate: 100001 }], "Az óradíj 0 és 100 000 Ft között legyen."],
                [[{ subject_id: 1, hourly_rate: 12.5 }], "Az óradíj 0 és 100 000 Ft között legyen."],
                [[{ subject_id: 1, hourly_rate: "sok" }], "Az óradíj 0 és 100 000 Ft között legyen."]
            ];

            for (const [subjects, error] of cases) {
                const res = await client.put("/api/account", { json: tutorBody({ subjects }) });

                assert.equal(res.status, 400, JSON.stringify(subjects));
                assert.equal(res.json.error, error);
            }

            assert.equal(db.queriesMatching(/UPDATE users/).length, 0);
        });

        it("rejects subjects that do not exist", async () => {
            mockCurrentUser(tutorRow);
            db.when(/SELECT id FROM subjects/, () => [{ id: 1 }]); // asked for 1 and 2
            const client = await loginAs(TUTOR);

            const res = await client.put("/api/account", {
                json: tutorBody({
                    subjects: [
                        { subject_id: 1, hourly_rate: 1000 },
                        { subject_id: 2, hourly_rate: 1000 }
                    ]
                })
            });

            assert.equal(res.status, 400);
            assert.equal(res.json.error, "Ismeretlen tantárgy.");
            assert.equal(db.queriesMatching(/UPDATE users/).length, 0);
        });
    });

    it("answers 404 when the user no longer exists", async () => {
        db.when(/SELECT email, role, password_hash FROM users/, () => []);
        const client = await loginAs(STUDENT);

        assert.equal((await client.put("/api/account", { json: studentBody() })).status, 404);
    });

    it("updates a student's name without asking for the password", async () => {
        mockCurrentUser(studentRow);
        const client = await loginAs(STUDENT);

        const res = await client.put("/api/account", {
            json: studentBody({ name: "Új Név", bio: "ezt figyelmen kívül hagyja" })
        });

        assert.equal(res.status, 200);
        assert.deepEqual(res.json, {
            success: true,
            user: { id: 1, name: "Új Név", email: STUDENT.email, role: "STUDENT" }
        });

        const [update] = db.queriesMatching(/UPDATE users SET/);
        assert.equal(update.sql.replace(/\s+/g, " ").trim(), "UPDATE users SET full_name = ?, email = ? WHERE id = ?");
        assert.deepEqual(update.params, ["Új Név", STUDENT.email, 1]);
        assert.equal(db.queriesMatching(/tutor_subjects/).length, 0);
    });

    it("keeps the session in sync with the new name and e-mail", async () => {
        mockCurrentUser(studentRow);
        const client = await loginAs(STUDENT);

        await client.put("/api/account", {
            json: studentBody({ name: "Új Név", email: "uj@example.com", currentPassword: PASSWORD })
        });

        const me = await client.get("/api/me");
        assert.equal(me.json.user.name, "Új Név");
        assert.equal(me.json.user.email, "uj@example.com");
    });

    it("changing the e-mail needs the current password", async () => {
        mockCurrentUser(studentRow);
        const client = await loginAs(STUDENT);

        const missing = await client.put("/api/account", { json: studentBody({ email: "uj@example.com" }) });
        const wrong = await client.put("/api/account", {
            json: studentBody({ email: "uj@example.com", currentPassword: "Rossz#999" })
        });

        assert.equal(missing.status, 400);
        assert.match(missing.json.error, /jelenlegi jelszavad/);
        assert.equal(wrong.status, 403);
        assert.deepEqual(wrong.json, { error: "A jelenlegi jelszó hibás." });
        assert.equal(db.queriesMatching(/UPDATE users/).length, 0);
    });

    it("changing only the letter case of the e-mail does not need the password", async () => {
        mockCurrentUser(studentRow);
        const client = await loginAs(STUDENT);

        const res = await client.put("/api/account", {
            json: studentBody({ email: STUDENT.email.toUpperCase() })
        });

        assert.equal(res.status, 200);
    });

    it("changing the password needs the current one and stores a new hash", async () => {
        mockCurrentUser(studentRow);
        const client = await loginAs(STUDENT);

        const wrong = await client.put("/api/account", {
            json: studentBody({ newPassword: "Uj#Jelszo9", currentPassword: "Rossz#999" })
        });
        assert.equal(wrong.status, 403);
        assert.equal(db.queriesMatching(/UPDATE users/).length, 0);

        const ok = await client.put("/api/account", {
            json: studentBody({ newPassword: "Uj#Jelszo9", currentPassword: PASSWORD })
        });
        assert.equal(ok.status, 200);

        const [update] = db.queriesMatching(/UPDATE users SET/);
        assert.match(update.sql, /password_hash = \?/);
        const newHash = update.params[2];
        assert.equal(await argon2.verify(newHash, "Uj#Jelszo9"), true);
        assert.ok(!JSON.stringify(ok.json).includes(newHash));
    });

    it("lets a tutor change bio and replace the subjects", async () => {
        mockCurrentUser(tutorRow);
        db.when(/SELECT id FROM subjects/, () => [{ id: 3 }, { id: 4 }]);
        const client = await loginAs(TUTOR);

        const res = await client.put("/api/account", {
            json: tutorBody({
                bio: "  Új bemutatkozás  ",
                subjects: [
                    { subject_id: 3, hourly_rate: 4000 },
                    { subject_id: "4", hourly_rate: "5000" }
                ]
            })
        });

        assert.equal(res.status, 200);

        const [update] = db.queriesMatching(/UPDATE users SET/);
        assert.match(update.sql, /bio = \?/);
        assert.deepEqual(update.params, ["Tanár Tamás", TUTOR.email, "Új bemutatkozás", 2]);

        assert.deepEqual(db.queriesMatching(/DELETE FROM tutor_subjects/)[0].params, [2]);
        assert.deepEqual(db.queriesMatching(/INSERT INTO tutor_subjects/)[0].params, [
            [[2, 3, 4000], [2, 4, 5000]]
        ]);
    });

    it("an empty subjects list removes all subjects", async () => {
        mockCurrentUser(tutorRow);
        const client = await loginAs(TUTOR);

        const res = await client.put("/api/account", { json: tutorBody({ subjects: [] }) });

        assert.equal(res.status, 200);
        assert.equal(db.queriesMatching(/DELETE FROM tutor_subjects/).length, 1);
        assert.equal(db.queriesMatching(/INSERT INTO tutor_subjects/).length, 0);
    });

    it("leaves the subjects alone when none are sent", async () => {
        mockCurrentUser(tutorRow);
        const client = await loginAs(TUTOR);

        const res = await client.put("/api/account", { json: tutorBody() });

        assert.equal(res.status, 200);
        assert.equal(db.queriesMatching(/tutor_subjects/).length, 0);
    });

    it("answers 409 when the new e-mail belongs to someone else", async () => {
        mockCurrentUser(studentRow);
        db.when(/UPDATE users SET/, () => {
            throw Object.assign(new Error("dup"), { code: "ER_DUP_ENTRY" });
        });
        const client = await loginAs(STUDENT);

        const res = await client.put("/api/account", {
            json: studentBody({ email: "foglalt@example.com", currentPassword: PASSWORD })
        });

        assert.equal(res.status, 409);
        assert.deepEqual(res.json, { error: "Ez az e-mail cím már foglalt." });
    });

    it("answers 500 on other database errors", async () => {
        db.when(/SELECT email, role, password_hash FROM users/, () => {
            throw new Error("boom");
        });
        const client = await loginAs(STUDENT);

        const res = await client.put("/api/account", { json: studentBody() });

        assert.equal(res.status, 500);
        assert.deepEqual(res.json, { error: "Nem sikerült menteni a módosításokat." });
    });
});
