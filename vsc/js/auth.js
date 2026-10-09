const argon2 = require("argon2");

// ============================================================
// TESZT-NAPLÓZÁS
// ============================================================
//
// Az alábbi naplók a nyers jelszót és a hash-t is kiírják a konzolra.
// Ez fejlesztés közben hasznos, de élesben veszélyes (a naplót látja a
// szerver gazdája, a hosting és a naplógyűjtő szolgáltatás is).
//
// Ezért csak akkor futnak, ha:
//   - a NODE_ENV nem "production", ÉS
//   - a DEBUG_LOGS környezeti változó nem "false".
//
// Fejlesztéskor tehát minden marad a régi (nem kell semmit beállítani).
// Élesben a NODE_ENV=production beállítás kikapcsolja őket.

const DEBUG_LOGS =
    process.env.NODE_ENV !== "production" &&
    process.env.DEBUG_LOGS !== "false";

function debug(...args) {

    if (DEBUG_LOGS) {
        console.log(...args);
    }
}

async function hashPassword(password) {

    debug("\n------------------------------------------");
    debug("🔐 AUTH.JS - hashPassword() CALLED");
    debug("------------------------------------------");

    debug("📥 RAW PASSWORD RECEIVED:");
    debug(password);

    debug("\n📊 PASSWORD INFORMATION:");
    debug("Type:", typeof password);
    debug("Length:", password.length);


try {
    debug("\n⚙️ Starting Argon2id hashing...");

    const hash = await argon2.hash(password, {
        type: argon2.argon2id,
        memoryCost: 2 ** 15,
        timeCost: 4,
        parallelism: 1
    });
    debug("\n✅ ARGON2 HASHING COMPLETE");
    debug("📤 HASH GENERATED:");
    debug(hash);
    debug("\n📊 HASH INFORMATION:");
    debug("Type:", typeof hash);
    debug("Length:", hash.length);
    debug("\n📤 Returning hash to main.js...");
    debug("------------------------------------------\n");
return hash;
} catch (err) {

    console.error("\n❌ ARGON2 HASHING ERROR");
    console.error(err);

    throw new Error(
        "Nem sikerült hash-elni a jelszót.",
        { cause: err }
    );
}
}


// ============================================================
// VERIFY PASSWORD
// ============================================================

async function verifyPassword(hash, password) {

    debug("\n------------------------------------------");
    debug("🔍 AUTH.JS - verifyPassword() CALLED");
    debug("------------------------------------------");

    debug("📥 HASH RECEIVED:");
    debug(hash);

    debug("\n📥 RAW PASSWORD RECEIVED:");
    debug(password);


    try {

        debug("\n⚙️ Argon2 verifying...");

        const result = await argon2.verify(
            hash,
            password
        );


        debug("✅ Verification finished");
        debug("Result:", result);

        debug("------------------------------------------\n");


        return result;

    } catch (err) {

        console.error("❌ PASSWORD VERIFICATION ERROR");
        console.error(err);

        return false;
    }
}

module.exports = {
    hashPassword,
    verifyPassword,
    debug,
    DEBUG_LOGS
};