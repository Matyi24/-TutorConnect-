// Test harness for vsc/js/main.js.
//
// main.js starts listening and connects to MySQL as soon as it is required, and
// it does not export anything. To test it WITHOUT changing it we:
//   1. replace the "mysql2" module with an in-memory fake (see FakeDb below),
//   2. intercept http.createServer() so we can reach the server main.js creates,
//   3. make that server listen on a random free port instead of 3000.
//
// Nothing here needs a real database or a free port 3000.

const Module = require("module");
const path = require("path");
const http = require("http");

const MAIN_PATH = path.join(__dirname, "../../vsc/js/main.js");
const MYSQL_PATH = require.resolve("mysql2", { paths: [path.dirname(MAIN_PATH)] });

// main.js and auth.js are very chatty (they even print passwords), so keep the
// test output readable.
function silenceConsole() {
    console.log = () => {};
    console.error = () => {};
}

// ------------------------------------------------------------
// Fake MySQL connection
// ------------------------------------------------------------
//
// db.when(/regex/, (sql, params) => rows)   register an answer for a query
// A responder may return:
//   - an array            -> rows (SELECT)
//   - an object           -> result header, e.g. { insertId: 5 } / { affectedRows: 2 }
//   - throw an Error      -> the query fails with that error
// The most recently registered matching responder wins.
// Every executed query is recorded in db.queries.

class FakeDb {
    constructor() {
        this.reset();
    }

    reset() {
        this.responders = [];
        this.queries = [];
        // main.js runs these once after a successful connect().
        this.when(/SHOW COLUMNS FROM messages/i, () => [{ Field: "x" }]);
    }

    when(pattern, responder) {
        this.responders.unshift({ pattern, responder });
        return this;
    }

    // All recorded queries whose SQL matches the pattern.
    queriesMatching(pattern) {
        return this.queries.filter((q) => pattern.test(q.sql));
    }

    _run(sql, params) {
        this.queries.push({ sql, params });

        const match = this.responders.find((r) => r.pattern.test(sql));

        if (!match) {
            throw new Error("Unexpected query in test: " + sql.replace(/\s+/g, " ").trim());
        }

        return match.responder(sql, params);
    }

    // Callback style: db.query(sql, [params], cb)
    query(sql, params, cb) {
        if (typeof params === "function") {
            cb = params;
            params = [];
        }

        let result;
        let error = null;

        try {
            result = this._run(sql, params);
        } catch (e) {
            error = e;
        }

        // Real mysql2 is asynchronous, so be asynchronous too.
        setImmediate(() => (error ? cb(error) : cb(null, result)));
    }

    connect(cb) {
        setImmediate(() => cb(null));
    }

    // Promise style: db.promise().query(sql, [params]) -> [rows]
    promise() {
        return {
            query: async (sql, params) => [this._run(sql, params || [])]
        };
    }
}

// ------------------------------------------------------------
// Loading main.js
// ------------------------------------------------------------

let loaded = null;

function loadServer() {
    if (loaded) return loaded;

    silenceConsole();

    const db = new FakeDb();

    // 1. fake mysql2
    require.cache[MYSQL_PATH] = {
        id: MYSQL_PATH,
        filename: MYSQL_PATH,
        loaded: true,
        exports: { createConnection: () => db }
    };

    // 2 + 3. grab the http server and listen on port 0
    let server = null;
    const realCreateServer = http.createServer;

    http.createServer = function (...args) {
        server = realCreateServer.apply(this, args);

        const realListen = server.listen.bind(server);
        server.listen = (port, ...rest) => realListen(0, "127.0.0.1", ...rest);

        return server;
    };

    try {
        delete require.cache[MAIN_PATH];
        require(MAIN_PATH);
    } finally {
        http.createServer = realCreateServer;
    }

    loaded = { db, server };
    return loaded;
}

// Resolves with the base URL once the server is listening.
async function start() {
    const { db, server } = loadServer();

    if (!server.listening) {
        await new Promise((resolve) => server.once("listening", resolve));
    }

    const { port } = server.address();

    return {
        db,
        server,
        port,
        baseUrl: `http://127.0.0.1:${port}`,
        stop: () =>
            new Promise((resolve) => {
                server.closeAllConnections();
                server.close(resolve);
            })
    };
}

// ------------------------------------------------------------
// HTTP client with its own cookie jar (one per simulated browser)
// ------------------------------------------------------------

class Client {
    constructor(baseUrl) {
        this.baseUrl = baseUrl;
        this.cookies = new Map();
    }

    get cookieHeader() {
        return [...this.cookies].map(([k, v]) => `${k}=${v}`).join("; ");
    }

    async request(method, url, { json, form, body, headers = {} } = {}) {
        const init = { method, redirect: "manual", headers: { ...headers } };

        if (json !== undefined) {
            init.headers["Content-Type"] = "application/json";
            init.body = JSON.stringify(json);
        } else if (form !== undefined) {
            init.headers["Content-Type"] = "application/x-www-form-urlencoded";
            init.body = new URLSearchParams(form).toString();
        } else if (body !== undefined) {
            init.body = body; // e.g. FormData
        }

        if (this.cookies.size) init.headers.Cookie = this.cookieHeader;

        const res = await fetch(this.baseUrl + url, init);

        for (const line of res.headers.getSetCookie()) {
            const [pair] = line.split(";");
            const eq = pair.indexOf("=");
            const name = pair.slice(0, eq).trim();
            const value = pair.slice(eq + 1).trim();
            const expired = /expires=Thu, 01 Jan 1970/i.test(line);

            if (expired || value === "") this.cookies.delete(name);
            else this.cookies.set(name, value);
        }

        const text = await res.text();
        let parsed;
        try {
            parsed = JSON.parse(text);
        } catch {
            parsed = undefined;
        }

        return { status: res.status, headers: res.headers, text, json: parsed };
    }

    get(url, opts) { return this.request("GET", url, opts); }
    post(url, opts) { return this.request("POST", url, opts); }
    put(url, opts) { return this.request("PUT", url, opts); }
    patch(url, opts) { return this.request("PATCH", url, opts); }
    delete(url, opts) { return this.request("DELETE", url, opts); }
}

module.exports = { start, Client };
