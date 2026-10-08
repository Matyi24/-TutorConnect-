    it("can hash an empty string and unicode passwords", async () => {
        const empty = await hashPassword("");
        const unicode = await hashPassword("árvíztűrő tükörfúrógép 🔐");

        assert.match(empty, /^\$argon2id\$/);
        assert.match(unicode, /^\$argon2id\$/);
    });

    it("rejects when no password is given", async () => {
        await assert.rejects(() => hashPassword(undefined));
        await assert.rejects(() => hashPassword(null));
    });

    it("wraps Argon2 failures in a Hungarian error and keeps the cause", async () => {
        const boom = new Error("argon2 exploded");
        mock.method(argon2, "hash", async () => {
            throw boom;
        });

        await assert.rejects(
            () => hashPassword("valami"),
            (err) => {
                assert.equal(err.message, "Nem sikerült hash-elni a jelszót.");
                assert.equal(err.cause, boom);
                return true;
            }
        );
    });

    it("asks Argon2 for argon2id with the configured options", async () => {
        const spy = mock.method(argon2, "hash", async () => "fake-hash");

        const result = await hashPassword("abc");

        assert.equal(result, "fake-hash");
        assert.equal(spy.mock.callCount(), 1);

        const [password, options] = spy.mock.calls[0].arguments;
        assert.equal(password, "abc");
        assert.deepEqual(options, {
            type: argon2.argon2id,
            memoryCost: 2 ** 15,
            timeCost: 4,
            parallelism: 1
        });
    });

describe("verifyPassword()", () => {

    let hash;

    before(async () => {
        hash = await hashPassword("Helyes#Jelszo1");
    });

    it("returns true for the correct password", async () => {
        assert.equal(await verifyPassword(hash, "Helyes#Jelszo1"), true);
    });

    it("returns false for a wrong password", async () => {
        assert.equal(await verifyPassword(hash, "Rossz#Jelszo1"), false);
    });

    it("is case sensitive and does not ignore whitespace", async () => {
        assert.equal(await verifyPassword(hash, "helyes#jelszo1"), false);
        assert.equal(await verifyPassword(hash, "Helyes#Jelszo1 "), false);
    });

    it("returns false for an empty password", async () => {
        assert.equal(await verifyPassword(hash, ""), false);
    });

    it("returns false (and does not throw) for a malformed hash", async () => {
        assert.equal(await verifyPassword("ez-nem-hash", "Helyes#Jelszo1"), false);
    });

    it("returns false (and does not throw) for a missing hash", async () => {
        assert.equal(await verifyPassword(undefined, "Helyes#Jelszo1"), false);
        assert.equal(await verifyPassword(null, "Helyes#Jelszo1"), false);
    });

    it("returns false when Argon2 itself throws", async () => {
        mock.method(argon2, "verify", async () => {
            throw new Error("boom");
        });

        assert.equal(await verifyPassword(hash, "Helyes#Jelszo1"), false);
    });

    it("passes the hash first and the password second to Argon2", async () => {
        const spy = mock.method(argon2, "verify", async () => true);

        assert.equal(await verifyPassword("H", "P"), true);
        assert.deepEqual(spy.mock.calls[0].arguments, ["H", "P"]);
    });

    it("round-trips with hashPassword for several passwords", async () => {
        for (const password of ["a", "1234567890", "árvíz tűrő", "p@$$w0rd!"]) {
            const h = await hashPassword(password);
            assert.equal(await verifyPassword(h, password), true, password);
            assert.equal(await verifyPassword(h, password + "x"), false, password);
        }
    });
});