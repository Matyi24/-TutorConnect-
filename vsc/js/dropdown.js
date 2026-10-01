document.addEventListener("DOMContentLoaded", () => {

    const dropdownButton = document.getElementById("dropdownButton");
    const dropdownMenu = document.getElementById("dropdownMenu");
    const selectedSubject = document.getElementById("selectedSubject");
    const dropdownItems = document.querySelectorAll(".dropdown-item");

    // Ha valamelyik elem hiányzik, ne fusson tovább
    if (
        !dropdownButton ||
        !dropdownMenu ||
        !selectedSubject
    ) {
        return;
    }


    // ==========================================
    // DROPDOWN MEGNYITÁSA / BEZÁRÁSA
    // ==========================================

    dropdownButton.addEventListener("click", (event) => {

        event.stopPropagation();

        const isOpen = dropdownMenu.classList.toggle("open");

        dropdownButton.classList.toggle("active", isOpen);

        dropdownButton.setAttribute(
            "aria-expanded",
            isOpen
        );
    });


    // ==========================================
    // KATEGÓRIA KIVÁLASZTÁSA
    // ==========================================

    dropdownItems.forEach(item => {

        item.addEventListener("click", (event) => {

            event.stopPropagation();

            const selectedText = item.textContent.trim();

            // Kiválasztott szöveg frissítése
            selectedSubject.textContent = selectedText;

            // Előző aktív elem eltávolítása
            dropdownItems.forEach(dropdownItem => {
                dropdownItem.classList.remove("active");
            });

            // Kiválasztott elem aktívvá tétele
            item.classList.add("active");

            // Menü bezárása
            dropdownMenu.classList.remove("open");

            // Gomb állapotának visszaállítása
            dropdownButton.classList.remove("active");

            dropdownButton.setAttribute(
                "aria-expanded",
                "false"
            );
        });

    });


    // ==========================================
    // KATTINTÁS A DROPDOWNON KÍVÜL
    // ==========================================

    document.addEventListener("click", (event) => {

        if (
            !dropdownMenu.contains(event.target) &&
            !dropdownButton.contains(event.target)
        ) {

            dropdownMenu.classList.remove("open");

            dropdownButton.classList.remove("active");

            dropdownButton.setAttribute(
                "aria-expanded",
                "false"
            );
        }

    });

});
