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
                <li>Oktatok</li>
            </a>

            <a href="/foglalas">
                <li>Foglalas</li>
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

                <span class="user-name">
                    ${escapeHtml(user.name)}
                </span>


                <button
                    class="logout"
                    id="logoutButton"
                >
                    Kilépés
                </button>

            </div>

        `;
    }


    // ========================================================
    // INSERT NAVBAR
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
                GYIK
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