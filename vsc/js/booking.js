// ============================================================
// FOGLALÁSI ABLAK (közös: oktatók oldal és chat profil)
// ============================================================
//
// Használat:
//   TCBooking.renderButton(tároló, oktatóId, oktatóNév)
//
// A gomb csak bejelentkezett diáknak működik. Vendégnek belépésre
// hívó link jelenik meg, oktatónak semmi. A gomb egy ablakot nyit:
// tantárgy- és időpontválasztás, opcionális üzenet, majd a foglalás
// elküldése. Az oktatónak ezt jóvá kell hagynia.
//
// A stílusait maga injektálja (bk- előtag), így bármelyik oldalon
// működik, ahol be van töltve.

(function () {

    "use strict";

    let currentUser;            // undefined = még nem kérdeztük le
    let modal = null;
    let state = null;           // az éppen nyitott foglalás állapota


    // ---------------- segédek ----------------

    function esc(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function formatPrice(value) {
        return Number(value || 0).toLocaleString("hu-HU") + " Ft";
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

    async function getCurrentUser() {

        if (currentUser !== undefined) {
            return currentUser;
        }

        try {

            const response = await fetch("/api/me", { credentials: "include" });
            const data = await response.json();

            currentUser = data.loggedIn ? data.user : null;

        } catch (error) {

            currentUser = null;
        }

        return currentUser;
    }


    // ---------------- stílusok ----------------

    function injectStyles() {

        if (document.getElementById("bkStyles")) {
            return;
        }

        const style = document.createElement("style");
        style.id = "bkStyles";

        style.textContent = `

            .bk-open-button {
                display: inline-flex; align-items: center; gap: 8px;
                padding: 11px 20px; border: 0; border-radius: 12px;
                background: #4c6ef5; color: #fff; font: inherit; font-size: 14px;
                font-weight: 700; text-decoration: none; cursor: pointer;
                box-shadow: 0 6px 16px rgba(76, 110, 245, .28);
                transition: background .2s ease, box-shadow .2s ease;
            }
            .bk-open-button:hover { background: #3b5bdb; box-shadow: 0 8px 20px rgba(76, 110, 245, .36); transform: none; }

            /* A gomb tárolója: hézag alatta, hogy ne érjen a következő elemhez */
            #profileBookingSlot:not(:empty),
            #chatBookingSlot:not(:empty) { margin-bottom: 20px; }

            .bk-modal {
                position: fixed; inset: 0; z-index: 10000; display: flex;
                align-items: center; justify-content: center; padding: 20px;
                opacity: 0; visibility: hidden; pointer-events: none;
                transition: opacity .2s ease, visibility 0s linear .2s;
            }
            .bk-modal.open { opacity: 1; visibility: visible; pointer-events: auto; transition: opacity .2s ease, visibility 0s; }
            .bk-overlay { position: absolute; inset: 0; background: rgba(18, 15, 38, .6); backdrop-filter: blur(4px); }

            .bk-window {
                position: relative; width: min(560px, 100%); max-height: 92vh; overflow-y: auto;
                padding: 28px; border-radius: 20px; background: #fff; color: #212529;
                box-shadow: 0 30px 80px rgba(20, 15, 60, .3); font-family: 'Segoe UI', system-ui, sans-serif;
                transform: translateY(24px) scale(.96); transition: transform .28s cubic-bezier(.2, .8, .2, 1);
            }
            .bk-modal.open .bk-window { transform: none; }

            .bk-close {
                position: absolute; top: 14px; right: 14px; width: 36px; height: 36px; padding: 0;
                border: 0; border-radius: 10px; background: rgba(76, 110, 245, .08);
                color: rgba(33, 37, 41, .6); font-size: 22px; line-height: 1; cursor: pointer;
            }
            .bk-close:hover { background: #4c6ef5; color: #fff; transform: none; }

            .bk-window h2 { margin: 0 0 4px; font-size: 22px; padding-right: 40px; }
            .bk-sub { margin: 0 0 18px; font-size: 14px; color: rgba(33, 37, 41, .6); }
            .bk-label { display: block; margin: 16px 0 6px; font-size: 13px; font-weight: 700; }
            .bk-label small { font-weight: 400; color: rgba(33, 37, 41, .5); }

            .bk-window select, .bk-window textarea {
                width: 100%; box-sizing: border-box; padding: 10px 12px; border: 1px solid #d9dcf0;
                border-radius: 10px; background: #fff; font: inherit; font-size: 14px; color: inherit;
            }
            .bk-window textarea { min-height: 70px; resize: vertical; }
            .bk-window select:focus, .bk-window textarea:focus { outline: none; border-color: #4c6ef5; box-shadow: 0 0 0 3px #dbe4ff; }

            .bk-days { display: flex; flex-direction: column; gap: 12px; max-height: 240px; overflow-y: auto; padding-right: 4px; }
            .bk-day-title { margin: 0 0 6px; font-size: 13px; font-weight: 700; text-transform: capitalize; }
            .bk-slots { display: flex; flex-wrap: wrap; gap: 8px; }
            .bk-slot {
                padding: 8px 14px; border: 1.5px solid #d9dcf0; border-radius: 10px; background: #fff;
                font: inherit; font-size: 13px; font-weight: 600; color: #3b5bdb; cursor: pointer;
                transition: background .15s ease, border-color .15s ease, color .15s ease;
            }
            .bk-slot:hover { background: #edf1ff; transform: none; }
            .bk-slot.selected { background: #4c6ef5; border-color: #4c6ef5; color: #fff; }

            .bk-empty { padding: 18px; border-radius: 12px; background: #f4f6ff; text-align: center; font-size: 14px; color: rgba(33, 37, 41, .7); }
            .bk-empty a { color: #4c6ef5; font-weight: 700; }

            .bk-summary { margin-top: 16px; padding: 12px 14px; border-radius: 12px; background: #f4f6ff; font-size: 14px; min-height: 20px; }
            .bk-error { margin-top: 12px; padding: 10px 12px; border-radius: 10px; background: #fff0f4; color: #b82a5b; font-size: 13px; }
            .bk-error:empty { display: none; }

            .bk-submit {
                display: block; width: 100%; margin-top: 16px; padding: 13px; border: 0; border-radius: 12px;
                background: #4c6ef5; color: #fff; font: inherit; font-size: 15px; font-weight: 700; cursor: pointer;
            }
            .bk-submit:hover:not(:disabled) { background: #3b5bdb; transform: none; }
            .bk-submit:disabled { opacity: .45; cursor: default; }

            .bk-success { text-align: center; padding: 10px 0 4px; }
            .bk-success-icon { font-size: 44px; }
            .bk-success p { font-size: 14px; color: rgba(33, 37, 41, .75); line-height: 1.6; }
            .bk-success a { display: inline-block; margin-top: 8px; color: #4c6ef5; font-weight: 700; }

            @media (prefers-reduced-motion: reduce) {
                .bk-modal, .bk-modal.open, .bk-window { transition: none; }
            }
        `;

        document.head.appendChild(style);
    }


    // ---------------- ablak ----------------

    function ensureModal() {

        if (modal) {
            return modal;
        }

        injectStyles();

        modal = document.createElement("div");
        modal.className = "bk-modal";
        modal.setAttribute("aria-hidden", "true");

        modal.innerHTML = `
            <div class="bk-overlay"></div>
            <div class="bk-window" role="dialog" aria-modal="true" aria-label="Időpont foglalása">
                <button type="button" class="bk-close" aria-label="Bezárás">×</button>
                <div class="bk-content"></div>
            </div>
        `;

        document.body.appendChild(modal);

        modal.querySelector(".bk-overlay").addEventListener("click", closeModal);
        modal.querySelector(".bk-close").addEventListener("click", closeModal);

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && modal.classList.contains("open")) {
                closeModal();
            }
        });

        return modal;
    }

    function closeModal() {

        if (!modal) {
            return;
        }

        modal.classList.remove("open");
        modal.setAttribute("aria-hidden", "true");
    }

    function setContent(html) {
        ensureModal().querySelector(".bk-content").innerHTML = html;
    }


    async function open(tutorId, tutorName) {

        ensureModal();

        state = {
            tutorId,
            tutorName,
            slots: [],
            subjects: [],
            slotId: null
        };

        setContent(`<h2>Időpont foglalása</h2><p class="bk-sub">Betöltés...</p>`);

        modal.classList.add("open");
        modal.setAttribute("aria-hidden", "false");

        try {

            const response = await fetch(
                `/api/tutors/${encodeURIComponent(tutorId)}/availability`,
                { credentials: "include" }
            );

            if (!response.ok) {
                throw new Error("HTTP " + response.status);
            }

            const data = await response.json();

            state.slots = data.slots.map((slot) => ({
                id: slot.id,
                start: new Date(slot.start_time),
                end: new Date(slot.end_time)
            }));

            state.subjects = data.subjects;

        } catch (error) {

            console.error("Időpontok betöltési hiba:", error);

            setContent(`
                <h2>Időpont foglalása</h2>
                <div class="bk-error">Nem sikerült betölteni az időpontokat. Próbáld újra később.</div>
            `);

            return;
        }

        render();
    }


    function render() {

        const header = `
            <h2>Időpont foglalása</h2>
            <p class="bk-sub">Oktató: <strong>${esc(state.tutorName)}</strong></p>
        `;

        if (state.subjects.length === 0) {

            setContent(header + `
                <div class="bk-empty">
                    Az oktató még nem adott meg tantárgyat, ezért most nem lehet nála foglalni.
                </div>
            `);

            return;
        }

        if (state.slots.length === 0) {

            setContent(header + `
                <div class="bk-empty">
                    Az oktatónak jelenleg nincs szabad időpontja.<br>
                    <a href="/chat?tutor=${encodeURIComponent(state.tutorId)}">Írj neki üzenetet</a>,
                    és kérd, hogy hirdessen meg újat.
                </div>
            `);

            return;
        }

        // Az időpontok napok szerint csoportosítva
        const days = new Map();

        state.slots.forEach((slot) => {

            const key = slot.start.toDateString();

            if (!days.has(key)) {
                days.set(key, { title: formatDay(slot.start), slots: [] });
            }

            days.get(key).slots.push(slot);
        });

        const daysHtml = Array.from(days.values()).map((day) => `
            <div>
                <p class="bk-day-title">${esc(day.title)}</p>
                <div class="bk-slots">
                    ${day.slots.map((slot) => `
                        <button type="button" class="bk-slot" data-slot="${esc(slot.id)}">
                            ${esc(formatTime(slot.start))} – ${esc(formatTime(slot.end))}
                        </button>
                    `).join("")}
                </div>
            </div>
        `).join("");

        const subjectOptions = state.subjects.map((subject) => `
            <option value="${esc(subject.id)}">
                ${esc(subject.name)} – ${esc(formatPrice(subject.hourly_rate))} / óra
            </option>
        `).join("");

        setContent(header + `
            <label class="bk-label" for="bkSubject">Tantárgy</label>
            <select id="bkSubject">${subjectOptions}</select>

            <span class="bk-label">Szabad időpontok</span>
            <div class="bk-days">${daysHtml}</div>

            <label class="bk-label" for="bkNote">
                Üzenet az oktatónak <small>(nem kötelező)</small>
            </label>
            <textarea id="bkNote" maxlength="300"
                placeholder="Pl. miben szeretnél segítséget kérni?"></textarea>

            <div class="bk-summary" id="bkSummary">Válassz egy időpontot.</div>
            <div class="bk-error" id="bkError" role="alert"></div>

            <button type="button" class="bk-submit" id="bkSubmit" disabled>
                Foglalás elküldése
            </button>
        `);

        const content = modal.querySelector(".bk-content");
        const subjectSelect = content.querySelector("#bkSubject");
        const submit = content.querySelector("#bkSubmit");

        function updateSummary() {

            const slot = state.slots.find((item) => item.id === state.slotId);
            const subject = state.subjects.find(
                (item) => String(item.id) === subjectSelect.value
            );

            submit.disabled = !(slot && subject);

            if (!slot || !subject) {
                content.querySelector("#bkSummary").textContent = "Válassz egy időpontot.";
                return;
            }

            const minutes = Math.round((slot.end - slot.start) / 60000);
            const price = Math.round(Number(subject.hourly_rate) * minutes / 60);

            content.querySelector("#bkSummary").innerHTML = `
                <strong>${esc(formatDay(slot.start))}, ${esc(formatTime(slot.start))} – ${esc(formatTime(slot.end))}</strong><br>
                ${esc(subject.name)} · ${esc(minutes)} perc · <strong>${esc(formatPrice(price))}</strong>
            `;
        }

        content.querySelectorAll(".bk-slot").forEach((button) => {

            button.addEventListener("click", () => {

                state.slotId = Number(button.dataset.slot);

                content.querySelectorAll(".bk-slot").forEach((other) => {
                    other.classList.toggle("selected", other === button);
                });

                content.querySelector("#bkError").textContent = "";

                updateSummary();
            });
        });

        subjectSelect.addEventListener("change", updateSummary);

        submit.addEventListener("click", async () => {

            const errorBox = content.querySelector("#bkError");

            errorBox.textContent = "";
            submit.disabled = true;
            submit.textContent = "Küldés...";

            try {

                const response = await fetch("/api/bookings", {
                    method: "POST",
                    credentials: "include",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        availability_id: state.slotId,
                        subject_id: Number(subjectSelect.value),
                        note: content.querySelector("#bkNote").value
                    })
                });

                const data = await response.json().catch(() => ({}));

                if (!response.ok) {

                    errorBox.textContent = data.error || "Nem sikerült a foglalás.";

                    // Közben lefoglalták: frissítjük a listát
                    if (response.status === 409) {
                        await open(state.tutorId, state.tutorName);
                        modal.querySelector(".bk-content")
                            .insertAdjacentHTML(
                                "afterbegin",
                                `<div class="bk-error">${esc(data.error)}</div>`
                            );
                    }

                    return;
                }

                showSuccess(data.price);

            } catch (error) {

                console.error("Foglalási hiba:", error);
                errorBox.textContent = "Hálózati hiba, próbáld újra.";

            } finally {

                if (submit.isConnected) {
                    submit.textContent = "Foglalás elküldése";
                    submit.disabled = !state.slotId;
                }
            }
        });
    }


    function showSuccess(price) {

        setContent(`
            <div class="bk-success">
                <div class="bk-success-icon">✅</div>
                <h2 style="padding-right:0">Foglalás elküldve!</h2>
                <p>
                    <strong>${esc(state.tutorName)}</strong> hamarosan jóváhagyja vagy
                    elutasítja a kérésedet. Az ár: <strong>${esc(formatPrice(price))}</strong>.
                    Az állapotát a foglalásaid között követheted.
                </p>
                <a href="/foglalasutan?statusz=fuggoben">Ugrás a foglalásaimhoz →</a>
            </div>
        `);
    }


    // ---------------- a gomb ----------------

    async function renderButton(container, tutorId, tutorName) {

        if (!container) {
            return;
        }

        injectStyles();

        container.innerHTML = "";

        const user = await getCurrentUser();

        // Oktatónak és adminnak nem jelenik meg
        if (user && user.role !== "STUDENT") {
            return;
        }

        if (!user) {

            container.innerHTML = `
                <a class="bk-open-button" href="/login">Jelentkezz be a foglaláshoz</a>
            `;

            return;
        }

        const button = document.createElement("button");
        button.type = "button";
        button.className = "bk-open-button";
        button.textContent = "📅 Időpont foglalása";

        button.addEventListener("click", () => open(tutorId, tutorName));

        container.appendChild(button);
    }


    window.TCBooking = { renderButton, open };

})();
