"use strict";

/* =====================================================
   STATE
===================================================== */

let allSubjects = [];
let tutors = [];

let selectedCategory = "Minden tárgy";
let searchQuery = "";

// Igaz, ha a tantárgyak már megérkeztek a szervertől
let subjectsLoaded = false;


/* =====================================================
   INIT
===================================================== */

document.addEventListener("DOMContentLoaded", () => {
    const subjectContainer = document.querySelector(".grid-container");

    if (!subjectContainer) {
        return;
    }

    setupCategoryFilter();
    loadSubjects();
});


/* =====================================================
   LOAD DATA
===================================================== */

async function loadSubjects() {
    try {
        const [subjectsResponse, tutorsResponse] = await Promise.all([
            fetch("/api/subjects"),
            fetch("/api/tutors")
        ]);

        if (!subjectsResponse.ok) {
            throw new Error("Nem sikerült lekérni a tantárgyakat.");
        }

        allSubjects = await subjectsResponse.json();

        // Tutors are optional.
        if (tutorsResponse.ok) {
            const tutorData = await tutorsResponse.json();

            if (Array.isArray(tutorData)) {
                tutors = tutorData;
            }
        }

        subjectsLoaded = true;

        applyFilters();

        setupSearch();
        setupClearFilters();

    } catch (error) {
        console.error("Hiba a tantárgyak betöltésekor:", error);

        const container = document.querySelector(".grid-container");

        if (container) {
            container.innerHTML = `
                <div class="no-results">
                    <div class="no-results-icon">⚠️</div>

                    <h3>Nem sikerült betölteni a tantárgyakat</h3>

                    <p>Próbáld újra később.</p>
                </div>
            `;
        }

        updateSubjectCount(0);
    }
}


/* =====================================================
   SEARCH
===================================================== */

function setupSearch() {
    const searchInput = document.getElementById("subjectSearch");

    if (!searchInput) {
        return;
    }

    searchInput.addEventListener("input", () => {
        searchQuery = searchInput.value.trim().toLowerCase();

        applyFilters();
    });
}


/* =====================================================
   CATEGORY FILTER
===================================================== */

// A legördülő menü megjelenését a dropdown.js kezeli, ez a függvény
// csak a "categorychange" eseményre reagál, és szűr.
// Az oldal betöltésekor azonnal regisztrálódik, hogy egy korai
// kattintás se vesszen el; a lista kirajzolása addig vár, amíg
// az adatok meg nem érkeznek.
function setupCategoryFilter() {
    document.addEventListener("categorychange", event => {
        selectedCategory = event.detail.category;

        if (subjectsLoaded) {
            applyFilters();
        }
    });
}


/* =====================================================
   CLEAR FILTERS
===================================================== */

function setupClearFilters() {
    const clearButton = document.getElementById("clearFilters");
    const searchInput = document.getElementById("subjectSearch");
    const selectedSubject = document.getElementById("selectedSubject");

    if (!clearButton) {
        return;
    }

    clearButton.addEventListener("click", () => {
        selectedCategory = "Minden tárgy";
        searchQuery = "";

        if (searchInput) {
            searchInput.value = "";
        }

        if (selectedSubject) {
            selectedSubject.textContent = "Minden tárgy";
        }

        document.querySelectorAll(".dropdown-item").forEach(item => {
            const isAll =
                item.textContent.trim() === "Minden tárgy";

            item.classList.toggle("active", isAll);
            item.setAttribute(
                "aria-selected",
                String(isAll)
            );
        });

        applyFilters();
    });
}


/* =====================================================
   FILTERING
===================================================== */

function applyFilters() {
    let filteredSubjects = [...allSubjects];

    if (selectedCategory !== "Minden tárgy") {
        const category = selectedCategory.toLowerCase();

        filteredSubjects = filteredSubjects.filter(subject =>
            subject.category?.toLowerCase() === category
        );
    }

    if (searchQuery) {
        filteredSubjects = filteredSubjects.filter(subject => {
            const name = subject.name?.toLowerCase() || "";

            return name.includes(searchQuery);
        });
    }

    displaySubjects(filteredSubjects);
}


/* =====================================================
   TUTOR COUNT
===================================================== */

function getTutorCountForSubject(subjectName) {
    if (!subjectName) {
        return 0;
    }

    const target = subjectName.trim().toLowerCase();

    return tutors.filter(tutor => {
        const subjects = String(tutor.subjects || "")
            .split(",")
            .map(subject => subject.trim().toLowerCase())
            .filter(Boolean);

        return subjects.includes(target);
    }).length;
}


/* =====================================================
   DISPLAY SUBJECTS
===================================================== */

function displaySubjects(subjects) {
    const container = document.querySelector(".grid-container");

    if (!container) {
        return;
    }

    container.innerHTML = "";

    if (subjects.length === 0) {
        container.innerHTML = `
            <div class="no-results">
                <div class="no-results-icon">🔎</div>

                <h3>Nincs találat</h3>

                <p>
                    Nem található a keresésnek
                    és a szűrőknek megfelelő tantárgy.
                </p>
            </div>
        `;

        updateSubjectCount(0);

        return;
    }

    updateSubjectCount(subjects.length);

    subjects.forEach((subject, index) => {
        const link = document.createElement("a");

        link.href =
            `/oktatok?subject=${encodeURIComponent(subject.name)}`;

        const card = document.createElement("div");

        card.classList.add("subcard");
        card.style.animationDelay = `${index * 0.06}s`;

        /* Icon */
        const icon = document.createElement("div");

        icon.classList.add("subject-icon");
        icon.textContent = getSubjectIcon(subject.name);

        /* Info */
        const info = document.createElement("div");

        info.classList.add("subject-info");

        const title = document.createElement("h2");

        title.textContent = subject.name;

        const description = document.createElement("p");

        description.textContent =
            subject.category || "Tantárgy";

        /* Tutor count */
        const tutorCount =
            getTutorCountForSubject(subject.name);

        const tutorBadge =
            document.createElement("span");

        tutorBadge.classList.add("tutor-count");

        if (tutorCount === 0) {
            tutorBadge.classList.add("no-tutors");
            tutorBadge.textContent = "Jelenleg nincs oktató";
        } else if (tutorCount === 1) {
            tutorBadge.textContent = "1 elérhető oktató";
        } else {
            tutorBadge.textContent =
                `${tutorCount} elérhető oktató`;
        }

        info.append(title, description, tutorBadge);

        /* Arrow */
        const arrow = document.createElement("span");

        arrow.classList.add("subject-arrow");
        arrow.textContent = "→";

        /* Card */
        card.append(icon, info, arrow);
        link.appendChild(card);

        container.appendChild(link);
    });
}


/* =====================================================
   SUBJECT COUNT
===================================================== */

function updateSubjectCount(count) {
    const subjectCount =
        document.getElementById("subjectCount");

    if (!subjectCount) {
        return;
    }

    if (count === 0) {
        subjectCount.textContent = "Nincs találat";
    } else if (count === 1) {
        subjectCount.textContent = "1 tantárgy";
    } else {
        subjectCount.textContent = `${count} tantárgy`;
    }
}


/* =====================================================
   SUBJECT ICONS
===================================================== */

function getSubjectIcon(subjectName) {
    const name = String(subjectName || "").toLowerCase();

    const icons = [
        [["matematika"], "∑"],
        [["fizika"], "⚛"],
        [["kémia"], "⚗"],
        [["informatika", "programoz"], "</>"],
        [["statisztika"], "▥"],
        [["történelem"], "◈"],
        [["magyar"], "A"],
        [["filozófia"], "Φ"],
        [["etika"], "◆"],
        [["pszichológia"], "Ψ"],
        [["társadalomismeret"], "◉"],
        [["jog"], "§"],
        [["angol"], "EN"],
        [["német"], "DE"],
        [["francia"], "FR"],
        [["spanyol"], "ES"],
        [["olasz"], "IT"],
        [["orosz"], "RU"],
        [["latin"], "LA"]
    ];

    for (const [keywords, icon] of icons) {
        if (keywords.some(keyword => name.includes(keyword))) {
            return icon;
        }
    }

    return "📚";
}
