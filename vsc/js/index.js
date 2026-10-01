document.addEventListener("DOMContentLoaded", () => {
    const subjectContainer = document.querySelector(".grid-container");

    if (!subjectContainer) {
        return;
    }

    loadSubjects();
});

let allSubjects = [];
let selectedCategory = "Minden tárgy";
let searchQuery = "";


// ==========================================
// SUBJECTEK BETÖLTÉSE
// ==========================================

async function loadSubjects() {
    try {
        const response = await fetch("/api/subjects");

        if (!response.ok) {
            throw new Error("Nem sikerült lekérni a subjecteket.");
        }

        allSubjects = await response.json();

        console.log("Subjectek:", allSubjects);

        // Kezdeti megjelenítés
        applyFilters();

        // Szűrők beállítása
        setupSearch();
        setupCategoryFilter();
        setupClearFilters();

    } catch (error) {
        console.error("Hiba a subjectek betöltésekor:", error);

        const container = document.querySelector(".grid-container");

        if (container) {
            container.innerHTML = `
                <div class="no-results">
                    <p>Hiba történt a tantárgyak betöltésekor.</p>
                </div>
            `;
        }
    }
}


// ==========================================
// KERESÉS
// ==========================================

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


// ==========================================
// KATEGÓRIA SZŰRÉS
// ==========================================

function setupCategoryFilter() {
    const dropdownMenu = document.getElementById("dropdownMenu");
    const selectedSubject = document.getElementById("selectedSubject");

    if (!dropdownMenu) {
        return;
    }

    const dropdownItems = dropdownMenu.querySelectorAll(".dropdown-item");

    dropdownItems.forEach(item => {
        item.addEventListener("click", () => {
            selectedCategory = item.textContent.trim();

            console.log("Kiválasztott kategória:", selectedCategory);

            // Dropdown felirat frissítése
            if (selectedSubject) {
                selectedSubject.textContent = selectedCategory;
            }

            applyFilters();
        });
    });
}


// ==========================================
// SZŰRŐK TÖRLÉSE
// ==========================================

function setupClearFilters() {
    const clearButton = document.getElementById("clearFilters");
    const searchInput = document.getElementById("subjectSearch");
    const selectedSubject = document.getElementById("selectedSubject");

    if (!clearButton) {
        return;
    }

    clearButton.addEventListener("click", () => {
        // Kategória visszaállítása
        selectedCategory = "Minden tárgy";

        // Keresés törlése
        searchQuery = "";

        if (searchInput) {
            searchInput.value = "";
        }

        // Dropdown szöveg visszaállítása
        if (selectedSubject) {
            selectedSubject.textContent = "Minden tárgy";
        }

        // Minden subject megjelenítése
        applyFilters();
    });
}


// ==========================================
// SZŰRÉSEK ALKALMAZÁSA
// ==========================================

function applyFilters() {
    let filteredSubjects = [...allSubjects];

    // ======================================
    // KATEGÓRIA SZŰRÉS
    // ======================================

    if (selectedCategory !== "Minden tárgy") {
        filteredSubjects = filteredSubjects.filter(subject => {
            return (
                subject.category &&
                subject.category.toLowerCase() ===
                selectedCategory.toLowerCase()
            );
        });
    }

    // ======================================
    // KERESÉS
    // ======================================

    if (searchQuery !== "") {
        filteredSubjects = filteredSubjects.filter(subject => {
            const subjectName = subject.name
                ? subject.name.toLowerCase()
                : "";

            return subjectName.includes(searchQuery);
        });
    }

    // ======================================
    // MEGJELENÍTÉS
    // ======================================

    displaySubjects(filteredSubjects);
}


// ==========================================
// SUBJECTEK MEGJELENÍTÉSE
// ==========================================

function displaySubjects(subjects) {
    const container = document.querySelector(".grid-container");

    if (!container) {
        return;
    }

    // Régi subjectek törlése
    container.innerHTML = "";

    // ======================================
    // NINCS TALÁLAT
    // ======================================

    if (subjects.length === 0) {
        container.innerHTML = `
            <div class="no-results">
                <h3>Nincs találat</h3>
                <p>
                    Nem található a keresésnek és a szűrőknek megfelelő tantárgy.
                </p>
            </div>
        `;

        updateSubjectCount(0);

        return;
    }

    // Tantárgyak számának frissítése
    updateSubjectCount(subjects.length);

    // ======================================
    // KÁRTYÁK
    // ======================================

    subjects.forEach((subject, index) => {

        // ==================================
        // LINK
        // ==================================

        const link = document.createElement("a");

        link.href = `matek.html?subject_id=${subject.id}`;


        // ==================================
        // KÁRTYA
        // ==================================

        const card = document.createElement("div");

        card.classList.add("subcard");

        // Animáció késleltetése
        card.style.animationDelay = `${index * 0.1}s`;


        // ==================================
        // IKON
        // ==================================

        const icon = document.createElement("div");

        icon.classList.add("subject-icon");

        icon.textContent = getSubjectIcon(subject.name);


        // ==================================
        // INFORMÁCIÓ
        // ==================================

        const info = document.createElement("div");

        info.classList.add("subject-info");


        const title = document.createElement("h2");

        title.textContent = subject.name;


        const description = document.createElement("p");

        description.textContent = "Elérhető korrepetitorok";


        info.appendChild(title);
        info.appendChild(description);


        // ==================================
        // NYÍL
        // ==================================

        const arrow = document.createElement("span");

        arrow.classList.add("subject-arrow");

        arrow.textContent = "→";


        // ==================================
        // KÁRTYA ÖSSZEÁLLÍTÁSA
        // ==================================

        card.appendChild(icon);
        card.appendChild(info);
        card.appendChild(arrow);

        link.appendChild(card);

        container.appendChild(link);
    });
}


// ==========================================
// TANTÁRGY DARABSZÁM
// ==========================================

function updateSubjectCount(count) {
    const subjectCount = document.getElementById("subjectCount");

    if (!subjectCount) {
        return;
    }

    if (count === 0) {
        subjectCount.textContent = "Nincs találat";
        return;
    }

    if (count === 1) {
        subjectCount.textContent = "1 tantárgy";
        return;
    }

    subjectCount.textContent = `${count} tantárgy`;
}


// ==========================================
// SUBJECT IKONOK
// ==========================================

function getSubjectIcon(subjectName) {
    const name = subjectName.toLowerCase();

    if (name.includes("matematika")) {
        return "∑";
    }

    if (name.includes("fizika")) {
        return "⚛";
    }

    if (name.includes("kémia")) {
        return "⚗";
    }

    if (
        name.includes("informatika") ||
        name.includes("programoz")
    ) {
        return "</>";
    }

    if (name.includes("statisztika")) {
        return "▥";
    }

    if (name.includes("történelem")) {
        return "◈";
    }

    if (name.includes("magyar")) {
        return "A";
    }

    if (name.includes("filozófia")) {
        return "Φ";
    }

    if (name.includes("etika")) {
        return "◆";
    }

    if (name.includes("pszichológia")) {
        return "Ψ";
    }

    if (name.includes("társadalomismeret")) {
        return "◉";
    }

    if (name.includes("jog")) {
        return "§";
    }

    if (name.includes("angol")) {
        return "EN";
    }

    if (name.includes("német")) {
        return "DE";
    }

    if (name.includes("francia")) {
        return "FR";
    }

    if (name.includes("spanyol")) {
        return "ES";
    }

    if (name.includes("olasz")) {
        return "IT";
    }

    if (name.includes("orosz")) {
        return "RU";
    }

    if (name.includes("latin")) {
        return "LA";
    }

    // Alapértelmezett ikon
    return "📚";
}
