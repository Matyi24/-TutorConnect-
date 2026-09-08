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

        console.error("Hiba:", error);

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

        const link = document.createElement("a");


        // Subject ID átadása
        link.href = `matek.html?subject_id=${subject.id}`;


        const card = document.createElement("div");

        card.classList.add("subcard");

        card.textContent = subject.name;


        link.appendChild(card);

        container.appendChild(link);

    });

}
