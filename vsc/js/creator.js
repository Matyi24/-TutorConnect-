const body = document.body;


// ============================================================
// THEME (dark / light)
// ============================================================
//
// The button only switches the theme: the chosen value ("light" or
// "dark") is stored in localStorage and set as a data-theme attribute
// on <html>. There is NO dark styling here, so style it yourself
// in your CSS, e.g.  html[data-theme="dark"] { ... }
//
// ============================================================

const TC_THEME_KEY = "tc-theme";

function tcGetTheme() {

    try {

        const saved = localStorage.getItem(TC_THEME_KEY);

        if (saved === "dark" || saved === "light") {
            return saved;
        }

    } catch (error) {
        // localStorage can be blocked; fall through to the default
    }

    return "light";
}

function tcApplyTheme(theme) {

    document.documentElement.setAttribute("data-theme", theme);

    const toggle = document.getElementById("themeToggle");

    if (toggle) {
        toggle.setAttribute("aria-pressed", String(theme === "dark"));
    }
}

// Apply right away, so the page doesn't flash light first
tcApplyTheme(tcGetTheme());


function tcThemeToggleHtml() {

    return `

        <button
            type="button"
            class="theme-toggle"
            id="themeToggle"
            aria-label="Sötét / világos mód váltása"
            title="Sötét / világos mód"
        >

            <svg class="icon-moon" viewBox="0 0 24 24" fill="none"
                 stroke="currentColor" stroke-width="2"
                 stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/>
            </svg>

            <svg class="icon-sun" viewBox="0 0 24 24" fill="none"
                 stroke="currentColor" stroke-width="2"
                 stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="4"/>
                <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>
            </svg>

        </button>

    `;
}

function tcSetupThemeToggle() {

    const toggle = document.getElementById("themeToggle");

    if (!toggle) {
        return;
    }

    tcApplyTheme(tcGetTheme());

    toggle.addEventListener("click", () => {

        const next =
            document.documentElement.getAttribute("data-theme") === "dark"
                ? "light"
                : "dark";

        tcApplyTheme(next);

        try {
            localStorage.setItem(TC_THEME_KEY, next);
        } catch (error) {
            // not saved, but the theme still changed for this page
        }
    });
}


// ============================================================
// SMALL HELPERS FOR THE NAVBAR USER
// ============================================================

function tcInitials(name) {

    return String(name).trim().split(/\s+/).slice(0, 2)
        .map(part => part[0] ? part[0].toUpperCase() : "")
        .join("");
}

function tcUpdateNavUser(name) {

    const navName = document.getElementById("navName");
    const navAvatar = document.getElementById("navAvatar");

    if (navName) {
        navName.textContent = name;
    }

    if (navAvatar) {
        navAvatar.textContent = tcInitials(name);
    }
}


// ============================================================
// NAVBAR
// ============================================================

async function createNavbar() {

    // The navbar styles (avatar, theme button) are needed for
    // logged-out visitors too, so inject them first.
    injectAccountStyles();

    let auth = {
        loggedIn: false,
        user: null
    };


    // --------------------------------------------------------
    // ASK SERVER WHO IS LOGGED IN
    // --------------------------------------------------------
    //
    // The login itself is NOT stored in this file or in the browser's
    // JavaScript. It lives in a session on the server, and the browser
    // only holds a session cookie.
    //
    // So on every page load we ask the server "who am I?" (/api/me).
    // The server looks at the cookie and answers with
    // { loggedIn: true, user: {...} } or { loggedIn: false }.
    // The navbar is then built based on that answer.
    // --------------------------------------------------------

    try {

        const response = await fetch(
            "/api/me",
            {
                method: "GET",

                // "include" = send the session cookie (connect.sid) along
                // with the request. Without it the server couldn't
                // recognise the user.

                credentials: "include"
            }
        );


        if (response.ok) {

            // Replace the default "logged out" object with the server's answer
            auth = await response.json();
        }

    } catch (error) {

        console.error(
            "Nem sikerült lekérni a bejelentkezési állapotot:",
            error
        );
    }


    // --------------------------------------------------------
    // USER DATA
    // --------------------------------------------------------

    const user = auth.user;

    // true only if the server said loggedIn AND sent user data
    const isLoggedIn =
        auth.loggedIn &&
        user;


    let navLinks = "";

    let accountArea = "";


    // ========================================================
    // NOT LOGGED IN
    // ========================================================

    if (!isLoggedIn) {

        /*navLinks = `

            <a href="/index">
                <li>Főoldal</li>
            </a>


            <a href="/subs">
                <li>Tantárgyak</li>
            </a>


            <a href="/gyik">
                <li>GYIK</li>
            </a>

        `;*/
        navLinks = `
            <a href="/index">
                <li>Főoldal</li>
            </a>


            <a href="/chat">
                <li>Chat</li>
            </a>


            <a href="/subs">
                <li>Tantárgyak</li>
            </a>

            <a href="/gyik">
                <li>GYIK</li>
            </a>

            <a href="/oktatok">
                <li>Oktatók</li>
            </a>

            <a href="/foglalas">
                <li>Foglalás</li>
            </a>
        `;

        accountArea = `

            <a href="/login">

                <button class="login">
                    Belépés
                </button>

            </a>


            <a href="/register">

                <button class="register">
                    Regisztráció
                </button>

            </a>

        `;
    }


    // ========================================================
    // LOGGED IN
    // ========================================================

    else {

        // ----------------------------------------------------
        // EVERY LOGGED-IN USER
        // ----------------------------------------------------

        navLinks = `

            <a href="/index">
                <li>Főoldal</li>
            </a>


            <a href="/chat">
                <li>Chat</li>
            </a>


            <a href="/subs">
                <li>Tantárgyak</li>
            </a>
            

        `;


        // ====================================================
        // TUTOR
        // ====================================================

        if (user.role === "TUTOR") {

            navLinks += `

                <a href="/foglalas">
                    <li>Foglalás</li>
                </a>

            `;
        }


        // ====================================================
        // STUDENT
        // ====================================================

        if (user.role === "STUDENT") {

            navLinks += `

                <a href="/oktatok">
                    <li>Oktatók</li>
                </a>


                <a href="/foglalas">
                    <li>Foglalás</li>
                </a>

            `;
        }


        // ----------------------------------------------------
        // USER NAME + LOGOUT
        // ----------------------------------------------------

        accountArea = `

            <div class="user-menu">

                <div
                    class="user-chip"
                    id="accountButton"
                    role="button"
                    tabindex="0"
                    title="Fiókom szerkesztése"
                >

                    <span class="nav-avatar" id="navAvatar">
                        ${escapeHtml(tcInitials(user.name))}
                    </span>

                    <span class="user-name" id="navName">
                        ${escapeHtml(user.name)}
                    </span>

                </div>

                ${tcThemeToggleHtml()}

            </div>

        `;
    }


    // Logged-out visitors get the theme circle after the buttons
    if (!isLoggedIn) {
        accountArea += tcThemeToggleHtml();
    }


    // ========================================================
    //  NAVBAR
    // ========================================================

    body.insertAdjacentHTML(

        "afterbegin",

        `

        <nav class="navbar">


            <!-- LOGO -->

            <a href="/index">

                <div class="logo">
                    TutorConnect
                </div>

            </a>


            <!-- NAVIGATION -->

            <ul class="nav-links">

                ${navLinks}

            </ul>


            <!-- LOGIN / USER -->

            <div class="cta-buttons">

                ${accountArea}

            </div>


        </nav>

        `
    );


    // ========================================================
    // ACCOUNT MODAL (click on the user's name)
    // ========================================================

    tcSetupThemeToggle();

    if (isLoggedIn) {
        setupAccountModal(user);
    }


    // ========================================================
    // LOGOUT BUTTON
    // ========================================================

    const logoutButton =
        document.getElementById(
            "logoutButton"
        );


    if (logoutButton) {

        logoutButton.addEventListener(
            "click",
            async () => {

                try {

                    const response =
                        await fetch(
                            "/logout",
                            {
                                method: "POST",

                                // Send the cookie so the server knows
                                // WHICH session to destroy.

                                credentials: "include"
                            }
                        );


                    if (response.ok) {

                        // After logout,
                        // return to homepage.

                        window.location.href =
                            "/index";

                    } else {

                        alert(
                            "Nem sikerült kijelentkezni."
                        );
                    }

                } catch (error) {

                    console.error(
                        "Logout error:",
                        error
                    );

                    alert(
                        "Nem sikerült kijelentkezni."
                    );
                }
            }
        );
    }
}


// ============================================================
// ACCOUNT MODAL
// ============================================================
//
// Opens when the logged-in user clicks on their name in the navbar.
// It loads the account data from GET /api/account and saves changes
// with PUT /api/account.
//
// ============================================================

function injectAccountStyles() {

    if (document.getElementById("accStyles")) {
        return;
    }

    const style = document.createElement("style");
    style.id = "accStyles";

    style.textContent = `

        /* ---------- navbar: avatar, name, theme circle ---------- */

        .user-menu { display: flex; align-items: center; gap: 12px; }

        .user-chip {
            display: flex; align-items: center; gap: 10px; cursor: pointer;
            padding: 3px 14px 3px 3px; border-radius: 999px; transition: background .2s;
        }
        .user-chip:hover { background: rgba(91, 77, 224, .10); }
        .user-chip:focus-visible { outline: 2px solid #5b4de0; outline-offset: 2px; }

        .user-name { cursor: pointer; transition: color .2s; }
        .user-chip:hover .user-name { color: #4de0a3; }

        .nav-avatar {
            flex: none; box-sizing: border-box; width: 36px; height: 36px; border-radius: 50%;
            display: flex; align-items: center; justify-content: center;
            background: linear-gradient(135deg, #7a6cf0, #5b4de0);
            color: #fff; font-weight: 700; font-size: 13px; line-height: 1;
        }

        .theme-toggle {
            flex: none; box-sizing: border-box; width: 36px; height: 36px; min-width: 0;
            margin: 0; padding: 0; border-radius: 50%;
            display: inline-flex; align-items: center; justify-content: center;
            border: 1px solid #e0dbfa; background: #f1eeff; color: #5b4de0;
            cursor: pointer; vertical-align: middle; line-height: 1;
            transition: background .2s, transform .25s;
        }
        .theme-toggle:hover { background: #e4dffc; transform: rotate(18deg); }
        .theme-toggle svg { width: 18px; height: 18px; }
        .theme-toggle .icon-sun { display: none; }
        html[data-theme="dark"] .theme-toggle .icon-moon { display: none; }
        html[data-theme="dark"] .theme-toggle .icon-sun { display: inline; }
        .cta-buttons > .theme-toggle { margin-left: 10px; }

        .acc-modal {
            position: fixed; inset: 0; z-index: 9999;
            display: none; align-items: center; justify-content: center;
            padding: 20px; font-family: inherit;
        }
        .acc-modal.open { display: flex; }

        .acc-overlay {
            position: absolute; inset: 0;
            background: rgba(27, 23, 53, .45);
            backdrop-filter: blur(4px);
        }

        .acc-window {
            position: relative; box-sizing: border-box; width: 100%; max-width: 880px; max-height: 92vh;
            overflow-y: auto; background: #fdfcff; border-radius: 26px;
            padding: 32px; box-shadow: 0 30px 80px rgba(60, 40, 160, .28);
            animation: accIn .25s ease;
        }
        @keyframes accIn {
            from { opacity: 0; transform: translateY(14px) scale(.98); }
            to   { opacity: 1; transform: none; }
        }

        .acc-close {
            position: absolute; top: 16px; right: 18px; padding: 0;
            width: 36px; height: 36px; border: 0; border-radius: 50%;
            background: #f1eeff; color: #5b4de0; font-size: 22px;
            line-height: 1; cursor: pointer;
        }
        .acc-close:hover { background: #e4dffc; }

        .acc-header { display: flex; align-items: center; gap: 16px; margin-bottom: 24px; }

        .acc-avatar {
            flex: none; width: 60px; height: 60px; border-radius: 50%;
            display: flex; align-items: center; justify-content: center;
            background: linear-gradient(135deg, #7a6cf0, #5b4de0);
            color: #fff; font-weight: 700; font-size: 20px;
        }

        .acc-label {
            font-size: 11px; font-weight: 700; letter-spacing: .18em; color: #5b4de0;
        }
        .acc-header h2 { margin: 2px 0 0; font-size: 22px; color: #1b1735; }

        .acc-field { margin-bottom: 16px; }
        .acc-field label {
            display: block; margin-bottom: 6px;
            font-size: 13px; font-weight: 600; color: #1b1735;
        }
        .acc-field input, .acc-field textarea {
            width: 100%; box-sizing: border-box; padding: 12px 14px;
            border: 1px solid #e0dbfa; border-radius: 12px; background: #fff;
            font: inherit; font-size: 14px; color: #1b1735;
        }
        .acc-field textarea { min-height: 90px; resize: vertical; }
        .acc-field input:focus, .acc-field textarea:focus {
            outline: none; border-color: #5b4de0; box-shadow: 0 0 0 3px rgba(91, 77, 224, .15);
        }

        .acc-columns {
            display: grid; grid-template-columns: 1fr 1.25fr; gap: 28px;
            align-items: start; margin-bottom: 8px;
        }

        /* left side: password panel */
        .acc-col-password {
            background: #f4f2ff; border: 1px solid #e6e1fb;
            border-radius: 20px; padding: 22px;
        }

        .acc-section {
            margin: 22px 0 14px; padding-top: 18px; border-top: 1px solid #ece8fb;
            font-size: 13px; font-weight: 700; color: #6b6688;
        }
        .acc-section:first-child {
            margin-top: 0; padding-top: 0; border-top: 0;
            color: #5b4de0; font-size: 12px; letter-spacing: .14em; text-transform: uppercase;
        }

        @media (max-width: 760px) {
            .acc-columns { grid-template-columns: 1fr; }
            .acc-col-profile { order: -1; }   /* profile data first on phones */
        }

        .acc-hint { font-size: 12px; color: #6b6688; margin: -8px 0 14px; }

        .acc-message { min-height: 20px; margin-bottom: 12px; font-size: 13px; font-weight: 600; }
        .acc-message.error { color: #d6336c; }
        .acc-message.success { color: #1f9d6b; }

        .acc-actions { display: flex; gap: 10px; justify-content: flex-end; }
        .acc-actions button {
            padding: 12px 22px; border-radius: 12px; border: 0;
            font: inherit; font-size: 14px; font-weight: 700; cursor: pointer;
        }
        .acc-cancel { background: #f1eeff; color: #5b4de0; }
        .acc-save { background: #5b4de0; color: #fff; }
        .acc-save:hover { background: #4a3dcb; }
        .acc-save:disabled { opacity: .6; cursor: default; }

        .acc-modal [hidden] { display: none !important; }

        /* logout button on the left of the action row */
        .acc-actions .logout { margin-right: auto; background: #fff0f4; color: #d6336c; }
        .acc-actions .logout:hover { background: #ffe0e8; }

        /* ---------- tutor: subjects and prices ---------- */

        .acc-subjects {
            margin: 4px 0 18px; padding: 22px;
            background: #f4f2ff; border: 1px solid #e6e1fb; border-radius: 20px;
        }
        .acc-subj-head, .acc-subj-row {
            display: grid; grid-template-columns: 1fr 130px 36px; gap: 10px; align-items: center;
        }
        .acc-subj-head {
            margin-bottom: 8px; padding: 0 2px;
            font-size: 13px; font-weight: 600; color: #1b1735;
        }
        .acc-subj-row { margin-bottom: 10px; }
        .acc-subj-select, .acc-subj-price {
            width: 100%; box-sizing: border-box; min-width: 0; height: 44px; padding: 0 14px;
            border: 1px solid #e0dbfa; border-radius: 12px; background: #fff;
            font: inherit; font-size: 14px; color: #1b1735;
        }
        .acc-subj-select:focus, .acc-subj-price:focus {
            outline: none; border-color: #5b4de0; box-shadow: 0 0 0 3px rgba(91, 77, 224, .15);
        }
        .acc-subj-remove {
            box-sizing: border-box; width: 36px; height: 36px; min-width: 0; margin: 0; padding: 0;
            border: 0; border-radius: 50%; background: #fff0f4; color: #d6336c;
            font-size: 20px; line-height: 1; cursor: pointer;
        }
        .acc-subj-remove:hover { background: #ffe0e8; }
        .acc-add-row {
            margin: 2px 0 0; padding: 10px 16px; border: 1px dashed #b9b0f2; border-radius: 12px;
            background: transparent; color: #5b4de0; font: inherit; font-size: 13px; font-weight: 700;
            cursor: pointer;
        }
        .acc-add-row:hover { background: #ebe7fd; }

        @media (max-width: 520px) {
            .acc-subjects { padding: 16px; }
            .acc-subj-head { display: none; }
            /* phone: the subject gets its own line, price + delete sit below it */
            .acc-subj-row {
                grid-template-columns: 1fr 36px; gap: 8px;
                padding-bottom: 12px; margin-bottom: 12px; border-bottom: 1px solid #e6e1fb;
            }
            .acc-subj-select { grid-column: 1 / -1; }
        }

    `;

    document.head.appendChild(style);
}


function setupAccountModal(user) {

    const trigger = document.getElementById("accountButton");

    if (!trigger) {
        return;
    }

    injectAccountStyles();


    body.insertAdjacentHTML("beforeend", `

        <div class="acc-modal" id="accModal" aria-hidden="true">

            <div class="acc-overlay" id="accOverlay"></div>

            <div class="acc-window" role="dialog" aria-modal="true" aria-labelledby="accTitle">

                <button type="button" class="acc-close" id="accClose" aria-label="Bezárás">×</button>

                <div class="acc-header">
                    <div class="acc-avatar" id="accAvatar"></div>
                    <div>
                        <span class="acc-label">FIÓKOM</span>
                        <h2 id="accTitle"></h2>
                    </div>
                </div>

                <form id="accForm" novalidate>

                    <div class="acc-columns">

                        <!-- LEFT: password -->
                        <div class="acc-col acc-col-password">

                            <div class="acc-section">Jelszó módosítása</div>
                            <p class="acc-hint">
                                Csak akkor töltsd ki, ha jelszót vagy e-mail címet változtatsz.
                            </p>

                            <div class="acc-field">
                                <label for="accCurrentPw">Jelenlegi jelszó</label>
                                <input type="password" id="accCurrentPw" autocomplete="current-password">
                            </div>

                            <div class="acc-field">
                                <label for="accNewPw">Új jelszó</label>
                                <input type="password" id="accNewPw" autocomplete="new-password">
                            </div>

                            <div class="acc-field">
                                <label for="accNewPw2">Új jelszó megerősítése</label>
                                <input type="password" id="accNewPw2" autocomplete="new-password">
                            </div>

                        </div>


                        <!-- RIGHT: profile data -->
                        <div class="acc-col acc-col-profile">

                            <div class="acc-field">
                                <label for="accName">Név</label>
                                <input type="text" id="accName" maxlength="60" required>
                            </div>

                            <div class="acc-field">
                                <label for="accEmail">E-mail</label>
                                <input type="email" id="accEmail" maxlength="30" required>
                            </div>

                            <div id="accTutorFields" hidden>

                                <div class="acc-field">
                                    <label for="accBio">Bemutatkozás</label>
                                    <textarea id="accBio" maxlength="1000"
                                        placeholder="Írj magadról pár mondatot..."></textarea>
                                </div>

                            </div>

                        </div>

                    </div>


                    <!-- TUTOR ONLY: what they teach and for how much -->
                    <div class="acc-subjects" id="accSubjectsBox" hidden>

                        <div class="acc-section">Tantárgyaim és óradíjak</div>
                        <p class="acc-hint">
                            Válaszd ki, mit tanítasz, és add meg az óradíjat forintban.
                        </p>

                        <div class="acc-subj-head">
                            <span>Tantárgy</span>
                            <span>Ár / óra</span>
                            <span></span>
                        </div>

                        <div id="accSubjectRows"></div>

                        <button type="button" class="acc-add-row" id="accAddRow">
                            + Tantárgy hozzáadása
                        </button>

                    </div>

                    <div class="acc-message" id="accMessage" role="status"></div>

                    <div class="acc-actions">
                        <button type="button" class="logout" id="logoutButton">Kilépés</button>
                        <button type="button" class="acc-cancel" id="accCancel">Mégse</button>
                        <button type="submit" class="acc-save" id="accSave">Mentés</button>
                    </div>

                </form>

            </div>

        </div>

    `);


    const modal = document.getElementById("accModal");
    const form = document.getElementById("accForm");
    const message = document.getElementById("accMessage");
    const saveButton = document.getElementById("accSave");

    const field = (id) => document.getElementById(id);


    function showMessage(text, type) {
        message.textContent = text;
        message.className = "acc-message " + (type || "");
    }

    function clearPasswords() {
        field("accCurrentPw").value = "";
        field("accNewPw").value = "";
        field("accNewPw2").value = "";
    }

    function setHeader(name) {
        field("accTitle").textContent = name;
        field("accAvatar").textContent = tcInitials(name);
    }


    // ---------------- tutor: subjects and prices ----------------

    const MAX_SUBJECT_ROWS = 10;
    let subjectCache = null;            // [{ id, name, category }]

    async function loadSubjectList() {

        if (subjectCache) {
            return subjectCache;
        }

        const response = await fetch("/api/subjects", { credentials: "include" });

        if (!response.ok) {
            throw new Error("HTTP " + response.status);
        }

        subjectCache = await response.json();

        return subjectCache;
    }

    function buildSubjectSelect(selectedId) {

        const select = document.createElement("select");
        select.className = "acc-subj-select";
        select.setAttribute("aria-label", "Tantárgy");

        const placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = "Válassz tantárgyat...";
        select.appendChild(placeholder);

        // group the subjects by category
        const groups = new Map();

        subjectCache.forEach(subject => {

            const category = subject.category || "Egyéb";

            if (!groups.has(category)) {
                groups.set(category, []);
            }

            groups.get(category).push(subject);
        });

        groups.forEach((list, category) => {

            const group = document.createElement("optgroup");
            group.label = category;

            list.forEach(subject => {

                const option = document.createElement("option");
                option.value = String(subject.id);
                option.textContent = subject.name;
                group.appendChild(option);
            });

            select.appendChild(group);
        });

        select.value = selectedId ? String(selectedId) : "";

        return select;
    }

    // A subject can only be picked once: disable it in the other rows
    function refreshSubjectRows() {

        const rows = field("accSubjectRows");
        const selects = Array.from(rows.querySelectorAll(".acc-subj-select"));
        const chosen = new Set(selects.map(select => select.value).filter(Boolean));

        selects.forEach(select => {
            Array.from(select.options).forEach(option => {
                option.disabled =
                    option.value !== "" &&
                    chosen.has(option.value) &&
                    option.value !== select.value;
            });
        });

        field("accAddRow").hidden = rows.children.length >= MAX_SUBJECT_ROWS;
    }

    function addSubjectRow(subjectId, rate) {

        const rows = field("accSubjectRows");

        if (rows.children.length >= MAX_SUBJECT_ROWS) {
            return;
        }

        const row = document.createElement("div");
        row.className = "acc-subj-row";

        const select = buildSubjectSelect(subjectId);

        const price = document.createElement("input");
        price.type = "number";
        price.className = "acc-subj-price";
        price.min = "0";
        price.max = "100000";
        price.step = "100";
        price.placeholder = "Ft / óra";
        price.setAttribute("aria-label", "Óradíj (Ft)");

        if (rate !== undefined && rate !== null) {
            price.value = rate;
        }

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "acc-subj-remove";
        remove.textContent = "×";
        remove.setAttribute("aria-label", "Sor törlése");

        remove.addEventListener("click", () => {

            if (rows.children.length > 1) {
                row.remove();
            } else {
                // keep one empty row so the tutor can start again
                select.value = "";
                price.value = "";
            }

            refreshSubjectRows();
        });

        select.addEventListener("change", refreshSubjectRows);

        row.append(select, price, remove);
        rows.appendChild(row);

        refreshSubjectRows();
    }

    async function fillSubjectRows(saved) {

        await loadSubjectList();

        field("accSubjectRows").innerHTML = "";

        if (saved.length === 0) {
            addSubjectRow();
        } else {
            saved.forEach(item => addSubjectRow(item.subject_id, item.hourly_rate));
        }
    }

    // Reads the rows. Rows without a chosen subject are skipped.
    function collectSubjects() {

        const list = [];

        for (const row of field("accSubjectRows").querySelectorAll(".acc-subj-row")) {

            const select = row.querySelector(".acc-subj-select");
            const price = row.querySelector(".acc-subj-price").value.trim();

            if (!select.value) {
                continue;
            }

            if (!/^\d+$/.test(price) || Number(price) > 100000) {

                const name = select.options[select.selectedIndex].textContent;

                return {
                    error: "Add meg a(z) „" + name + "” óradíját (0 és 100 000 Ft között)."
                };
            }

            list.push({
                subject_id: Number(select.value),
                hourly_rate: Number(price)
            });
        }

        return { list };
    }

    field("accAddRow").addEventListener("click", () => addSubjectRow());


    // ---------------- open / close ----------------

    async function openModal() {

        modal.classList.add("open");
        modal.setAttribute("aria-hidden", "false");
        document.body.style.overflow = "hidden";

        showMessage("Betöltés...", "");
        clearPasswords();

        try {

            const response = await fetch("/api/account", { credentials: "include" });

            if (!response.ok) {
                throw new Error("HTTP " + response.status);
            }

            const data = await response.json();

            setHeader(data.name);
            field("accName").value = data.name;
            field("accEmail").value = data.email;

            const isTutor = data.role === "TUTOR";
            field("accTutorFields").hidden = !isTutor;
            field("accSubjectsBox").hidden = !isTutor;

            if (isTutor) {
                field("accBio").value = data.bio;
                await fillSubjectRows(data.subjects || []);
            }

            showMessage("", "");

        } catch (error) {

            console.error("Fiókadatok betöltési hiba:", error);
            showMessage("Nem sikerült betölteni az adataidat.", "error");
        }
    }

    function closeModal() {
        modal.classList.remove("open");
        modal.setAttribute("aria-hidden", "true");
        document.body.style.overflow = "";
        clearPasswords();
    }

    trigger.addEventListener("click", openModal);

    trigger.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            openModal();
        }
    });

    field("accClose").addEventListener("click", closeModal);
    field("accCancel").addEventListener("click", closeModal);
    field("accOverlay").addEventListener("click", closeModal);

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && modal.classList.contains("open")) {
            closeModal();
        }
    });


    // ---------------- save ----------------

    form.addEventListener("submit", async (event) => {

        event.preventDefault();

        const newPassword = field("accNewPw").value;

        if (newPassword !== field("accNewPw2").value) {
            showMessage("Az új jelszavak nem egyeznek.", "error");
            return;
        }

        if (newPassword && !/^(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{6,}$/.test(newPassword)) {
            showMessage(
                "Az új jelszó legalább 6 karakter legyen, és tartalmazzon számot és speciális karaktert.",
                "error"
            );
            return;
        }

        let subjectList = null;

        if (!field("accSubjectsBox").hidden) {

            const collected = collectSubjects();

            if (collected.error) {
                showMessage(collected.error, "error");
                return;
            }

            subjectList = collected.list;
        }

        const payload = {
            name: field("accName").value,
            email: field("accEmail").value,
            currentPassword: field("accCurrentPw").value,
            newPassword: newPassword
        };

        if (!field("accTutorFields").hidden) {
            payload.bio = field("accBio").value;
        }

        if (subjectList !== null) {
            payload.subjects = subjectList;
        }

        saveButton.disabled = true;
        showMessage("Mentés...", "");

        try {

            const response = await fetch("/api/account", {
                method: "PUT",
                credentials: "include",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                showMessage(data.error || "Nem sikerült menteni.", "error");
                return;
            }

            // Update the navbar and the modal header with the new name
            tcUpdateNavUser(data.user.name);
            setHeader(data.user.name);

            clearPasswords();
            showMessage("Sikeresen mentve.", "success");

        } catch (error) {

            console.error("Mentési hiba:", error);
            showMessage("Hálózati hiba, próbáld újra.", "error");

        } finally {

            saveButton.disabled = false;
        }
    });
}


// ============================================================
// ESCAPE USER NAME
// ============================================================
//
// Prevents a user's name from being interpreted as HTML.
//
// ============================================================

function escapeHtml(value) {

    return String(value)

        .replaceAll(
            "&",
            "&amp;"
        )

        .replaceAll(
            "<",
            "&lt;"
        )

        .replaceAll(
            ">",
            "&gt;"
        )

        .replaceAll(
            '"',
            "&quot;"
        )

        .replaceAll(
            "'",
            "&#039;"
        );
}


// ============================================================
// CREATE NAVBAR
// ============================================================

createNavbar();


// ============================================================
// FOOTER
// ============================================================

body.insertAdjacentHTML(

    "beforeend",

    `

    <footer class="footer">

        <ul>

            <li>
                Kapcsolat
            </li>

            <li>
                <a href="gyik">
                    GYIK
                </a>
            </li>

            <li>
                Adatvédelem
            </li>

            <li>
                Felhasználási feltételek
            </li>

        </ul>

    </footer>

    `
);