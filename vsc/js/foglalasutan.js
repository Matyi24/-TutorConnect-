// ============================================================
// FOGLALÁSOK LISTÁJA
// ============================================================
//
// /foglalasutan?statusz=kozelgo | fuggoben | lemondott | korabbi
//
// Diák: látja az oktatót, lemondhat (függő vagy elfogadott, még el nem
// kezdődött óránál). Oktató: elfogadhatja vagy elutasíthatja a függő
// kéréseket, és ő is lemondhat.

(function () {

    "use strict";

    const tabsBox = document.getElementById("bkTabs");
    const listBox = document.getElementById("bkList");
    const feedback = document.getElementById("bkFeedback");


    // ---------------- beállítások ----------------

    // URL-ben magyar szó, a szerver angol kulcsot vár
    const TABS = [
        { slug: "kozelgo",   key: "upcoming",  label: "Közelgő" },
        { slug: "fuggoben",  key: "pending",   label: "Függőben" },
        { slug: "lemondott", key: "cancelled", label: "Lemondott" },
        { slug: "korabbi",   key: "past",      label: "Korábbi" }
    ];

    const STATUS = {
        PENDING:   { label: "Függőben",   css: "pending" },
        CONFIRMED: { label: "Elfogadva",  css: "confirmed" },
        REJECTED:  { label: "Elutasítva", css: "rejected" },
        CANCELLED: { label: "Lemondva",   css: "cancelled" },
        COMPLETED: { label: "Teljesítve", css: "completed" }
    };

    const EMPTY = {
        upcoming:  "Nincs közelgő foglalásod.",
        pending:   "Nincs függőben lévő foglalásod.",
        cancelled: "Nincs lemondott vagy elutasított foglalásod.",
        past:      "Még nincs lezárult foglalásod."
    };

    const STAR_LABELS = ["", "Gyenge", "Elmegy", "Jó", "Nagyon jó", "Kiváló"];

    const params = new URLSearchParams(window.location.search);

    // A legutóbb betöltött foglalások (az értékelő ablaknak kellenek)
    let currentBookings = [];

    let activeTab =
        TABS.find((tab) => tab.slug === params.get("statusz")) || TABS[0];


    // ---------------- segédek ----------------

    function esc(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function stars(rating) {
        return "★".repeat(rating) + "☆".repeat(5 - rating);
    }

    function formatPrice(value) {
        return Number(value).toLocaleString("hu-HU") + " Ft";
    }

    function showFeedback(text, type) {
        feedback.textContent = text;
        feedback.className = "bk-feedback " + (type || "");
    }


    // ---------------- fülek ----------------

    function renderTabs(summary) {

        tabsBox.innerHTML = TABS.map((tab) => `
            <a href="/foglalasutan?statusz=${tab.slug}"
               class="bk-tab${tab === activeTab ? " active" : ""}"
               data-slug="${tab.slug}">
                ${tab.label}
                <span class="bk-tab-count">${summary ? summary[tab.key] : 0}</span>
            </a>
        `).join("");
    }

    // A fülváltás ne töltse újra az oldalt
    tabsBox.addEventListener("click", (event) => {

        const link = event.target.closest(".bk-tab");

        if (!link) {
            return;
        }

        event.preventDefault();

        activeTab = TABS.find((tab) => tab.slug === link.dataset.slug);

        window.history.replaceState(null, "", link.getAttribute("href"));

        showFeedback("", "");
        refresh();
    });


    // ---------------- kártyák ----------------

    // A teljesített óra értékelése (ha van), vagy egy halvány jelzés, hogy még nincs
    function reviewHtml(booking) {

        if (booking.status !== "COMPLETED") {
            return "";
        }

        if (!booking.review) {

            return booking.as_student
                ? ""
                : `<p class="bk-review-none">A diák még nem értékelte ezt az órát.</p>`;
        }

        return `
            <div class="bk-review">
                <span class="bk-review-label">${booking.as_student ? "Az értékelésed" : "Kapott értékelés"}</span>
                <span class="bk-review-stars" aria-label="${esc(booking.review.rating)} csillag az 5-ből">${stars(booking.review.rating)}</span>
                ${booking.review.comment
                    ? `<p class="bk-review-text">${esc(booking.review.comment)}</p>`
                    : ""}
            </div>
        `;
    }

    function cardHtml(booking) {

        const start = new Date(booking.start_time);
        const end = new Date(booking.end_time);

        const status = STATUS[booking.status] || { label: booking.status, css: "" };

        const month = start.toLocaleDateString("hu-HU", { month: "short" });
        const weekday = start.toLocaleDateString("hu-HU", { weekday: "long" });

        const time =
            start.toLocaleTimeString("hu-HU", { hour: "2-digit", minute: "2-digit" }) +
            " – " +
            end.toLocaleTimeString("hu-HU", { hour: "2-digit", minute: "2-digit" });

        const who = booking.as_student ? "Oktató" : "Diák";

        const price = booking.price !== null && booking.price !== undefined
            ? " · " + formatPrice(booking.price)
            : "";

        const actions = [];

        if (booking.can_confirm) {
            actions.push(`<button type="button" class="bk-btn primary" data-action="confirm" data-id="${esc(booking.id)}">Elfogadom</button>`);
        }

        if (booking.can_reject) {
            actions.push(`<button type="button" class="bk-btn danger" data-action="reject" data-id="${esc(booking.id)}">Elutasítom</button>`);
        }

        if (booking.can_cancel) {
            actions.push(`<button type="button" class="bk-btn ghost" data-action="cancel" data-id="${esc(booking.id)}">Lemondás</button>`);
        }

        // Teljesített, még nem értékelt óra: a diák értékelheti
        if (booking.status === "COMPLETED" && booking.as_student && !booking.has_review) {
            actions.push(`<button type="button" class="bk-btn primary" data-review="${esc(booking.id)}">⭐ Értékelés írása</button>`);
        }

        // A diák az oktatónak közvetlenül írhat
        if (booking.as_student && booking.other_id) {
            actions.push(`<a class="bk-btn ghost" href="/chat?tutor=${esc(booking.other_id)}">Üzenet</a>`);
        }

        return `
            <article class="bk-card">

                <div class="bk-date">
                    <span class="bk-date-day">${esc(start.getDate())}</span>
                    <span class="bk-date-month">${esc(month)}</span>
                </div>

                <div class="bk-main">
                    <h3>
                        ${esc(booking.subject_name || "Tantárgy")}
                        <span class="bk-status ${esc(status.css)}">${esc(status.label)}</span>
                    </h3>

                    <p class="bk-meta">
                        ${esc(who)}: <strong>${esc(booking.other_name || "Ismeretlen")}</strong>
                    </p>

                    <p class="bk-meta">
                        <span class="bk-weekday">${esc(weekday)}</span>, ${esc(time)}${esc(price)}
                    </p>

                    ${booking.note
                        ? `<p class="bk-note">„${esc(booking.note)}”</p>`
                        : ""}

                    ${reviewHtml(booking)}
                </div>

                ${actions.length > 0
                    ? `<div class="bk-actions">${actions.join("")}</div>`
                    : ""}

            </article>
        `;
    }


    // ---------------- betöltés ----------------

    async function loadSummary() {

        try {

            const response = await fetch("/api/bookings/summary", { credentials: "include" });

            if (response.ok) {
                return await response.json();
            }

        } catch (error) {
            console.error("Összesítő betöltési hiba:", error);
        }

        return null;
    }

    async function loadList() {

        listBox.innerHTML = `<p class="bk-empty">Betöltés...</p>`;

        try {

            const response = await fetch(
                "/api/bookings?status=" + encodeURIComponent(activeTab.key),
                { credentials: "include" }
            );

            if (response.status === 401) {
                listBox.innerHTML = `
                    <p class="bk-empty">
                        A foglalásaid megtekintéséhez <a href="/login">jelentkezz be</a>.
                    </p>
                `;
                return;
            }

            if (!response.ok) {
                throw new Error("HTTP " + response.status);
            }

            const bookings = await response.json();

            currentBookings = bookings;

            if (bookings.length === 0) {
                listBox.innerHTML = `<p class="bk-empty">${EMPTY[activeTab.key]}</p>`;
                return;
            }

            listBox.innerHTML = bookings.map(cardHtml).join("");

        } catch (error) {

            console.error("Foglalások betöltési hiba:", error);
            listBox.innerHTML = `<p class="bk-empty">Nem sikerült betölteni a foglalásokat.</p>`;
        }
    }

    async function refresh() {

        renderTabs(await loadSummary());

        await loadList();
    }


    // ---------------- műveletek ----------------

    listBox.addEventListener("click", async (event) => {

        const reviewButton = event.target.closest("[data-review]");

        if (reviewButton) {

            const booking = currentBookings.find(
                (item) => String(item.id) === reviewButton.dataset.review
            );

            if (booking) {
                openReviewDialog(booking);
            }

            return;
        }

        const button = event.target.closest("[data-action]");

        if (!button) {
            return;
        }

        // Lemondás és elutasítás: első kattintás megerősítést kér
        const needsConfirm = button.dataset.action !== "confirm";

        if (needsConfirm && !button.classList.contains("confirm")) {

            const original = button.textContent;

            button.classList.add("confirm");
            button.textContent = "Biztosan?";

            clearTimeout(button.confirmTimer);

            button.confirmTimer = setTimeout(() => {
                button.classList.remove("confirm");
                button.textContent = original;
            }, 3000);

            return;
        }

        clearTimeout(button.confirmTimer);

        button.disabled = true;
        showFeedback("", "");

        try {

            const response = await fetch(
                `/api/bookings/${encodeURIComponent(button.dataset.id)}/${button.dataset.action}`,
                { method: "POST", credentials: "include" }
            );

            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                showFeedback(data.error || "Nem sikerült a művelet.", "error");
            } else {
                showFeedback(
                    ({
                        confirm: "A foglalást elfogadtad.",
                        reject: "A foglalást elutasítottad.",
                        cancel: "A foglalást lemondtad."
                    })[button.dataset.action],
                    "success"
                );
            }

            await refresh();

        } catch (error) {

            console.error("Foglalási művelet hiba:", error);
            showFeedback("Hálózati hiba, próbáld újra.", "error");
            button.disabled = false;
        }
    });


    // ---------------- értékelő ablak ----------------
    //
    // 1-5 csillag + opcionális vélemény. Beküldés után nem szerkeszthető,
    // erre az ablak is figyelmeztet.

    function openReviewDialog(booking) {

        const start = new Date(booking.start_time);

        const modal = document.createElement("div");
        modal.className = "rv-modal";

        modal.innerHTML = `
            <div class="rv-overlay"></div>

            <div class="rv-window" role="dialog" aria-modal="true" aria-labelledby="rvTitle">

                <button type="button" class="rv-close" aria-label="Bezárás">×</button>

                <h2 id="rvTitle">Értékelés írása</h2>

                <p class="rv-sub">
                    ${esc(booking.subject_name || "Tantárgy")} ·
                    <strong>${esc(booking.other_name || "Oktató")}</strong> ·
                    ${esc(start.toLocaleDateString("hu-HU", { month: "long", day: "numeric" }))}
                </p>

                <div class="rv-stars" role="radiogroup" aria-label="Értékelés csillagokkal">
                    ${[1, 2, 3, 4, 5].map((n) => `
                        <button type="button" class="rv-star" data-value="${n}"
                            role="radio" aria-checked="false" aria-label="${n} csillag">★</button>
                    `).join("")}
                </div>

                <p class="rv-stars-label" id="rvStarsLabel" aria-live="polite">Válassz csillagot</p>

                <label class="rv-label" for="rvComment">
                    Vélemény <small>(nem kötelező)</small>
                </label>
                <textarea id="rvComment" maxlength="1000"
                    placeholder="Mi tetszett, miben segített az oktató?"></textarea>
                <div class="rv-counter" id="rvCounter">0 / 1000</div>

                <p class="rv-note">Az értékelés beküldés után nem szerkeszthető és nem törölhető.</p>

                <div class="rv-error" id="rvError" role="alert"></div>

                <div class="rv-actions">
                    <button type="button" class="bk-btn ghost" id="rvCancel">Mégse</button>
                    <button type="button" class="bk-btn primary" id="rvSubmit" disabled>Értékelés beküldése</button>
                </div>

            </div>
        `;

        document.body.appendChild(modal);

        const starButtons = Array.from(modal.querySelectorAll(".rv-star"));
        const label = modal.querySelector("#rvStarsLabel");
        const comment = modal.querySelector("#rvComment");
        const counter = modal.querySelector("#rvCounter");
        const submit = modal.querySelector("#rvSubmit");
        const errorBox = modal.querySelector("#rvError");

        let rating = 0;

        function paint(value) {

            starButtons.forEach((button) => {
                button.classList.toggle("on", Number(button.dataset.value) <= value);
            });

            label.textContent = value > 0 ? STAR_LABELS[value] : "Válassz csillagot";
        }

        function close() {
            document.removeEventListener("keydown", onKeyDown);
            modal.remove();
        }

        function onKeyDown(event) {
            if (event.key === "Escape") {
                close();
            }
        }

        document.addEventListener("keydown", onKeyDown);

        starButtons.forEach((button) => {

            // Egérrel fölé víve előnézet, elengedve visszaáll a választott érték
            button.addEventListener("mouseenter", () => paint(Number(button.dataset.value)));
            button.addEventListener("mouseleave", () => paint(rating));

            button.addEventListener("click", () => {

                rating = Number(button.dataset.value);

                starButtons.forEach((other) => {
                    other.setAttribute("aria-checked", String(other === button));
                });

                paint(rating);

                submit.disabled = false;
                errorBox.textContent = "";
            });
        });

        comment.addEventListener("input", () => {
            counter.textContent = comment.value.length + " / 1000";
        });

        modal.querySelector(".rv-overlay").addEventListener("click", close);
        modal.querySelector(".rv-close").addEventListener("click", close);
        modal.querySelector("#rvCancel").addEventListener("click", close);

        submit.addEventListener("click", async () => {

            if (rating === 0) {
                return;
            }

            submit.disabled = true;
            submit.textContent = "Küldés...";
            errorBox.textContent = "";

            try {

                const response = await fetch(
                    `/api/bookings/${encodeURIComponent(booking.id)}/review`,
                    {
                        method: "POST",
                        credentials: "include",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ rating, comment: comment.value })
                    }
                );

                const data = await response.json().catch(() => ({}));

                if (!response.ok) {

                    errorBox.textContent = data.error || "Nem sikerült menteni az értékelést.";

                    // Már értékelték (pl. másik fülön): a lista frissüljön
                    if (response.status === 409) {
                        refresh();
                    }

                    submit.textContent = "Értékelés beküldése";
                    submit.disabled = response.status === 409;

                    return;
                }

                close();
                showFeedback("Köszönjük az értékelést!", "success");
                await refresh();

            } catch (error) {

                console.error("Értékelési hiba:", error);

                errorBox.textContent = "Hálózati hiba, próbáld újra.";
                submit.textContent = "Értékelés beküldése";
                submit.disabled = false;
            }
        });

        starButtons[0].focus();
    }


    refresh();

})();
