// ============================================================
// ADMIN FELÜLET (kliens)
// ============================================================
//
// Fülek: Áttekintés, Jelentések, Felhasználók, Értékelések, Foglalások.
// Az adatokat a /api/admin/... végpontok adják; azok a szerveren is
// ellenőrzik, hogy a kérő admin-e, itt csak a megjelenítés van.

(function () {

    "use strict";

    const root = document.getElementById("admRoot");
    const tabs = document.getElementById("admTabs");
    const badge = document.getElementById("reportsBadge");

    const REASONS = {
        SPAM: "Spam vagy reklám",
        HARASSMENT: "Zaklatás, sértő viselkedés",
        SCAM: "Csalás, átverés",
        INAPPROPRIATE: "Nem megfelelő tartalom",
        OTHER: "Egyéb"
    };

    const ROLES = { STUDENT: "Diák", TUTOR: "Oktató", ADMIN: "Admin" };

    const REPORT_STATUS = {
        OPEN: ["Nyitott", "bad"],
        RESOLVED: ["Intézkedés történt", "ok"],
        DISMISSED: ["Elutasítva", "gray"]
    };

    const BOOKING_STATUS = {
        PENDING: ["Függőben", "warn"],
        CONFIRMED: ["Elfogadva", "ok"],
        REJECTED: ["Elutasítva", "gray"],
        CANCELLED: ["Lemondva", "bad"],
        COMPLETED: ["Teljesítve", "ok"]
    };

    const ACTIONS = {
        report_resolved: "Jelentés lezárva (intézkedés)",
        report_dismissed: "Jelentés elutasítva",
        report_reopened: "Jelentés újranyitva",
        user_suspended: "Felhasználó felfüggesztve",
        user_unsuspended: "Felfüggesztés feloldva",
        user_email_verified: "E-mail kézzel megerősítve",
        review_deleted: "Értékelés törölve"
    };


    // ---------------- segédek ----------------

    function esc(value) {

        return String(value === null || value === undefined ? "" : value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function date(value) {

        if (!value) {
            return "–";
        }

        return new Date(value).toLocaleString("hu-HU", {
            year: "numeric", month: "2-digit", day: "2-digit",
            hour: "2-digit", minute: "2-digit"
        });
    }

    function badge2(text, tone) {
        return `<span class="adm-badge ${tone || ""}">${esc(text)}</span>`;
    }

    async function api(url, options) {

        const response = await fetch(url, Object.assign({ credentials: "include" }, options || {}));

        const data = await response.json().catch(() => ({}));

        if (!response.ok) {
            throw new Error(data.error || "Hiba történt (" + response.status + ").");
        }

        return data;
    }

    function post(url, body) {

        return api(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body || {})
        });
    }

    function showError(error) {
        root.innerHTML = `<p class="adm-empty">${esc(error.message || error)}</p>`;
    }


    // ---------------- ablakok ----------------

    function openDialog(html, wide) {

        const overlay = document.createElement("div");
        overlay.className = "adm-overlay";
        overlay.innerHTML = `<div class="adm-dialog${wide ? " wide" : ""}" role="dialog" aria-modal="true">${html}</div>`;

        document.body.appendChild(overlay);

        function close() {
            overlay.remove();
            document.removeEventListener("keydown", onKey);
        }

        function onKey(event) {

            // csak a legfelső (utoljára megnyitott) ablak záródik
            const all = document.querySelectorAll(".adm-overlay");

            if (event.key === "Escape" && all[all.length - 1] === overlay) {
                close();
            }
        }

        document.addEventListener("keydown", onKey);

        overlay.addEventListener("mousedown", (event) => {
            if (event.target === overlay) {
                close();
            }
        });

        return { el: overlay.firstElementChild, close };
    }

    // Egyszerű "indoklás kérése" ablak. Visszaad egy Promise-t, ami a megadott
    // szöveggel teljesül (vagy null, ha mégsem).
    function askText({ title, label, placeholder, confirmText, danger, optional }) {

        return new Promise((resolve) => {

            const dialog = openDialog(`
                <h2>${esc(title)}</h2>
                <label class="adm-field">
                    <span>${esc(label)}</span>
                    <textarea maxlength="200" placeholder="${esc(placeholder || "")}"></textarea>
                </label>
                <p class="adm-error"></p>
                <div class="adm-dialog-actions">
                    <button type="button" class="adm-btn" data-cancel>Mégse</button>
                    <button type="button" class="adm-btn ${danger ? "danger" : "primary"}" data-ok>${esc(confirmText)}</button>
                </div>
            `);

            const input = dialog.el.querySelector("textarea");
            const error = dialog.el.querySelector(".adm-error");

            input.focus();

            dialog.el.querySelector("[data-cancel]").addEventListener("click", () => {
                dialog.close();
                resolve(null);
            });

            dialog.el.querySelector("[data-ok]").addEventListener("click", () => {

                const value = input.value.trim();

                if (!optional && value.length < 3) {
                    error.textContent = "Kérlek adj meg legalább pár szót.";
                    return;
                }

                dialog.close();
                resolve(value);
            });
        });
    }


    // ---------------- lapozó ----------------

    function pagerHtml(data) {

        const pages = Math.max(1, Math.ceil(data.total / data.pageSize));

        if (pages <= 1) {
            return `<div class="adm-pager"><span>${data.total} találat</span></div>`;
        }

        return `
            <div class="adm-pager">
                <button type="button" class="adm-btn" data-page="${data.page - 1}" ${data.page <= 1 ? "disabled" : ""}>←</button>
                <span>${data.page} / ${pages} · ${data.total} találat</span>
                <button type="button" class="adm-btn" data-page="${data.page + 1}" ${data.page >= pages ? "disabled" : ""}>→</button>
            </div>`;
    }

    function bindPager(container, onPage) {

        container.querySelectorAll("[data-page]").forEach((button) => {
            button.addEventListener("click", () => onPage(Number(button.dataset.page)));
        });
    }


    // ============================================================
    // ÁTTEKINTÉS
    // ============================================================

    async function renderOverview() {

        const data = await api("/api/admin/stats");

        updateBadge(data.reports.open);

        const bookingTotal = Object.values(data.bookings).reduce((sum, n) => sum + n, 0) || 1;

        const bars = Object.keys(BOOKING_STATUS).map((key) => {

            const n = data.bookings[key] || 0;

            return `
                <div class="adm-bar">
                    <span>${esc(BOOKING_STATUS[key][0])}</span>
                    <div class="adm-bar-track"><div class="adm-bar-fill" style="width:${Math.round((n / bookingTotal) * 100)}%"></div></div>
                    <strong>${n}</strong>
                </div>`;
        }).join("");

        const log = data.log.length
            ? data.log.map((item) => `
                <li>
                    <strong>${esc(ACTIONS[item.action] || item.action)}</strong>
                    ${item.details ? " – " + esc(item.details) : ""}
                    <small>${esc(item.admin_name)} · ${esc(date(item.created_at))}</small>
                </li>`).join("")
            : `<li>Még nem volt admin művelet.</li>`;

        root.innerHTML = `
            <div class="adm-grid">
                <div class="adm-stat ${data.reports.open ? "alert" : ""}"><strong>${data.reports.open}</strong><span>nyitott jelentés</span></div>
                <div class="adm-stat"><strong>${data.users.total}</strong><span>felhasználó (${data.users.students} diák, ${data.users.tutors} oktató)</span></div>
                <div class="adm-stat"><strong>${data.users.new_week}</strong><span>új felhasználó az elmúlt 7 napban</span></div>
                <div class="adm-stat"><strong>${data.users.unverified}</strong><span>megerősítetlen e-mail</span></div>
                <div class="adm-stat"><strong>${data.users.suspended}</strong><span>felfüggesztett fiók</span></div>
                <div class="adm-stat"><strong>${data.messages}</strong><span>elküldött üzenet</span></div>
                <div class="adm-stat"><strong>${data.reviews.count}</strong><span>értékelés${data.reviews.average ? " (átlag " + data.reviews.average.toFixed(1) + ")" : ""}</span></div>
            </div>

            <div class="adm-split">
                <section class="adm-panel">
                    <h2>Foglalások állapot szerint</h2>
                    <div class="adm-bars">${bars}</div>
                </section>

                <section class="adm-panel">
                    <h2>Legutóbbi admin műveletek</h2>
                    <ul class="adm-log">${log}</ul>
                </section>
            </div>
        `;
    }

    function updateBadge(open) {

        badge.hidden = !open;
        badge.textContent = open;
    }


    // ============================================================
    // JELENTÉSEK
    // ============================================================

    const reportState = { status: "OPEN", page: 1 };

    async function renderReports() {

        const filters = [["OPEN", "Nyitott"], ["RESOLVED", "Intézkedés történt"], ["DISMISSED", "Elutasított"], ["ALL", "Mind"]];

        root.innerHTML = `
            <div class="adm-filters">
                <div class="adm-chips">
                    ${filters.map(([key, label]) => `<button type="button" class="adm-chip ${reportState.status === key ? "active" : ""}" data-status="${key}">${label}</button>`).join("")}
                </div>
            </div>
            <div id="reportList"><p class="adm-empty">Betöltés...</p></div>`;

        root.querySelectorAll("[data-status]").forEach((button) => {
            button.addEventListener("click", () => {
                reportState.status = button.dataset.status;
                reportState.page = 1;
                renderReports().catch(showError);
            });
        });

        const data = await api(`/api/admin/reports?status=${reportState.status}&page=${reportState.page}`);

        const list = document.getElementById("reportList");

        if (reportState.status === "OPEN") {
            updateBadge(data.total);
        }

        if (!data.items.length) {
            list.innerHTML = `<p class="adm-empty">${reportState.status === "OPEN" ? "Nincs nyitott jelentés. 🎉" : "Nincs találat."}</p>`;
            return;
        }

        list.innerHTML = `
            <div class="adm-reports">
                ${data.items.map((r) => {

                    const [statusText, statusTone] = REPORT_STATUS[r.status] || [r.status, ""];

                    return `
                    <article class="adm-report ${r.status === "OPEN" ? "open" : ""}">
                        <div>
                            <h3>${esc(r.reported_name)} <span class="adm-sub" style="display:inline">(${esc(ROLES[r.reported_role] || r.reported_role)})</span></h3>
                            ${badge2(REASONS[r.reason] || r.reason, "warn")}
                            ${badge2(statusText, statusTone)}
                            ${r.reported_suspended ? badge2("Felfüggesztve", "bad") : ""}
                            ${r.other_reports > 0 ? badge2(r.other_reports + " további jelentés", "gray") : ""}
                            <p>Jelentette: ${esc(r.reporter_name)} · ${esc(date(r.created_at))}${r.message_id ? " · konkrét üzenet" : ""}</p>
                            ${r.details ? `<p>„${esc(r.details)}”</p>` : ""}
                        </div>
                        <button type="button" class="adm-btn ${r.status === "OPEN" ? "primary" : ""}" data-open="${r.id}">${r.status === "OPEN" ? "Megnyitás" : "Részletek"}</button>
                    </article>`;
                }).join("")}
            </div>
            ${pagerHtml(data)}`;

        list.querySelectorAll("[data-open]").forEach((button) => {
            button.addEventListener("click", () => openReport(Number(button.dataset.open)));
        });

        bindPager(list, (page) => {
            reportState.page = page;
            renderReports().catch(showError);
        });
    }


    async function openReport(id) {

        let detail;

        try {
            detail = await api("/api/admin/reports/" + id);
        } catch (error) {
            alert(error.message);
            return;
        }

        const { report, context } = detail;

        const [statusText, statusTone] = REPORT_STATUS[report.status] || [report.status, ""];

        const isOpen = report.status === "OPEN";

        const messages = context.length
            ? context.map((m) => {

                const isReported = Number(m.sender_id) === Number(report.reported_user_id);

                const who = isReported ? report.reported_name + " (jelentett)" : report.reporter_name + " (jelentő)";

                const body = m.retracted
                    ? `<em>Az üzenet a jelentés előtt vissza lett vonva.</em>`
                    : esc(m.content || "") + (m.attachment_name ? ` <em>📎 ${esc(m.attachment_name)}</em>` : "");

                return `<div class="adm-msg ${isReported ? "reported" : ""} ${m.target ? "target" : ""}">
                            <small>${esc(who)} · ${esc(date(m.created_at))}${m.target ? " · JELENTETT ÜZENET" : ""}</small>
                            ${body}
                        </div>`;
            }).join("")
            : `<em>Nincs mentett üzenet.</em>`;

        const dialog = openDialog(`
            <h2>Jelentés #${report.id} ${badge2(statusText, statusTone)}</h2>

            <dl class="adm-meta">
                <dt>Jelentő</dt><dd>${esc(report.reporter_name)}</dd>
                <dt>Jelentett</dt><dd>${esc(report.reported_name)}</dd>
                <dt>Ok</dt><dd>${esc(REASONS[report.reason] || report.reason)}</dd>
                <dt>Megjegyzés</dt><dd>${report.details ? esc(report.details) : "<em>nincs</em>"}</dd>
                <dt>Beküldve</dt><dd>${esc(date(report.created_at))}</dd>
                ${isOpen ? "" : `<dt>Lezárva</dt><dd>${esc(date(report.handled_at))}</dd>
                <dt>Admin megjegyzés</dt><dd>${report.admin_note ? esc(report.admin_note) : "<em>nincs</em>"}</dd>`}
            </dl>

            <strong>A jelentéskor mentett üzenetek</strong>
            <div class="adm-context" style="margin-top:8px">${messages}</div>

            ${isOpen ? `
                <label class="adm-field">
                    <span>Megjegyzés a döntéshez (nem kötelező)</span>
                    <textarea id="closeNote" maxlength="500"></textarea>
                </label>` : ""}

            <p class="adm-error" id="reportError"></p>

            <div class="adm-dialog-actions">
                <button type="button" class="adm-btn" data-close>Bezárás</button>
                ${isOpen ? `
                    <button type="button" class="adm-btn danger" data-suspend>Felhasználó felfüggesztése</button>
                    <button type="button" class="adm-btn" data-dismiss>Elutasítás (nem megalapozott)</button>
                    <button type="button" class="adm-btn primary" data-resolve>Intézkedés történt</button>
                ` : `<button type="button" class="adm-btn" data-reopen>Újranyitás</button>`}
            </div>
        `, true);

        const errorBox = dialog.el.querySelector("#reportError");

        const run = async (button, action) => {

            button.disabled = true;
            errorBox.textContent = "";

            try {

                await action();

                dialog.close();

                renderReports().catch(showError);

            } catch (error) {

                errorBox.textContent = error.message;
                button.disabled = false;
            }
        };

        const note = () => {
            const field = dialog.el.querySelector("#closeNote");
            return field ? field.value.trim() : "";
        };

        dialog.el.querySelector("[data-close]").addEventListener("click", dialog.close);

        const resolveButton = dialog.el.querySelector("[data-resolve]");
        const dismissButton = dialog.el.querySelector("[data-dismiss]");
        const suspendButton = dialog.el.querySelector("[data-suspend]");
        const reopenButton = dialog.el.querySelector("[data-reopen]");

        if (resolveButton) {
            resolveButton.addEventListener("click", () =>
                run(resolveButton, () => post(`/api/admin/reports/${id}/close`, { outcome: "RESOLVED", note: note() })));
        }

        if (dismissButton) {
            dismissButton.addEventListener("click", () =>
                run(dismissButton, () => post(`/api/admin/reports/${id}/close`, { outcome: "DISMISSED", note: note() })));
        }

        if (reopenButton) {
            reopenButton.addEventListener("click", () =>
                run(reopenButton, () => post(`/api/admin/reports/${id}/reopen`)));
        }

        if (suspendButton) {

            suspendButton.addEventListener("click", async () => {

                const reason = await askText({
                    title: report.reported_name + " felfüggesztése",
                    label: "Indoklás (ezt a felhasználó belépéskor látni fogja)",
                    placeholder: "pl. Zaklató üzenetek más felhasználók felé",
                    confirmText: "Felfüggesztés",
                    danger: true
                });

                if (reason === null) {
                    return;
                }

                run(suspendButton, async () => {

                    await post(`/api/admin/users/${report.reported_user_id}/suspend`, { reason });

                    // A jelentést is lezárjuk: az intézkedés megtörtént
                    await post(`/api/admin/reports/${id}/close`, {
                        outcome: "RESOLVED",
                        note: ("Felfüggesztve: " + reason).slice(0, 500)
                    });
                });
            });
        }
    }


    // ============================================================
    // FELHASZNÁLÓK
    // ============================================================

    const userState = { q: "", role: "", status: "", page: 1 };

    function renderUsersShell() {

        root.innerHTML = `
            <div class="adm-filters">
                <input type="search" class="adm-search" id="userSearch" placeholder="Keresés névre vagy e-mailre..." value="${esc(userState.q)}">
                <select id="userRole">
                    <option value="">Minden szerepkör</option>
                    <option value="STUDENT">Diák</option>
                    <option value="TUTOR">Oktató</option>
                    <option value="ADMIN">Admin</option>
                </select>
                <select id="userStatus">
                    <option value="">Minden állapot</option>
                    <option value="active">Aktív</option>
                    <option value="suspended">Felfüggesztett</option>
                    <option value="unverified">Megerősítetlen e-mail</option>
                    <option value="deleted">Törölt</option>
                </select>
            </div>
            <div id="userList"><p class="adm-empty">Betöltés...</p></div>`;

        const search = document.getElementById("userSearch");
        const role = document.getElementById("userRole");
        const status = document.getElementById("userStatus");

        role.value = userState.role;
        status.value = userState.status;

        if (window.TCSelect) {
            TCSelect.enhance(role);
            TCSelect.enhance(status);
        }

        let timer = null;

        search.addEventListener("input", () => {
            clearTimeout(timer);
            timer = setTimeout(() => {
                userState.q = search.value.trim();
                userState.page = 1;
                loadUsers().catch(showError);
            }, 300);
        });

        role.addEventListener("change", () => {
            userState.role = role.value;
            userState.page = 1;
            loadUsers().catch(showError);
        });

        status.addEventListener("change", () => {
            userState.status = status.value;
            userState.page = 1;
            loadUsers().catch(showError);
        });
    }

    async function loadUsers() {

        const params = new URLSearchParams({
            q: userState.q, role: userState.role, status: userState.status, page: userState.page
        });

        const data = await api("/api/admin/users?" + params);

        const list = document.getElementById("userList");

        if (!data.items.length) {
            list.innerHTML = `<p class="adm-empty">Nincs találat.</p>`;
            return;
        }

        list.innerHTML = `
            <div class="adm-table-wrap">
                <table class="adm-table">
                    <thead><tr>
                        <th>Név</th><th>Szerepkör</th><th>Állapot</th><th>Regisztrált</th><th>Jelentések</th><th></th>
                    </tr></thead>
                    <tbody>
                    ${data.items.map((u) => {

                        const deleted = Boolean(u.deleted_at);
                        const suspended = Boolean(u.suspended_at);

                        const states = [];
                        if (deleted) states.push(badge2("Törölt", "gray"));
                        else if (suspended) states.push(badge2("Felfüggesztett", "bad"));
                        else states.push(badge2("Aktív", "ok"));
                        if (!deleted && !u.email_verified_at) states.push(badge2("E-mail nincs megerősítve", "warn"));

                        const actions = [];
                        if (!deleted && u.role !== "ADMIN") {
                            actions.push(suspended
                                ? `<button type="button" class="adm-btn" data-unsuspend="${u.id}">Feloldás</button>`
                                : `<button type="button" class="adm-btn danger" data-suspend="${u.id}" data-name="${esc(u.full_name)}">Felfüggesztés</button>`);
                        }
                        if (!deleted && !u.email_verified_at) {
                            actions.push(`<button type="button" class="adm-btn" data-verify="${u.id}">E-mail megerősítése</button>`);
                        }

                        return `<tr>
                            <td><strong>${esc(u.full_name)}</strong><span class="adm-sub">${esc(u.email)}</span>
                                ${suspended && u.suspended_reason ? `<span class="adm-sub">Indoklás: ${esc(u.suspended_reason)}</span>` : ""}</td>
                            <td>${esc(ROLES[u.role] || u.role)}</td>
                            <td>${states.join(" ")}</td>
                            <td>${esc(date(u.created_at))}</td>
                            <td>${u.open_reports > 0 ? badge2(u.open_reports + " nyitott", "bad") + " " : ""}${u.all_reports > 0 ? `<span class="adm-sub" style="display:inline">${u.all_reports} összesen</span>` : "–"}</td>
                            <td><div class="adm-actions">${actions.join("")}</div></td>
                        </tr>`;
                    }).join("")}
                    </tbody>
                </table>
            </div>
            ${pagerHtml(data)}`;

        bindPager(list, (page) => {
            userState.page = page;
            loadUsers().catch(showError);
        });

        const reload = () => loadUsers().catch(showError);

        list.querySelectorAll("[data-suspend]").forEach((button) => {

            button.addEventListener("click", async () => {

                const reason = await askText({
                    title: button.dataset.name + " felfüggesztése",
                    label: "Indoklás (ezt a felhasználó belépéskor látni fogja)",
                    placeholder: "pl. Többszöri szabálysértés",
                    confirmText: "Felfüggesztés",
                    danger: true
                });

                if (reason === null) {
                    return;
                }

                try {
                    await post(`/api/admin/users/${button.dataset.suspend}/suspend`, { reason });
                    reload();
                } catch (error) {
                    alert(error.message);
                }
            });
        });

        list.querySelectorAll("[data-unsuspend]").forEach((button) => {

            button.addEventListener("click", async () => {

                if (!confirm("Biztosan feloldod a felfüggesztést?")) {
                    return;
                }

                try {
                    await post(`/api/admin/users/${button.dataset.unsuspend}/unsuspend`);
                    reload();
                } catch (error) {
                    alert(error.message);
                }
            });
        });

        list.querySelectorAll("[data-verify]").forEach((button) => {

            button.addEventListener("click", async () => {

                try {
                    await post(`/api/admin/users/${button.dataset.verify}/verify-email`);
                    reload();
                } catch (error) {
                    alert(error.message);
                }
            });
        });
    }

    async function renderUsers() {

        renderUsersShell();

        await loadUsers();
    }


    // ============================================================
    // ÉRTÉKELÉSEK
    // ============================================================

    const reviewState = { page: 1 };

    async function renderReviews() {

        const data = await api("/api/admin/reviews?page=" + reviewState.page);

        if (!data.items.length) {
            root.innerHTML = `<p class="adm-empty">Még nincs értékelés.</p>`;
            return;
        }

        root.innerHTML = `
            <div class="adm-table-wrap">
                <table class="adm-table">
                    <thead><tr><th>Oktató</th><th>Értékelő diák</th><th>Pont</th><th>Szöveg</th><th>Dátum</th><th></th></tr></thead>
                    <tbody>
                    ${data.items.map((r) => `
                        <tr>
                            <td><strong>${esc(r.tutor_name)}</strong></td>
                            <td>${esc(r.student_name)}</td>
                            <td>${"★".repeat(Number(r.rating))}<span class="adm-sub">${esc(r.rating)} / 5</span></td>
                            <td style="max-width:340px">${r.comment ? esc(r.comment) : "<em class='adm-sub'>nincs szöveg</em>"}</td>
                            <td>${esc(date(r.created_at))}</td>
                            <td><div class="adm-actions"><button type="button" class="adm-btn danger" data-delete="${r.id}">Törlés</button></div></td>
                        </tr>`).join("")}
                    </tbody>
                </table>
            </div>
            ${pagerHtml(data)}`;

        bindPager(root, (page) => {
            reviewState.page = page;
            renderReviews().catch(showError);
        });

        root.querySelectorAll("[data-delete]").forEach((button) => {

            button.addEventListener("click", async () => {

                const reason = await askText({
                    title: "Értékelés törlése",
                    label: "Miért törlöd? (naplózzuk)",
                    placeholder: "pl. Sértő megfogalmazás",
                    confirmText: "Törlés",
                    danger: true
                });

                if (reason === null) {
                    return;
                }

                try {

                    await api("/api/admin/reviews/" + button.dataset.delete, {
                        method: "DELETE",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ reason })
                    });

                    renderReviews().catch(showError);

                } catch (error) {
                    alert(error.message);
                }
            });
        });
    }


    // ============================================================
    // FOGLALÁSOK (csak olvasható)
    // ============================================================

    const bookingState = { status: "", page: 1 };

    async function renderBookings() {

        const filters = [["", "Mind"], ...Object.entries(BOOKING_STATUS).map(([key, value]) => [key, value[0]])];

        const data = await api(`/api/admin/bookings?status=${bookingState.status}&page=${bookingState.page}`);

        root.innerHTML = `
            <div class="adm-filters">
                <div class="adm-chips">
                    ${filters.map(([key, label]) => `<button type="button" class="adm-chip ${bookingState.status === key ? "active" : ""}" data-status="${key}">${esc(label)}</button>`).join("")}
                </div>
            </div>
            ${data.items.length ? `
            <div class="adm-table-wrap">
                <table class="adm-table">
                    <thead><tr><th>#</th><th>Diák</th><th>Oktató</th><th>Tantárgy</th><th>Időpont</th><th>Ár</th><th>Állapot</th></tr></thead>
                    <tbody>
                    ${data.items.map((b) => {

                        const [text, tone] = BOOKING_STATUS[b.status] || [b.status || "–", "gray"];

                        return `<tr>
                            <td>${b.id}</td>
                            <td>${esc(b.student_name || "–")}</td>
                            <td>${esc(b.tutor_name || "–")}</td>
                            <td>${esc(b.subject_name || "–")}</td>
                            <td>${esc(date(b.start_time))}</td>
                            <td>${b.price ? esc(Number(b.price).toLocaleString("hu-HU")) + " Ft" : "–"}</td>
                            <td>${badge2(text, tone)}</td>
                        </tr>`;
                    }).join("")}
                    </tbody>
                </table>
            </div>
            ${pagerHtml(data)}` : `<p class="adm-empty">Nincs találat.</p>`}`;

        root.querySelectorAll("[data-status]").forEach((button) => {
            button.addEventListener("click", () => {
                bookingState.status = button.dataset.status;
                bookingState.page = 1;
                renderBookings().catch(showError);
            });
        });

        bindPager(root, (page) => {
            bookingState.page = page;
            renderBookings().catch(showError);
        });
    }


    // ============================================================
    // FÜLEK
    // ============================================================

    const RENDERERS = {
        overview: renderOverview,
        reports: renderReports,
        users: renderUsers,
        reviews: renderReviews,
        bookings: renderBookings
    };

    function show(tab) {

        if (!RENDERERS[tab]) {
            tab = "overview";
        }

        tabs.querySelectorAll(".adm-tab").forEach((button) => {
            button.classList.toggle("active", button.dataset.tab === tab);
        });

        history.replaceState(null, "", "#" + tab);

        root.innerHTML = `<p class="adm-empty">Betöltés...</p>`;

        RENDERERS[tab]().catch(showError);
    }

    tabs.addEventListener("click", (event) => {

        const button = event.target.closest(".adm-tab");

        if (button) {
            show(button.dataset.tab);
        }
    });

    show(location.hash.replace("#", "") || "overview");

    // a jelvény az első betöltésnél is pontos legyen
    api("/api/admin/stats").then((data) => updateBadge(data.reports.open)).catch(() => {});

})();
