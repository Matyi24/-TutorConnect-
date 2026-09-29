"use strict";


/* =====================================================
   ALAP ELEMEK
===================================================== */

const searchInput =
    document.getElementById("searchInput");

const subjectSelect =
    document.getElementById("subject");

const ratingSelect =
    document.getElementById("rating");

const minPriceInput =
    document.getElementById("minPrice");

const maxPriceInput =
    document.getElementById("maxPrice");

const sortSelect =
    document.getElementById("sortSelect");

const tutorGrid =
    document.getElementById("tutorGrid");

const resultsCount =
    document.getElementById("resultsCount");

const noResults =
    document.getElementById("noResults");

const resetFilters =
    document.getElementById("resetFilters");


/* =====================================================
   PROFIL MODAL ELEMEK
===================================================== */

const profileModal =
    document.getElementById("profileModal");

const profileOverlay =
    document.getElementById("profileOverlay");

const profileClose =
    document.getElementById("profileClose");

const profileAvatar =
    document.getElementById("profileAvatar");

const profileTutorName =
    document.getElementById("profileTutorName");

const profileTutorSubjects =
    document.getElementById("profileTutorSubjects");

const profileAverage =
    document.getElementById("profileAverage");

const profileReviewCount =
    document.getElementById("profileReviewCount");

const profilePrice =
    document.getElementById("profilePrice");

const profileSubjects =
    document.getElementById("profileSubjects");

const profileReviewsNumber =
    document.getElementById("profileReviewsNumber");

const profileEmail =
    document.getElementById("profileEmail");

const profileBio =
    document.getElementById("profileBio");

const profileReviewBadge =
    document.getElementById("profileReviewBadge");

const profileReviewsList =
    document.getElementById("profileReviewsList");


/* =====================================================
   ADATOK
===================================================== */

let tutors = [];


/* =====================================================
   HTML BIZTONSÁG
===================================================== */

function escapeHtml(value) {

    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}


/* =====================================================
   KEZDŐBETŰK
===================================================== */

function getInitials(name) {

    return String(name || "OK")
        .trim()
        .split(/\s+/)
        .map(word => word.charAt(0))
        .join("")
        .substring(0, 2)
        .toUpperCase();
}


/* =====================================================
   CSILLAGOK
===================================================== */

function getStars(rating) {

    const rounded =
        Math.max(
            0,
            Math.min(
                5,
                Math.round(
                    Number(rating) || 0
                )
            )
        );


    return (
        "★".repeat(rounded) +
        "☆".repeat(5 - rounded)
    );
}


/* =====================================================
   TANTÁRGYAK BETÖLTÉSE
===================================================== */

async function loadSubjects() {

    try {

        const response =
            await fetch("/api/subjects");


        if (!response.ok) {

            throw new Error(
                "Nem sikerült betölteni a tantárgyakat."
            );

        }


        const subjects =
            await response.json();


        subjectSelect.innerHTML = `
            <option value="all">
                Összes tantárgy
            </option>
        `;


        if (!Array.isArray(subjects)) {
            return;
        }


        subjects.forEach(subject => {

            const option =
                document.createElement("option");


            option.value =
                subject.name;


            option.textContent =
                subject.name;


            subjectSelect.appendChild(
                option
            );

        });


    } catch (error) {

        console.error(
            "❌ Tantárgyak betöltési hiba:",
            error
        );

    }
}


/* =====================================================
   OKTATÓK BETÖLTÉSE
===================================================== */

async function loadTutors() {

    try {

        resultsCount.textContent =
            "Oktatók betöltése...";


        const response =
            await fetch("/api/tutors");


        if (!response.ok) {

            throw new Error(
                "A szerver nem tudta lekérni az oktatókat."
            );

        }


        const data =
            await response.json();


        if (!Array.isArray(data)) {

            throw new Error(
                "Érvénytelen válasz érkezett a szervertől."
            );

        }


        tutors = data;


        console.log(
            "✅ Adatbázisból betöltött oktatók:",
            tutors
        );


        renderTutors();


    } catch (error) {

        console.error(
            "❌ Oktatók betöltési hiba:",
            error
        );


        resultsCount.textContent =
            "Hiba az oktatók betöltésekor";


        tutorGrid.innerHTML = `

            <div class="api-error">

                <strong>
                    ⚠️ Nem sikerült betölteni az oktatókat.
                </strong>

                <span>
                    Ellenőrizd, hogy fut-e a Node.js szerver,
                    és létezik-e a /api/tutors végpont.
                </span>

            </div>

        `;


        noResults.style.display =
            "none";

    }
}


/* =====================================================
   OKTATÓ KÁRTYA
===================================================== */

function createTutorCard(tutor) {

    const name =
        tutor.full_name ||
        "Ismeretlen oktató";


    const bio =
        tutor.bio ||
        "Az oktató még nem adott meg bemutatkozást.";


    const price =
        Number(
            tutor.hourly_rate || 0
        );


    const rating =
        Number(
            tutor.average_rating || 0
        );


    const reviewCount =
        Number(
            tutor.review_count || 0
        );


    const subjects =
        tutor.subjects ||
        "Nincs megadott tantárgy";


    const initials =
        getInitials(name);


    const avatarColors = [

        "avatar-blue",
        "avatar-purple",
        "avatar-green",
        "avatar-orange",
        "avatar-red",
        "avatar-pink",
        "avatar-cyan",
        "avatar-indigo"

    ];


    const tutorId =
        Number(tutor.id);


    const colorIndex =
        Math.abs(tutorId) %
        avatarColors.length;


    const colorClass =
        avatarColors[colorIndex];


    const card =
        document.createElement("article");


    card.className =
        "tutor-card";


    card.dataset.id =
        tutor.id;


    card.innerHTML = `

        <!-- KÁRTYA FEJLÉC -->

        <div class="card-top">

            <div class="avatar ${colorClass}">
                ${escapeHtml(initials)}
            </div>

        </div>


        <!-- NÉV -->

        <h2>
            ${escapeHtml(name)}
        </h2>


        <!-- TANTÁRGY -->

        <span class="subject">
            ${escapeHtml(subjects)}
        </span>


        <!-- ÉRTÉKELÉS -->

        <div class="rating">

            <span>
                ★
            </span>

            <strong>

                ${
                    rating > 0
                        ? rating.toFixed(1)
                        : "Nincs"
                }

            </strong>


            <small>

                ${reviewCount}
                ${
                    reviewCount === 1
                        ? " értékelés"
                        : " értékelés"
                }

            </small>

        </div>


        <!-- ÁR -->

        <div class="price">

            <strong>
                ${price.toLocaleString("hu-HU")} Ft
            </strong>

            <span>
                / óra
            </span>

        </div>


        <!-- BEMUTATKOZÁS -->

        <p class="description">
            ${escapeHtml(bio)}
        </p>


        <!-- PROFIL GOMB -->

        <button
            class="profile-btn"
            type="button"
            data-tutor-id="${escapeHtml(tutor.id)}"
        >

            <span>
                Profil megtekintése
            </span>

            <span>
                →
            </span>

        </button>

    `;


    return card;
}


/* =====================================================
   SZŰRÉS
===================================================== */

function getFilteredTutors() {

    const search =
        searchInput.value
            .toLowerCase()
            .trim();


    const selectedSubject =
        subjectSelect.value;


    const minPrice =
        Number(
            minPriceInput.value
        ) || 0;


    const maxPrice =
        maxPriceInput.value.trim() === ""
            ? Infinity
            : Number(
                maxPriceInput.value
            );


    const minimumRating =
        Number(
            ratingSelect.value
        ) || 0;


    return tutors.filter(tutor => {


        const name =
            String(
                tutor.full_name || ""
            ).toLowerCase();


        const subjects =
            String(
                tutor.subjects || ""
            ).toLowerCase();


        const rating =
            Number(
                tutor.average_rating || 0
            );


        const price =
            Number(
                tutor.hourly_rate || 0
            );


        /* KERESÉS */

        const matchesSearch =
            search === "" ||
            name.includes(search) ||
            subjects.includes(search);


        /* TANTÁRGY */

        let matchesSubject =
            true;


        if (
            selectedSubject !== "all"
        ) {

            const tutorSubjectList =
                String(
                    tutor.subjects || ""
                )
                .split(",")
                .map(item =>
                    item
                        .trim()
                        .toLowerCase()
                );


            matchesSubject =
                tutorSubjectList.includes(
                    selectedSubject
                        .trim()
                        .toLowerCase()
                );

        }


        /* ÉRTÉKELÉS */

        const matchesRating =
            rating >= minimumRating;


        /* ÁR */

        const matchesPrice =
            price >= minPrice &&
            price <= maxPrice;


        return (
            matchesSearch &&
            matchesSubject &&
            matchesRating &&
            matchesPrice
        );

    });
}


/* =====================================================
   RENDEZÉS
===================================================== */

function sortTutors(list) {

    const sorted =
        [...list];


    switch (sortSelect.value) {


        case "rating":

            sorted.sort(
                (a, b) =>
                    Number(
                        b.average_rating || 0
                    ) -
                    Number(
                        a.average_rating || 0
                    )
            );

            break;


        case "priceLow":

            sorted.sort(
                (a, b) =>
                    Number(
                        a.hourly_rate || 0
                    ) -
                    Number(
                        b.hourly_rate || 0
                    )
            );

            break;


        case "priceHigh":

            sorted.sort(
                (a, b) =>
                    Number(
                        b.hourly_rate || 0
                    ) -
                    Number(
                        a.hourly_rate || 0
                    )
            );

            break;


        default:

            break;

    }


    return sorted;
}


/* =====================================================
   MEGJELENÍTÉS
===================================================== */

function renderTutors() {

    const filtered =
        getFilteredTutors();


    const sorted =
        sortTutors(filtered);


    tutorGrid.innerHTML =
        "";


    sorted.forEach(tutor => {

        tutorGrid.appendChild(
            createTutorCard(tutor)
        );

    });


    resultsCount.textContent =
        `${sorted.length} oktató található`;


    noResults.style.display =
        sorted.length === 0
            ? "block"
            : "none";
}


/* =====================================================
   PROFIL MEGNYITÁSA
===================================================== */

async function openProfile(tutorId) {

    const tutor =
        tutors.find(
            item =>
                Number(item.id) ===
                Number(tutorId)
        );


    if (!tutor) {

        console.error(
            "❌ Az oktató nem található:",
            tutorId
        );

        return;
    }


    /* =================================================
       ALAPADATOK
    ================================================== */

    const name =
        tutor.full_name ||
        "Ismeretlen oktató";


    const subjects =
        tutor.subjects ||
        "Nincs megadott tantárgy";


    const bio =
        tutor.bio ||
        "Az oktató még nem adott meg bemutatkozást.";


    const email =
        tutor.email ||
        "Nincs megadva";


    const price =
        Number(
            tutor.hourly_rate || 0
        );


    const rating =
        Number(
            tutor.average_rating || 0
        );


    const reviewCount =
        Number(
            tutor.review_count || 0
        );


    /* =================================================
       PROFIL ADATOK KIÍRÁSA
    ================================================== */

    profileAvatar.textContent =
        getInitials(name);


    profileTutorName.textContent =
        name;


    profileTutorSubjects.textContent =
        subjects;


    profileAverage.textContent =
        rating > 0
            ? rating.toFixed(1)
            : "0.0";


    profileReviewCount.textContent =
        `${reviewCount} értékelés`;


    profilePrice.textContent =
        `${price.toLocaleString("hu-HU")} Ft / óra`;


    profileSubjects.textContent =
        subjects;


    profileReviewsNumber.textContent =
        reviewCount;


    profileReviewBadge.textContent =
        `${reviewCount} értékelés`;


    profileEmail.textContent =
        email;


    profileBio.textContent =
        bio;


    /* =================================================
       MODAL MEGNYITÁSA
    ================================================== */

    profileModal.classList.add(
        "active"
    );


    profileModal.setAttribute(
        "aria-hidden",
        "false"
    );


    document.body.style.overflow =
        "hidden";


    /* =================================================
       ÉRTÉKELÉSEK BETÖLTÉSE
    ================================================== */

    profileReviewsList.innerHTML = `

        <div class="profile-reviews-loading">

            Értékelések betöltése...

        </div>

    `;


    try {

        const response =
            await fetch(
                `/api/tutors/${encodeURIComponent(tutorId)}/reviews`
            );


        if (!response.ok) {

            throw new Error(
                "Nem sikerült lekérni az értékeléseket."
            );

        }


        const reviews =
            await response.json();


        renderProfileReviews(
            reviews
        );


    } catch (error) {

        console.error(
            "❌ Értékelések betöltési hiba:",
            error
        );


        profileReviewsList.innerHTML = `

            <div class="profile-reviews-error">

                ⚠️ Nem sikerült betölteni
                az értékeléseket.

            </div>

        `;

    }
}


/* =====================================================
   PROFIL ÉRTÉKELÉSEK
===================================================== */

function renderProfileReviews(reviews) {

    if (!Array.isArray(reviews)) {

        profileReviewsList.innerHTML = `

            <div class="profile-reviews-error">

                Hibás válasz érkezett
                a szervertől.

            </div>

        `;

        return;
    }


    if (reviews.length === 0) {

        profileReviewsList.innerHTML = `

            <div class="profile-reviews-empty">

                <div class="empty-review-icon">
                    ⭐
                </div>

                <strong>
                    Még nincs értékelés
                </strong>

                <span>
                    Ehhez az oktatóhoz még nem érkezett
                    értékelés.
                </span>

            </div>

        `;

        return;
    }


    profileReviewsList.innerHTML =
        reviews
            .map(review => {


                const reviewerName =
                    review.reviewer_name ||
                    "Névtelen értékelő";


                const rating =
                    Number(
                        review.rating || 0
                    );


                const text =
                    review.comment ||
                    "Az értékelő nem írt szöveges értékelést.";


                let dateText =
                    "";


                if (
                    review.created_at
                ) {

                    const date =
                        new Date(
                            review.created_at
                        );


                    if (
                        !Number.isNaN(
                            date.getTime()
                        )
                    ) {

                        dateText =
                            date.toLocaleDateString(
                                "hu-HU"
                            );

                    }

                }


                return `

                    <article
                        class="profile-review-item"
                    >


                        <div
                            class="profile-review-top"
                        >


                            <div
                                class="profile-reviewer"
                            >


                                <div
                                    class="profile-reviewer-avatar"
                                >

                                    ${escapeHtml(
                                        getInitials(
                                            reviewerName
                                        )
                                    )}

                                </div>


                                <div>

                                    <strong
                                        class="profile-reviewer-name"
                                    >

                                        ${escapeHtml(
                                            reviewerName
                                        )}

                                    </strong>


                                    ${
                                        dateText
                                            ? `
                                                <span
                                                    class="profile-review-date"
                                                >
                                                    ${escapeHtml(
                                                        dateText
                                                    )}
                                                </span>
                                            `
                                            : ""
                                    }

                                </div>


                            </div>


                            <div
                                class="profile-review-stars"
                            >

                                ${getStars(
                                    rating
                                )}

                            </div>


                        </div>


                        <p
                            class="profile-review-text"
                        >

                            ${escapeHtml(
                                text
                            )}

                        </p>


                    </article>

                `;

            })
            .join("");
}


/* =====================================================
   PROFIL BEZÁRÁSA
===================================================== */

function closeProfile() {

    profileModal.classList.remove(
        "active"
    );


    profileModal.setAttribute(
        "aria-hidden",
        "true"
    );


    document.body.style.overflow =
        "";

}


/* =====================================================
   MODAL ESEMÉNYEK
===================================================== */

profileClose.addEventListener(
    "click",
    closeProfile
);


profileOverlay.addEventListener(
    "click",
    closeProfile
);


document.addEventListener(
    "keydown",
    event => {

        if (
            event.key === "Escape" &&
            profileModal.classList.contains(
                "active"
            )
        ) {

            closeProfile();

        }

    }
);


/* =====================================================
   PROFIL GOMBOK
===================================================== */

tutorGrid.addEventListener(
    "click",
    event => {

        const button =
            event.target.closest(
                ".profile-btn"
            );


        if (!button) {
            return;
        }


        const tutorId =
            button.dataset.tutorId;


        openProfile(
            tutorId
        );

    }
);


/* =====================================================
   SZŰRŐ ESEMÉNYEK
===================================================== */

searchInput.addEventListener(
    "input",
    renderTutors
);


subjectSelect.addEventListener(
    "change",
    renderTutors
);


ratingSelect.addEventListener(
    "change",
    renderTutors
);


minPriceInput.addEventListener(
    "input",
    renderTutors
);


maxPriceInput.addEventListener(
    "input",
    renderTutors
);


sortSelect.addEventListener(
    "change",
    renderTutors
);


/* =====================================================
   SZŰRŐK TÖRLÉSE
===================================================== */

resetFilters.addEventListener(
    "click",
    () => {

        searchInput.value =
            "";


        subjectSelect.value =
            "all";


        ratingSelect.value =
            "0";


        minPriceInput.value =
            "";


        maxPriceInput.value =
            "";


        sortSelect.value =
            "default";


        renderTutors();

    }
);


/* =====================================================
   INDÍTÁS
===================================================== */

async function init() {

    await loadSubjects();

    await loadTutors();

}


init();
