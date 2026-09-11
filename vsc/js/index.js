document.addEventListener("DOMContentLoaded", () => {

    const subjectContainer = document.querySelector(".grid-container");

    if (!subjectContainer) {
        return;
    }

    loadSubjects();

});


let allSubjects = [];


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

        // Kezdetben minden subject
        displaySubjects(allSubjects);

        // Dropdown figyelése
        setupCategoryFilter();

    } catch (error) {

        console.error("Hiba a subjectek betöltésekor:", error);

    }

}


// ==========================================
// KATEGÓRIA SZŰRÉS
// ==========================================

function setupCategoryFilter() {

    const dropdownMenu = document.getElementById("dropdownMenu");

    if (!dropdownMenu) {
        return;
    }

    const dropdownItems = dropdownMenu.querySelectorAll(".dropdown-item");

    dropdownItems.forEach(item => {

        item.addEventListener("click", () => {

            const selectedCategory = item.textContent.trim();

            console.log("Kiválasztott kategória:", selectedCategory);

            // Minden tárgy
            if (selectedCategory === "Minden tárgy") {

                displaySubjects(allSubjects);

                return;
            }

            // Szűrés kategória alapján
            const filteredSubjects = allSubjects.filter(subject => {

                return subject.category === selectedCategory;

            });

            displaySubjects(filteredSubjects);

        });

    });

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


    subjects.forEach(subject => {

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

    if (name.includes("informatika")) {
        return "</>";
    }

    if (name.includes("programoz")) {
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