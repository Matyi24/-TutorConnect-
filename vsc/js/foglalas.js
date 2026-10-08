// ============================================================
// FOGLALÁS - ÖSSZESÍTŐ OLDAL
// ============================================================
//
// Mindenkinek: négy kártya a foglalások számával (közelgő, függőben,
// lemondott, korábbi), amelyek a /foglalasutan listáira visznek.
// Diáknak: egy gomb az oktatók kereséséhez.
// Oktatónak: a szabad időpontok kezelése (hozzáadás, ismétlés, törlés).

(function () {

    "use strict";

    const root = document.getElementById("hubRoot");


    // ---------------- segédek ----------------

    function esc(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function formatDay(date) {
        return date.toLocaleDateString("hu-HU", {
            weekday: "long",
            month: "long",
            day: "numeric"
        });
    }

    function formatTime(date) {
        return date.toLocaleTimeString("hu-HU", {
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    // <input type="datetime-local"> formátum: 2026-10-09T14:00
    function toInputValue(date) {

        const pad = (n) => String(n).padStart(2, "0");

        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
               `T${pad(date.getHours())}:${pad(date.getMinutes())}`;
    }


    // ---------------- kártyák ----------------

    const CARDS = [
        { key: "upcoming",  slug: "kozelgo",   icon: "📅", tone: "upcoming",
          title: "Közelgő foglalásaim",        desc: "Elfogadott, még le nem zajlott órák" },
        { key: "pending",   slug: "fuggoben",  icon: "⏳", tone: "pending",
          title: "Függőben lévő foglalásaim",  desc: "Jóváhagyásra váró kérések" },
        { key: "cancelled", slug: "lemondott", icon: "❌", tone: "cancelled",
          title: "Lemondott foglalásaim",      desc: "Lemondott vagy elutasított órák" },
        { key: "past",      slug: "korabbi",   icon: "📁", tone: "past",
          title: "Korábbi foglalásaim",        desc: "Lezajlott órák és értékelések" }
    ];

    function cardsHtml(summary) {

        return `
            <div class="tutor-cards-container">
                ${CARDS.map((card) => `
                    <a href="/foglalasutan?statusz=${card.slug}"
                       class="hub-card tone-${card.tone}${summary[card.key] === 0 ? " is-empty" : ""}">
                        <div class="hub-card-top">
                            <span class="hub-icon" aria-hidden="true">${card.icon}</span>
                            <span class="hub-num">${summary[card.key]}</span>
                        </div>
                        <h3>${card.title}</h3>
                        <p>${card.desc}</p>
                        <span class="hub-go">Megnyitás →</span>
                    </a>
                `).join("")}
            </div>
        `;
    }

    // A legközelebbi elfogadott óra, kiemelve
    function nextLessonHtml(booking) {

        const start = new Date(booking.start_time);
        const end = new Date(booking.end_time);

        const day = start.toLocaleDateString("hu-HU", {
            weekday: "long",
            month: "long",
            day: "numeric"
        });

        const time =
            formatTime(start) + " – " + formatTime(end);

        return `
            <div class="hub-next">
                <div>
                    <span class="hub-next-label">Következő órád</span>
                    <h3>${esc(booking.subject_name || "Tantárgy")}</h3>
                    <p>
                        <span class="hub-next-when">${esc(day)}</span>, ${esc(time)}
                        · ${booking.as_student ? "Oktató" : "Diák"}:
                        <strong>${esc(booking.other_name || "Ismeretlen")}</strong>
                    </p>
                </div>
                <a class="hub-next-link" href="/foglalasutan?statusz=kozelgo">Részletek →</a>
            </div>
        `;
    }

    // Az oktatónak: jóváhagyásra váró kérések
    function alertHtml(count) {

        return `
            <div class="hub-alert">
                <strong>
                    ⏳ ${count} új foglalási kérés vár a jóváhagyásodra
                </strong>
                <a href="/foglalasutan?statusz=fuggoben">Megnézem →</a>
            </div>
        `;
    }


    // ---------------- diák ----------------

    function studentExtras() {

        return `
            <div class="hub-cta">
                <div>
                    <strong>Új időpontot szeretnél foglalni?</strong>
                    <p>Keress oktatót, nézd meg a profilját, és válassz a szabad időpontjai közül.</p>
                </div>
                <a class="hub-cta-button" href="/oktatok">Oktatók keresése →</a>
            </div>
        `;
    }


    // ---------------- oktató: szabad időpontok ----------------

    function availabilityPanel() {

        const start = new Date();
        start.setDate(start.getDate() + 1);
        start.setHours(16, 0, 0, 0);

        const repeatOptions = Array.from({ length: 12 }, (_, i) => {
            const n = i + 1;
            return `<option value="${n}">${n === 1 ? "Csak egyszer" : "Hetente, " + n + " alkalommal"}</option>`;
        }).join("");

        return `
            <section class="avail-panel">

                <h3>Szabad időpontjaim</h3>
                <p class="avail-hint">
                    Ezekből választhatnak a diákok. Egy időpont lefoglalása után te hagyod jóvá.
                </p>

                <form class="avail-form" id="availForm">

                    <div class="avail-field">
                        <label for="availStart">Kezdés</label>
                        <input type="datetime-local" id="availStart" step="900"
                            value="${toInputValue(start)}" required>
                    </div>

                    <div class="avail-field">
                        <label for="availMinutes">Időtartam</label>
                        <select id="availMinutes">
                            <option value="30">30 perc</option>
                            <option value="45">45 perc</option>
                            <option value="60" selected>60 perc</option>
                            <option value="90">90 perc</option>
                            <option value="120">120 perc</option>
                        </select>
                    </div>

                    <div class="avail-field">
                        <label for="availRepeat">Ismétlés</label>
                        <select id="availRepeat">${repeatOptions}</select>
                    </div>

                    <button type="submit" class="avail-add" id="availAdd">Időpont hozzáadása</button>

                </form>

                <div class="avail-message" id="availMessage" role="status"></div>

                <div id="availList"><p class="hub-message">Betöltés...</p></div>

            </section>
        `;
    }

    function setupAvailability() {

        const form = document.getElementById("availForm");
        const message = document.getElementById("availMessage");
        const list = document.getElementById("availList");
        const addButton = document.getElementById("availAdd");

        function showMessage(text, type) {
            message.textContent = text;
            message.className = "avail-message " + (type || "");
        }

        async function load() {

            try {

                const response = await fetch("/api/availability", { credentials: "include" });

                if (!response.ok) {
                    throw new Error("HTTP " + response.status);
                }

                render(await response.json());

            } catch (error) {

                console.error("Időpontok betöltési hiba:", error);
                list.innerHTML = `<p class="hub-message">Nem sikerült betölteni az időpontokat.</p>`;
            }
        }

        function render(slots) {

            if (slots.length === 0) {
                list.innerHTML = `<p class="hub-message">Még nincs megadott időpontod.</p>`;
                return;
            }

            const days = new Map();

            slots.forEach((slot) => {

                const start = new Date(slot.start_time);
                const end = new Date(slot.end_time);
                const key = start.toDateString();

                if (!days.has(key)) {
                    days.set(key, { title: formatDay(start), items: [] });
                }

                days.get(key).items.push({
                    id: slot.id,
                    text: formatTime(start) + " – " + formatTime(end),
                    booked: Boolean(slot.is_booked)
                });
            });

            list.innerHTML = Array.from(days.values()).map((day) => `
                <div class="avail-day">
                    <p class="avail-day-title">${esc(day.title)}</p>
                    <div class="avail-slots">
                        ${day.items.map((item) => item.booked
                            ? `<span class="avail-slot booked" title="Ezt az időpontot lefoglalták">
                                    ${esc(item.text)} · foglalt
                               </span>`
                            : `<span class="avail-slot">
                                    ${esc(item.text)}
                                    <button type="button" class="avail-remove" data-id="${esc(item.id)}"
                                        aria-label="Időpont törlése">×</button>
                               </span>`
                        ).join("")}
                    </div>
                </div>
            `).join("");
        }

        list.addEventListener("click", async (event) => {

            const button = event.target.closest(".avail-remove");

            if (!button) {
                return;
            }

            button.disabled = true;

            try {

                const response = await fetch(
                    "/api/availability/" + encodeURIComponent(button.dataset.id),
                    { method: "DELETE", credentials: "include" }
                );

                if (!response.ok) {

                    const data = await response.json().catch(() => ({}));
                    showMessage(data.error || "Nem sikerült törölni.", "error");
                }

                await load();

            } catch (error) {

                console.error("Időpont törlési hiba:", error);
                showMessage("Hálózati hiba, próbáld újra.", "error");
                button.disabled = false;
            }
        });

        form.addEventListener("submit", async (event) => {

            event.preventDefault();

            addButton.disabled = true;
            showMessage("", "");

            try {

                const response = await fetch("/api/availability", {
                    method: "POST",
                    credentials: "include",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        start: document.getElementById("availStart").value,
                        minutes: Number(document.getElementById("availMinutes").value),
                        repeat_weeks: Number(document.getElementById("availRepeat").value)
                    })
                });

                const data = await response.json().catch(() => ({}));

                if (!response.ok) {
                    showMessage(data.error || "Nem sikerült hozzáadni.", "error");
                    return;
                }

                showMessage(
                    data.created + " időpont hozzáadva" +
                    (data.skipped > 0 ? " (" + data.skipped + " kimaradt: ütközés vagy túl messze)" : "") + ".",
                    "success"
                );

                await load();

            } catch (error) {

                console.error("Időpont hozzáadási hiba:", error);
                showMessage("Hálózati hiba, próbáld újra.", "error");

            } finally {

                addButton.disabled = false;
            }
        });

        load();
    }


    // ---------------- indulás ----------------

    async function init() {

        let me = { loggedIn: false };

        try {
            const response = await fetch("/api/me", { credentials: "include" });
            me = await response.json();
        } catch (error) {
            console.error("Bejelentkezési állapot hiba:", error);
        }

        if (!me.loggedIn) {

            root.innerHTML = `
                <div class="hub-cta">
                    <div>
                        <strong>A foglalásaid megtekintéséhez jelentkezz be.</strong>
                        <p>Még nincs fiókod? Regisztrálj diákként vagy korrepetálóként.</p>
                    </div>
                    <a class="hub-cta-button" href="/login">Belépés →</a>
                </div>
            `;

            return;
        }

        let summary = { pending: 0, upcoming: 0, cancelled: 0, past: 0 };

        try {

            const response = await fetch("/api/bookings/summary", { credentials: "include" });

            if (response.ok) {
                summary = await response.json();
            }

        } catch (error) {
            console.error("Összesítő betöltési hiba:", error);
        }

        const role = me.user.role;

        // A legközelebbi elfogadott óra
        let nextBooking = null;

        if (summary.upcoming > 0) {

            try {

                const response = await fetch(
                    "/api/bookings?status=upcoming",
                    { credentials: "include" }
                );

                if (response.ok) {
                    const list = await response.json();
                    nextBooking = list[0] || null;
                }

            } catch (error) {
                console.error("Következő óra betöltési hiba:", error);
            }
        }

        root.innerHTML =
            (role === "TUTOR" && summary.pending > 0 ? alertHtml(summary.pending) : "") +
            (nextBooking ? nextLessonHtml(nextBooking) : "") +
            cardsHtml(summary) +
            (role === "STUDENT" ? studentExtras() : "") +
            (role === "TUTOR" ? availabilityPanel() : "");

        if (role === "TUTOR") {
            setupAvailability();
        }
    }

    init();

})();
