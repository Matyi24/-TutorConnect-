const body = document.body;


// ============================================================
// NAVBAR
// ============================================================

async function createNavbar() {

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

                <span
                    class="user-name"
                    id="accountButton"
                    role="button"
                    tabindex="0"
                    title="Fiókom szerkesztése"
                >
                    ${escapeHtml(user.name)}
                </span>

            </div>

        `;
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

        .user-name { cursor: pointer; transition: color .2s; }
        .user-name:hover { color: #4de0a3; }
        .user-name:focus-visible { outline: 2px solid #5b4de0; outline-offset: 3px; border-radius: 6px; }

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
            position: relative; width: 100%; max-width: 880px; max-height: 92vh;
            overflow-y: auto; background: #fdfcff; border-radius: 26px;
            padding: 32px; box-shadow: 0 30px 80px rgba(60, 40, 160, .28);
            animation: accIn .25s ease;
        }
        @keyframes accIn {
            from { opacity: 0; transform: translateY(14px) scale(.98); }
            to   { opacity: 1; transform: none; }
        }

        .acc-close {
            position: absolute; top: 16px; right: 18px; padding: 0px 0px;
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
        .acc-actions .logout { margin-right: auto; background: #fff0f4; color: #d6336c; }
        .acc-actions .logout:hover { background: #ffe0e8; }
        .acc-actions button {
            padding: 12px 22px; border-radius: 12px; border: 0;
            font: inherit; font-size: 14px; font-weight: 700; cursor: pointer;
        }
        .acc-cancel { background: #f1eeff; color: #5b4de0; }
        .acc-save { background: #5b4de0; color: #fff; }
        .acc-save:hover { background: #4a3dcb; }
        .acc-save:disabled { opacity: .6; cursor: default; }

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
                                <input type="text" id="accName" maxlength="100" required>
                            </div>

                            <div class="acc-field">
                                <label for="accEmail">E-mail</label>
                                <input type="email" id="accEmail" maxlength="255" required>
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

                    <div class="acc-message" id="accMessage" role="status"></div>

                    <div class="acc-actions">
                        <button class="logout" id="logoutButton">Kilépés</button>
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


    function initials(name) {
        return name.trim().split(/\s+/).slice(0, 2)
            .map(part => part[0] ? part[0].toUpperCase() : "").join("");
    }

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
        field("accAvatar").textContent = initials(name);
    }


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

            if (isTutor) {
                field("accBio").value = data.bio;
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

        const payload = {
            name: field("accName").value,
            email: field("accEmail").value,
            currentPassword: field("accCurrentPw").value,
            newPassword: newPassword
        };

        if (!field("accTutorFields").hidden) {
            payload.bio = field("accBio").value;
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
            trigger.textContent = data.user.name;
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