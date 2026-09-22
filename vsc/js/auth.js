const argon2 = require("argon2");


// ============================================================
// HASH PASSWORD
// ============================================================

async function hashPassword(password) {

    console.log("\n------------------------------------------");
    console.log("🔐 AUTH.JS - hashPassword() CALLED");
    console.log("------------------------------------------");

    console.log("📥 RAW PASSWORD RECEIVED:");
    console.log(password);

    console.log("\n📊 PASSWORD INFORMATION:");
    console.log("Type:", typeof password);
    console.log("Length:", password.length);


try {
    console.log("\n⚙️ Starting Argon2id hashing...");

    const hash = await argon2.hash(password, {
        type: argon2.argon2id,
        memoryCost: 2 ** 15,
        timeCost: 4,
        parallelism: 1
    });
    console.log("\n✅ ARGON2 HASHING COMPLETE");
    console.log("📤 HASH GENERATED:");
    console.log(hash);
    console.log("\n📊 HASH INFORMATION:");
    console.log("Type:", typeof hash);
    console.log("Length:", hash.length);
    console.log("\n📤 Returning hash to main.js...");
    console.log("------------------------------------------\n");
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

    console.log("\n------------------------------------------");
    console.log("🔍 AUTH.JS - verifyPassword() CALLED");
    console.log("------------------------------------------");

    console.log("📥 HASH RECEIVED:");
    console.log(hash);

    console.log("\n📥 RAW PASSWORD RECEIVED:");
    console.log(password);


    try {

        console.log("\n⚙️ Argon2 verifying...");

        const result = await argon2.verify(
            hash,
            password
        );


        console.log("✅ Verification finished");
        console.log("Result:", result);

        console.log("------------------------------------------\n");


        return result;

    } catch (err) {

        console.error("❌ PASSWORD VERIFICATION ERROR");
        console.error(err);

        return false;
    }
}


// ============================================================
// EXPORT
// ============================================================

module.exports = {
    hashPassword,
    verifyPassword
};