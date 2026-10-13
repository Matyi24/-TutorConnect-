// ============================================================
// EGYEDI LEGÖRDÜLŐ MENÜ
// ============================================================
//
// A böngésző saját <select> listája nem formázható (és sötét módban
// sem illik az oldalhoz). Ez a script a megadott <select> elemeket
// ugyanolyan menüre cseréli, mint a Tantárgyak oldal kategória-szűrője.
//
// A valódi <select> megmarad (rejtve), így a többi script változtatás
// nélkül tovább használja: a `select.value` olvasása/írása és a
// "change" esemény ugyanúgy működik. Ha az opciók később töltődnek be
// (pl. a tantárgyak a szerverről), a menü magától frissül.
//
// Használat:  TCSelect.enhance(document.getElementById("rating"));

(function () {

    "use strict";

    const STYLE_ID = "tcs-styles";

    function injectStyles() {

        if (document.getElementById(STYLE_ID)) {
            return;
        }

        const style = document.createElement("style");
        style.id = STYLE_ID;

        style.textContent = `
            .tcs { position: relative; width: 100%; }
            .tcs.tcs-inline { width: auto; min-width: 210px; }
            .tcs > select { display: none !important; }

            .tcs-trigger {
                display: flex; align-items: center; justify-content: space-between;
                gap: 10px; width: 100%; height: 45px; padding: 0 5px 0 14px;
                box-sizing: border-box; border: 1px solid #dedff0; border-radius: 11px;
                background: #fafaff; color: #292944;
                font: inherit; font-size: 13px; font-weight: 600; text-align: left;
                cursor: pointer;
                transition: border-color .2s ease, box-shadow .2s ease, background .2s ease;
            }
            .tcs-trigger:hover { border-color: #cfd2ed; }
            .tcs.open .tcs-trigger,
            .tcs-trigger:focus-visible {
                outline: none; border-color: #5258d9; background: #fff;
                box-shadow: 0 0 0 3px rgba(82, 88, 217, .1);
            }

            .tcs-label { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

            .tcs-arrow {
                flex: none; display: flex; align-items: center; justify-content: center;
                width: 32px; height: 32px; border-radius: 9px;
                background: #e9e9fb; transition: background .2s ease, box-shadow .2s ease;
            }
            .tcs-arrow::before {
                content: ""; width: 8px; height: 8px;
                border-right: 2px solid #5258d9; border-bottom: 2px solid #5258d9;
                transform: rotate(45deg) translate(-2px, -2px);
                transition: transform .25s ease, border-color .2s ease;
            }
            .tcs-trigger:hover .tcs-arrow { background: #dedeff; }
            .tcs.open .tcs-arrow { background: #4c6ef5; box-shadow: 0 5px 14px rgba(76, 110, 245, .25); }
            .tcs.open .tcs-arrow::before {
                border-color: #fff; transform: rotate(225deg) translate(-2px, -2px);
            }

            .tcs-menu {
                position: absolute; top: calc(100% + 8px); left: 0; z-index: 1000;
                min-width: 100%; max-height: 300px; overflow-y: auto; padding: 7px;
                box-sizing: border-box; border: 1px solid #e3e3f0; border-radius: 14px;
                background: #fff;
                box-shadow: 0 15px 35px rgba(40, 40, 90, .14), 0 4px 10px rgba(40, 40, 90, .05);
                opacity: 0; visibility: hidden;
                transform: translateY(-8px) scale(.98); transform-origin: top center;
                transition: opacity .2s ease, visibility .2s ease, transform .2s ease;
            }
            .tcs.tcs-inline .tcs-menu { left: auto; right: 0; white-space: nowrap; }
            .tcs.open .tcs-menu { opacity: 1; visibility: visible; transform: none; }

            .tcs-item {
                padding: 10px 13px; border-radius: 9px;
                color: #34344e; font-size: 13.5px; font-weight: 500; cursor: pointer;
                transition: background .15s ease, color .15s ease, transform .15s ease;
            }
            .tcs-item:hover, .tcs-item.focus { background: #f0efff; color: #4c6ef5; transform: translateX(2px); }
            .tcs-item.active { background: #4c6ef5; color: #fff; font-weight: 600; }
            .tcs-item.active:hover, .tcs-item.active.focus { background: #3b5bdb; color: #fff; transform: none; }

            .tcs-menu::-webkit-scrollbar { width: 8px; }
            .tcs-menu::-webkit-scrollbar-thumb { background: #d4d6ee; border-radius: 8px; }

            /* ---------- sötét mód ---------- */
            html[data-theme="dark"] .tcs-trigger {
                background: #1d2338; border-color: #2f3860; color: #eef0f8;
            }
            html[data-theme="dark"] .tcs-trigger:hover { border-color: #3f4a7a; }
            html[data-theme="dark"] .tcs.open .tcs-trigger,
            html[data-theme="dark"] .tcs-trigger:focus-visible {
                background: #232a47; border-color: #8aa0ff;
                box-shadow: 0 0 0 3px rgba(138, 160, 255, .16);
            }
            html[data-theme="dark"] .tcs-arrow { background: #2a3150; }
            html[data-theme="dark"] .tcs-arrow::before { border-color: #aebcff; }
            html[data-theme="dark"] .tcs-trigger:hover .tcs-arrow { background: #343d63; }
            html[data-theme="dark"] .tcs.open .tcs-arrow { background: #6c82f5; box-shadow: none; }
            html[data-theme="dark"] .tcs.open .tcs-arrow::before { border-color: #fff; }
            html[data-theme="dark"] .tcs-menu {
                background: #171c2e; border-color: #2a3150;
                box-shadow: 0 15px 35px rgba(0, 0, 0, .55);
            }
            html[data-theme="dark"] .tcs-item { color: #d5d9f2; }
            html[data-theme="dark"] .tcs-item:hover,
            html[data-theme="dark"] .tcs-item.focus { background: #232a47; color: #b3c0ff; }
            html[data-theme="dark"] .tcs-item.active { background: #6c82f5; color: #fff; }
            html[data-theme="dark"] .tcs-item.active:hover,
            html[data-theme="dark"] .tcs-item.active.focus { background: #8aa0ff; color: #0f1320; }
            html[data-theme="dark"] .tcs-menu::-webkit-scrollbar-thumb { background: #3a4366; }
        `;

        document.head.appendChild(style);
    }


    const instances = [];

    function closeAll(except) {
        instances.forEach((api) => {
            if (api !== except) {
                api.close();
            }
        });
    }

    document.addEventListener("click", (event) => {
        instances.forEach((api) => {
            if (!api.root.contains(event.target)) {
                api.close();
            }
        });
    });


    function enhance(select, options) {

        if (!select || select.dataset.tcsReady) {
            return null;
        }

        injectStyles();

        select.dataset.tcsReady = "1";

        const inline = Boolean(options && options.inline);

        const root = document.createElement("div");
        root.className = "tcs" + (inline ? " tcs-inline" : "");

        select.parentNode.insertBefore(root, select);
        root.appendChild(select);

        const trigger = document.createElement("button");
        trigger.type = "button";
        trigger.className = "tcs-trigger";
        trigger.setAttribute("aria-haspopup", "listbox");
        trigger.setAttribute("aria-expanded", "false");

        const label = document.createElement("span");
        label.className = "tcs-label";

        const arrow = document.createElement("span");
        arrow.className = "tcs-arrow";
        arrow.setAttribute("aria-hidden", "true");

        trigger.append(label, arrow);

        const menu = document.createElement("div");
        menu.className = "tcs-menu";
        menu.setAttribute("role", "listbox");

        root.append(trigger, menu);

        // A <label for="..."> kattintására is a gomb kapjon fókuszt
        if (select.id) {
            const external = document.querySelector(`label[for="${select.id}"]`);
            if (external) {
                external.addEventListener("click", (event) => {
                    event.preventDefault();
                    trigger.focus();
                });
            }
        }

        let focusIndex = -1;

        function items() {
            return Array.from(menu.querySelectorAll(".tcs-item"));
        }

        function syncLabel() {

            const option = select.options[select.selectedIndex];

            label.textContent = option ? option.textContent.trim() : "";

            items().forEach((item, index) => {
                const isActive = index === select.selectedIndex;
                item.classList.toggle("active", isActive);
                item.setAttribute("aria-selected", String(isActive));
            });
        }

        function build() {

            menu.innerHTML = "";

            Array.from(select.options).forEach((option, index) => {

                const item = document.createElement("div");
                item.className = "tcs-item";
                item.setAttribute("role", "option");
                item.textContent = option.textContent.trim();

                item.addEventListener("click", (event) => {
                    event.stopPropagation();
                    choose(index);
                });

                menu.appendChild(item);
            });

            syncLabel();
        }

        function choose(index) {

            const changed = select.selectedIndex !== index;

            select.selectedIndex = index;
            syncLabel();
            api.close();
            trigger.focus();

            if (changed) {
                select.dispatchEvent(new Event("change", { bubbles: true }));
            }
        }

        function setFocus(index) {

            const list = items();

            if (!list.length) {
                return;
            }

            focusIndex = (index + list.length) % list.length;

            list.forEach((item, i) => item.classList.toggle("focus", i === focusIndex));

            list[focusIndex].scrollIntoView({ block: "nearest" });
        }

        const api = {

            root,

            open() {
                closeAll(api);
                root.classList.add("open");
                trigger.setAttribute("aria-expanded", "true");
                setFocus(select.selectedIndex);

                const active = menu.querySelector(".tcs-item.active");
                if (active) {
                    active.scrollIntoView({ block: "nearest" });
                }
            },

            close() {
                root.classList.remove("open");
                trigger.setAttribute("aria-expanded", "false");
                items().forEach((item) => item.classList.remove("focus"));
            },

            refresh: build
        };

        trigger.addEventListener("click", (event) => {
            event.stopPropagation();
            root.classList.contains("open") ? api.close() : api.open();
        });

        trigger.addEventListener("keydown", (event) => {

            const isOpen = root.classList.contains("open");

            if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                event.preventDefault();
                const step = event.key === "ArrowDown" ? 1 : -1;
                isOpen ? setFocus(focusIndex + step) : api.open();
            } else if ((event.key === "Enter" || event.key === " ") && isOpen) {
                event.preventDefault();
                choose(focusIndex >= 0 ? focusIndex : select.selectedIndex);
            } else if (event.key === "Escape" && isOpen) {
                event.preventDefault();
                api.close();
            } else if (event.key === "Tab") {
                api.close();
            }
        });


        // Opciók utólagos betöltése / módosítása
        new MutationObserver(build).observe(select, { childList: true });

        // Ha egy másik script a .value / .selectedIndex értéket írja,
        // a felirat is kövesse (ilyenkor nincs "change" esemény)
        ["value", "selectedIndex"].forEach((prop) => {

            const descriptor = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, prop);

            Object.defineProperty(select, prop, {
                configurable: true,
                get() { return descriptor.get.call(this); },
                set(value) { descriptor.set.call(this, value); syncLabel(); }
            });
        });

        select.addEventListener("change", syncLabel);

        build();
        instances.push(api);

        return api;
    }


    window.TCSelect = { enhance };

})();
