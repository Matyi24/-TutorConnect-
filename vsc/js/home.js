// ============================================================
// FŐOLDAL
// ============================================================
//
// 1) A hero diái és a hozzájuk tartozó szöveg váltása (automatikusan,
//    pontokkal is). Egérrel fölé víve vagy fókuszban megáll.
// 2) A hero gombjai és a "Készen állsz?" sáv a belépett felhasználóhoz
//    igazodik (vendég: regisztráció, diák: oktatók és foglalások,
//    oktató: foglalások és üzenetek).
// 3) A számok (oktatók, teljesített órák, elégedettség) betöltése és
//    felszámolása.

(function () {

    "use strict";

    const reduceMotion =
        window.matchMedia("(prefers-reduced-motion: reduce)").matches;


    // ---------------- 1) diák ----------------

    const slides = Array.from(document.querySelectorAll(".home-slide"));
    const copies = Array.from(document.querySelectorAll(".home-copy"));
    const dots = Array.from(document.querySelectorAll(".home-dot"));
    const hero = document.querySelector(".home-hero");

    let index = 0;
    let timer = null;

    function show(i) {

        index = (i + slides.length) % slides.length;

        slides.forEach((slide, n) => slide.classList.toggle("active", n === index));
        copies.forEach((copy, n) => copy.classList.toggle("active", n === index));

        dots.forEach((dot, n) => {
            dot.classList.toggle("active", n === index);
            dot.setAttribute("aria-current", String(n === index));
        });
    }

    function start() {

        // Aki csökkentett mozgást kért, annak nem váltunk magunktól
        if (reduceMotion || timer || slides.length < 2) {
            return;
        }

        timer = setInterval(() => show(index + 1), 6500);
    }

    function stop() {
        clearInterval(timer);
        timer = null;
    }

    dots.forEach((dot, n) => {
        dot.addEventListener("click", () => {
            show(n);
            stop();
            start();
        });
    });

    if (hero) {
        hero.addEventListener("mouseenter", stop);
        hero.addEventListener("mouseleave", start);
        hero.addEventListener("focusin", stop);
        hero.addEventListener("focusout", start);
    }

    show(0);
    start();


    // ---------------- 2) a felhasználóhoz igazított gombok ----------------

    function button(href, text, cls) {
        return `<a class="home-btn ${cls}" href="${href}">${text}</a>`;
    }

    function adaptToUser(user) {

        const actions = document.getElementById("homeActions");
        const cta = document.getElementById("homeCta");

        if (!user) {
            return;     // vendég: az oldalban lévő alap marad
        }

        const isTutor = user.role === "TUTOR";
        const isAdmin = user.role === "ADMIN";

        // A "Hogyan működik" lépések linkjei (oktatók keresése, foglalás) a
        // diáknak szólnak: az oktatónak és az adminnak nem vezetnek sehová.
        if (user.role !== "STUDENT") {
            document.querySelectorAll(".home-step-link").forEach((link) => link.remove());
        }

        if (actions) {

            actions.innerHTML = isAdmin
                ? button("/admin", "Admin felület", "primary") +
                  button("/subs", "Tantárgyak", "ghost")
                : isTutor
                ? button("/foglalas", "Foglalásaim és időpontjaim", "primary") +
                  button("/chat", "Üzenetek", "ghost")
                : button("/oktatok", "Oktatók böngészése", "primary") +
                  button("/foglalas", "Foglalásaim", "ghost");
        }

        if (cta) {

            cta.innerHTML = isAdmin
                ? `
                    <h2>Admin felület</h2>
                    <p>Nézd át a jelentéseket, kezeld a felhasználókat és az értékeléseket.</p>
                    <div class="home-cta-actions">
                        ${button("/admin", "Megnyitás", "light")}
                    </div>
                `
                : isTutor
                ? `
                    <h2>Hirdesd meg az időpontjaidat</h2>
                    <p>Add meg, mikor érsz rá, és a diákok közvetlenül foglalhatnak nálad.</p>
                    <div class="home-cta-actions">
                        ${button("/foglalas", "Szabad időpontjaim", "light")}
                        ${button("/chat", "Üzenetek", "outline")}
                    </div>
                `
                : `
                    <h2>Készen állsz a következő órára?</h2>
                    <p>Keress oktatót, nézd meg a profilját, és válassz a szabad időpontjai közül.</p>
                    <div class="home-cta-actions">
                        ${button("/oktatok", "Oktatók keresése", "light")}
                        ${button("/foglalas", "Foglalásaim", "outline")}
                    </div>
                `;
        }
    }

    async function loadUser() {

        try {

            const response = await fetch("/api/me", { credentials: "include" });
            const data = await response.json();

            adaptToUser(data.loggedIn ? data.user : null);

        } catch (error) {
            console.error("Bejelentkezési állapot hiba:", error);
        }
    }


    // ---------------- 3) számok ----------------

    function countUp(element, target, suffix) {

        const format = (value) => Math.round(value).toLocaleString("hu-HU") + suffix;

        if (reduceMotion || target === 0) {
            element.textContent = format(target);
            return;
        }

        const duration = 1100;
        const begin = performance.now();

        function frame(now) {

            const progress = Math.min((now - begin) / duration, 1);

            // lassuló mozgás a vége felé
            const eased = 1 - Math.pow(1 - progress, 3);

            element.textContent = format(target * eased);

            if (progress < 1) {
                requestAnimationFrame(frame);
            }
        }

        requestAnimationFrame(frame);
    }

    async function loadStats() {

        const box = document.querySelector(".home-stats");

        try {

            const response = await fetch("/api/stats");

            if (!response.ok) {
                throw new Error("HTTP " + response.status);
            }

            const data = await response.json();

            const tutors = document.getElementById("statTutors");
            const bookings = document.getElementById("statBookings");
            const satisfaction = document.getElementById("statSatisfaction");

            const run = () => {

                countUp(tutors, Number(data.tutors) || 0, "");
                countUp(bookings, Number(data.bookings) || 0, "");

                // Értékelés nélkül nincs értelme a 0%-nak
                if (Number(data.satisfaction) > 0) {
                    countUp(satisfaction, Number(data.satisfaction), "%");
                } else {
                    satisfaction.textContent = "–";
                }
            };

            // A számok akkor indulnak, amikor a kártya látható
            if ("IntersectionObserver" in window && box) {

                const observer = new IntersectionObserver((entries) => {

                    if (entries.some((entry) => entry.isIntersecting)) {
                        observer.disconnect();
                        run();
                    }
                }, { threshold: 0.4 });

                observer.observe(box);

            } else {

                run();
            }

        } catch (error) {

            console.error("Statisztikák betöltése sikertelen:", error);
        }
    }

    loadUser();
    loadStats();

})();
