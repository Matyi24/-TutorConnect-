const dropdownButton = document.getElementById("dropdownButton");
const dropdownMenu = document.getElementById("dropdownMenu");
const selectedSubject = document.getElementById("selectedSubject");

const dropdownItems = document.querySelectorAll(".dropdown-item");

// Menü megnyitása / bezárása
dropdownButton.addEventListener("click", function () {
    dropdownMenu.classList.toggle("open");
});

// Ha kiválasztunk egy tárgyat
dropdownItems.forEach(function (item) {

    item.addEventListener("click", function () {

        // A kiválasztott szöveg
        const selectedText = item.textContent;

        // Átírjuk a "Minden tárgy" szöveget
        selectedSubject.textContent = selectedText;

        // Bezárjuk a menüt
        dropdownMenu.classList.remove("open");
    });

});
