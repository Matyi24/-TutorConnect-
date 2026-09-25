"use strict";


/* =====================================================
   ELEMEK
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

const availabilitySelect =
    document.getElementById("availability");

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
   REVIEW MODAL ELEMEK
===================================================== */

const reviewsModal =
    document.getElementById("reviewsModal");

const reviewsOverlay =
    document.getElementById("reviewsOverlay");

const reviewsClose =
    document.getElementById("reviewsClose");

const reviewsTutorName =
    document.getElementById("reviewsTutorName");

const reviewsAvatar =
    document.getElementById("reviewsAvatar");

const reviewsAverage =
    document.getElementById("reviewsAverage");

const reviewsTotal =
    document.getElementById("reviewsTotal");

const reviewsList =
    document.getElementById("reviewsList");


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
        Math.round(Number(rating) || 0);

    return "★".repeat(rounded) +
        "☆".repeat(5 - rounded);
}


/* =====================================================
   TANTÁRGYAK
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

        subjects.forEach(subject => {

            const option =
                document.createElement("option");

            option.value =
                subject.name;

            option.textContent =
                subject.name;

            subjectSelect.appendChild(option);

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
        Number(tutor.hourly_rate || 0);

    const rating =
        Number(tutor.average_rating || 0);

    const reviewCount =
        Number(tutor.review_count || 0);

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


    const colorClass =
        avatarColors[
            Math.abs(tutorId) %
            avatarColors.length
        ];


    const isAvailable =
        tutor.is_available === true ||
        tutor.is_available === 1 ||
        tutor.is_available === "1";


    const availabilityClass =
        isAvailable
            ? "online"
            : "offline";


    const availabilityText =
        isAvailable
            ? "Elérhető"
            : "Nem elérhető";


    const card =
        document.createElement("article");


    card.className =
        "tutor-card";


    card.dataset.id =
        tutor.id;

    card.dataset.name =
        name;

    card.dataset.subject =
        subjects;

    card.dataset.rating =
        rating;

    card.dataset.price =
        price;

    card.dataset.available =
        isAvailable;


    card.innerHTML = `

        <div class="card-top">

            <div class="avatar ${colorClass}">
                ${escapeHtml(initials)}
            </div>

            <span class="${availabilityClass}">

                <i></i>

                ${availabilityText}

            </span>

        </div>


        <h2>
            ${escapeHtml(name)}
        </h2>


        <span class="subject">
            ${escapeHtml(subjects)}
        </span>


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
                ${
                    reviewCount === 1
                        ? "(1 értékelés)"
                        : `(${reviewCount} értékelés)`
                }
            </small>

        </div>


        <div class="price">

            <strong>
                ${price.toLocaleString("hu-HU")} Ft
            </strong>

            <span>
                / óra
            </span>

        </div>


        <p class="description">
            ${escapeHtml(bio)}
        </p>


        <button
            class="profile-btn"
            type="button"
            data-tutor-id="${tutor.id}"
        >

            <span>
                Értékelések megtekintése
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
        Number(minPriceInput.value) || 0;

    const maxPrice =
        maxPriceInput.value.trim() === ""
            ? Infinity
            : Number(maxPriceInput.value);

    const minimumRating =
        Number(ratingSelect.value) || 0;

    const selectedAvailability =
        availabilitySelect.value;


    return tutors.filter(tutor => {

        const name =
            String(tutor.full_name || "")
                .toLowerCase();

        const subjects =
            String(tutor.subjects || "")
                .toLowerCase();

        const rating =
            Number(
                tutor.average_rating || 0
            );

        const price =
            Number(
                tutor.hourly_rate || 0
            );

        const isAvailable =
            tutor.is_available === true ||
            tutor.is_available === 1 ||
            tutor.is_available === "1";


        const matchesSearch =
            search === "" ||
            name.includes(search) ||
            subjects.includes(search);


        let matchesSubject = true;


        if (selectedSubject !== "all") {

            const tutorSubjectList =
                String(tutor.subjects || "")
                    .split(",")
                    .map(item =>
                        item.trim()
                            .toLowerCase()
                    );

            matchesSubject =
                tutorSubjectList.includes(
                    selectedSubject
                        .trim()
                        .toLowerCase()
                );
        }


        const matchesRating =
            rating >= minimumRating;


        const matchesPrice =
            price >= minPrice &&
            price <= maxPrice;


        let matchesAvailability = true;


        if (
            selectedAvailability ===
            "available"
        ) {

            matchesAvailability =
                isAvailable;

        } else if (
            selectedAvailability ===
            "unavailable"
        ) {

            matchesAvailability =
                !isAvailable;
        }


        return (
            matchesSearch &&
            matchesSubject &&
            matchesRating &&
            matchesPrice &&
            matchesAvailability
        );

    });
}


/* =====================================================
   RENDEZÉS
===================================================== */

function sortTutors(list) {

    const sorted =
        [...list];


    if (
        sortSelect.value ===
        "rating"
    ) {

        sorted.sort(
            (a, b) =>
                Number(
                    b.average_rating || 0
                ) -
                Number(
                    a.average_rating || 0
                )
        );
    }


    if (
        sortSelect.value ===
        "priceLow"
    ) {

        sorted.sort(
            (a, b) =>
                Number(
                    a.hourly_rate || 0
                ) -
                Number(
                    b.hourly_rate || 0
                )
        );
    }


    if (
        sortSelect.value ===
        "priceHigh"
    ) {

        sorted.sort(
            (a, b) =>
                Number(
                    b.hourly_rate || 0
                ) -
                Number(
                    a.hourly_rate || 0
                )
        );
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

        const card =
            createTutorCard(tutor);

        tutorGrid.appendChild(card);

    });


    resultsCount.textContent =
        `${sorted.length} oktató található`;


    noResults.style.display =
        sorted.length === 0
            ? "block"
            : "none";
}


/* =====================================================
   REVIEW MODAL MEGNYITÁSA
===================================================== */

async function openReviews(tutorId) {

    const tutor =
        tutors.find(
            item =>
                Number(item.id) ===
                Number(tutorId)
        );


    if (!tutor) {
        return;
    }


    const name =
        tutor.full_name ||
        "Ismeretlen oktató";


    const rating =
        Number(
            tutor.average_rating || 0
        );


    const reviewCount =
        Number(
            tutor.review_count || 0
        );


    reviewsTutorName.textContent =
        name;


    reviewsAvatar.textContent =
        getInitials(name);


    reviewsAverage.textContent =
        rating > 0
            ? rating.toFixed(1)
            : "0.0";


    reviewsTotal.textContent =
        reviewCount === 1
            ? "1 értékelés"
            : `${reviewCount} értékelés`;


    reviewsList.innerHTML = `
        <div class="reviews-loading">
            Értékelések betöltése...
        </div>
    `;


    reviewsModal.classList.add("active");

    reviewsModal.setAttribute(
        "aria-hidden",
        "false"
    );


    document.body.style.overflow =
        "hidden";


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


        renderReviews(reviews);


    } catch (error) {

        console.error(
            "❌ Review betöltési hiba:",
            error
        );


        reviewsList.innerHTML = `

            <div class="reviews-error">

                ⚠️ Nem sikerült betölteni
                az értékeléseket.

            </div>

        `;
    }
}


/* =====================================================
   REVIEW-K MEGJELENÍTÉSE
===================================================== */

function renderReviews(reviews) {

    if (!Array.isArray(reviews)) {

        reviewsList.innerHTML = `
            <div class="reviews-error">
                Hibás válasz érkezett a szervertől.
            </div>
        `;

        return;
    }


    if (reviews.length === 0) {

        reviewsList.innerHTML = `
            <div class="reviews-empty">
                ⭐ Ennek az oktatónak még nincs értékelése.
            </div>
        `;

        return;
    }


    reviewsList.innerHTML =
        reviews.map(review => {

            const reviewerName =
                review.reviewer_name ||
                review.student_name ||
                "Névtelen értékelő";


            const rating =
                Number(
                    review.rating || 0
                );


            const text =
                review.comment ||
                review.review_text ||
                review.text ||
                "Az értékelő nem írt szöveges értékelést.";


            let dateText = "";


            if (review.created_at) {

                const date =
                    new Date(
                        review.created_at
                    );


                if (!Number.isNaN(
                    date.getTime()
                )) {

                    dateText =
                        date.toLocaleDateString(
                            "hu-HU"
                        );
                }
            }


            return `

                <article class="review-item">

                    <div class="review-item-top">

                        <div class="reviewer">

                            <div class="reviewer-avatar">
                                ${escapeHtml(
                                    getInitials(
                                        reviewerName
                                    )
                                )}
                            </div>

                            <div>

                                <div class="reviewer-name">
                                    ${escapeHtml(
                                        reviewerName
                                    )}
                                </div>

                                ${
                                    dateText
                                        ? `
                                            <span class="review-date">
                                                ${escapeHtml(
                                                    dateText
                                                )}
                                            </span>
                                        `
                                        : ""
                                }

                            </div>

                        </div>


                        <div class="review-stars">
                            ${getStars(rating)}
                        </div>

                    </div>


                    <p class="review-text">
                        ${escapeHtml(text)}
                    </p>

                </article>

            `;

        }).join("");
}


/* =====================================================
   MODAL BEZÁRÁSA
===================================================== */

function closeReviews() {

    reviewsModal.classList.remove(
        "active"
    );

    reviewsModal.setAttribute(
        "aria-hidden",
        "true"
    );


    document.body.style.overflow =
        "";
}


reviewsClose.addEventListener(
    "click",
    closeReviews
);


reviewsOverlay.addEventListener(
    "click",
    closeReviews
);


document.addEventListener(
    "keydown",
    event => {

        if (
            event.key === "Escape" &&
            reviewsModal.classList.contains(
                "active"
            )
        ) {

            closeReviews();
        }
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

availabilitySelect.addEventListener(
    "change",
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

        availabilitySelect.value =
            "all";

        sortSelect.value =
            "default";


        renderTutors();
    }
);


/* =====================================================
   PROFIL / REVIEW GOMB
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


        openReviews(tutorId);
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
