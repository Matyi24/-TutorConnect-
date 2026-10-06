// ============================================================
// TUTORCONNECT CHAT
// ============================================================

let currentUser = null;
let conversations = [];
let currentConversationId = null;

// A keresőmező aktuális szövege
let searchQuery = "";

// Valós idejű kapcsolat és "gépel..." jelzés
let chatSocket = null;
let lastTypingSent = 0;
let typingTimer = null;
let typingIndicator = null;

// Dupla küldés elleni védelem
let isSending = false;


// ============================================================
// INITIALIZATION
// ============================================================

document.addEventListener("DOMContentLoaded", async () => {

    console.log("💬 Chat JS elindult");

    try {

        // 1. Megnézzük, ki van bejelentkezve
        await loadCurrentUser();

        // 2. Betöltjük a beszélgetéseket
        await loadConversations();

        // 3. Keresés a beszélgetések között
        setupConversationSearch();

        // 4. Új beszélgetés gomb (csak diákoknak)
        setupNewChatButton();

        // 5. Ha az URL-ben van ?tutor=ID, azonnal megnyitjuk vele a chatet
        await openTutorFromUrl();

        // 6. Valós idejű kapcsolat
        connectWebSocket();

    } catch (error) {

        console.error(
            "❌ Chat inicializálási hiba:",
            error
        );
    }

});


// ============================================================
// CURRENT USER
// ============================================================

async function loadCurrentUser() {

    const response = await fetch("/api/me");

    if (!response.ok) {

        throw new Error(
            "Nem sikerült lekérni a bejelentkezett felhasználót."
        );
    }

    const data = await response.json();

    console.log("👤 Aktuális user:", data);

    if (!data.loggedIn) {

        console.warn(
            "⚠️ Nincs bejelentkezett felhasználó."
        );

        return;
    }

    currentUser = data.user;

    console.log(
        "✅ Bejelentkezett user ID:",
        currentUser.id
    );
}


// ============================================================
// LOAD CONVERSATIONS
// ============================================================

async function loadConversations() {

    console.log(
        "📥 Beszélgetések betöltése..."
    );

    const response = await fetch(
        "/api/conversations"
    );

    if (!response.ok) {

        throw new Error(
            "Nem sikerült lekérni a beszélgetéseket."
        );
    }

    conversations = await response.json();

    console.log(
        "✅ Beszélgetések:",
        conversations
    );

    renderConversations();


    // Ha van legalább egy beszélgetés,
    // automatikusan megnyitjuk az elsőt.
    if (conversations.length > 0) {

        await selectConversation(
            conversations[0].id
        );
    }
}


// ------------------------------------------------------------
// REFRESH CONVERSATIONS
// ------------------------------------------------------------
//
// Újratölti a listát, de NEM vált át másik beszélgetésre.

async function refreshConversations() {

    const response = await fetch("/api/conversations");

    if (!response.ok) {
        return;
    }

    conversations = await response.json();

    renderConversations();
}


function findConversation(conversationId) {

    return conversations.find(
        item => Number(item.id) === Number(conversationId)
    );
}


// ============================================================
// RENDER CONVERSATIONS
// ============================================================

function renderConversations() {

    // Legfrissebb beszélgetés legyen legfelül
    sortConversations();


    const conversationList =
        document.querySelector(
            ".conversation-list"
        );

    if (!conversationList) {

        console.error(
            "❌ .conversation-list nem található!"
        );

        return;
    }


    conversationList.innerHTML = "";


    // Nincs beszélgetés
    if (conversations.length === 0) {

        conversationList.innerHTML = `
            <p class="no-conversations">
                Még nincs beszélgetésed.
            </p>
        `;

        return;
    }


    // A keresőmező alapján szűrt lista
    const visibleConversations =
        getVisibleConversations();


    // Van beszélgetés, de a keresésre egyik sem illik
    if (visibleConversations.length === 0) {

        conversationList.innerHTML = `
            <p class="no-conversations">
                Nincs találat.
            </p>
        `;

        return;
    }


    visibleConversations.forEach(
        (conversation) => {

            const element =
                createConversationElement(
                    conversation
                );

            conversationList.appendChild(
                element
            );
        }
    );
}


// ============================================================
// CREATE CONVERSATION ELEMENT
// ============================================================

function createConversationElement(
    conversation
) {

    const element =
        document.createElement("div");

    element.className =
        "conversation";


    if (
        conversation.id ===
        currentConversationId
    ) {

        element.classList.add("active");
    }


    const initials =
        getInitials(
            conversation.other_user_name
        );


    const time =
        formatConversationTime(
            conversation.last_message_time ||
            conversation.created_at
        );


    const lastMessage =
        conversation.last_message ||
        "Még nincs üzenet.";


    // Olvasatlan üzenetek (a megnyitott beszélgetésnél nem jelezzük)
    const unreadCount =
        Number(conversation.unread_count) || 0;

    const showUnread =
        unreadCount > 0 &&
        Number(conversation.id) !==
        Number(currentConversationId);

    if (showUnread) {

        element.classList.add("unread");
    }


    element.innerHTML = `

        <div class="avatar">
            ${escapeHtml(initials)}
        </div>

        <div class="conversation-info">

            <div class="conversation-top">

                <h3>
                    ${escapeHtml(
                        conversation.other_user_name
                    )}
                </h3>

                <span>
                    ${escapeHtml(time)}
                </span>

            </div>

            <div class="conversation-bottom">

                <p>
                    ${escapeHtml(lastMessage)}
                </p>

                ${
                    showUnread
                        ? `<div class="unread-badge">${
                            unreadCount > 99 ? "99+" : unreadCount
                        }</div>`
                        : ""
                }

            </div>

        </div>

    `;


    // Kattintás a beszélgetésre
    element.addEventListener(
        "click",
        () => {

            selectConversation(
                conversation.id
            );
        }
    );


    return element;
}


// ============================================================
// SELECT CONVERSATION
// ============================================================

async function selectConversation(
    conversationId
) {

    console.log(
        "💬 Conversation kiválasztva:",
        conversationId
    );


    currentConversationId =
        conversationId;


    // Aktív elem frissítése
    renderConversations();


    // Megkeressük a conversation objektumot
    const conversation =
        conversations.find(
            item =>
                item.id ===
                conversationId
        );


    if (!conversation) {

        console.error(
            "❌ Conversation nem található:",
            conversationId
        );

        return;
    }


    // Chat fejléc frissítése
    updateChatHeader(
        conversation
    );


    // A másik fél "gépel..." jelzése nem maradhat meg
    hideTypingIndicator();


    // Üzenetek betöltése (beszélgetésváltásnál mindig az aljára görgetünk)
    await loadMessages(
        conversationId,
        true
    );


    // A beszélgetés megnyílt, az üzenetek olvasottnak számítanak
    await markConversationAsRead(
        conversationId
    );
}


// ============================================================
// MARK AS READ
// ============================================================

async function markConversationAsRead(
    conversationId,
    force = false
) {

    const conversation =
        findConversation(
            conversationId
        );

    if (!conversation) {
        return;
    }

    // Ha nincs olvasatlan üzenet, nem kell hívni a szervert
    if (
        !force &&
        !(Number(conversation.unread_count) > 0)
    ) {
        return;
    }

    try {

        const response = await fetch(
            `/api/conversations/${conversationId}/read`,
            { method: "POST" }
        );

        if (!response.ok) {

            throw new Error(
                "Nem sikerült olvasottnak jelölni."
            );
        }

        conversation.unread_count = 0;

        renderConversations();

    } catch (error) {

        console.error(
            "❌ Olvasottnak jelölési hiba:",
            error
        );
    }
}


// ============================================================
// UPDATE CHAT HEADER
// ============================================================

function updateChatHeader(
    conversation
) {

    const avatar =
        document.querySelector(
            ".chat-user .avatar.large"
        );

    const name =
        document.querySelector(
            ".chat-user h2"
        );


    if (!avatar || !name) {

        console.error(
            "❌ Chat fejléc elemei nem találhatók."
        );

        return;
    }


    avatar.textContent =
        getInitials(
            conversation.other_user_name
        );


    name.textContent =
        conversation.other_user_name;
}


// ============================================================
// LOAD MESSAGES
// ============================================================

async function loadMessages(
    conversationId,
    forceScroll = false
) {

    console.log(
        "📥 Üzenetek betöltése:",
        conversationId
    );


    const response =
        await fetch(
            `/api/conversations/${conversationId}/messages`
        );


    if (!response.ok) {

        throw new Error(
            "Nem sikerült lekérni az üzeneteket."
        );
    }


    const messages =
        await response.json();


    console.log(
        "✅ Üzenetek:",
        messages
    );


    renderMessages(
        messages,
        forceScroll
    );
}


// ============================================================
// RENDER MESSAGES
// ============================================================

function renderMessages(
    messages,
    forceScroll = false
) {

    const messagesContainer =
        document.querySelector(
            ".messages"
        );


    if (!messagesContainer) {

        console.error(
            "❌ .messages nem található!"
        );

        return;
    }


    // Ha a felhasználó felgörgetett (régi üzeneteket olvas),
    // egy új üzenet ne dobja le az aljára.
    const previousScrollTop =
        messagesContainer.scrollTop;

    const wasAtBottom =
        messagesContainer.scrollHeight -
        messagesContainer.scrollTop -
        messagesContainer.clientHeight < 80;


    messagesContainer.innerHTML = "";


    let previousDay = null;


    messages.forEach(
        (message) => {

            // Dátum elválasztó, ha új nap kezdődik
            const messageDate =
                new Date(message.created_at);

            const dayKey =
                Number.isNaN(messageDate.getTime())
                    ? null
                    : messageDate.toDateString();

            if (dayKey && dayKey !== previousDay) {

                const separator =
                    document.createElement("div");

                separator.className =
                    "date-separator";

                separator.innerHTML = `
                    <span>${escapeHtml(
                        formatDateLabel(message.created_at)
                    )}</span>
                `;

                messagesContainer.appendChild(
                    separator
                );

                previousDay = dayKey;
            }


            const isOwnMessage =
                Number(message.sender_id) ===
                Number(currentUser.id);


            const group =
                document.createElement(
                    "div"
                );


            group.className =
                isOwnMessage
                    ? "message-group sent"
                    : "message-group received";


            const time =
                formatTime(
                    message.created_at
                );


            group.innerHTML = `

                <span class="message-time">
                    ${escapeHtml(time)}
                </span>

                <div class="message">
                    ${escapeHtml(
                        message.content
                    )}
                </div>

            `;


            messagesContainer.appendChild(
                group
            );
        }
    );


    if (forceScroll || wasAtBottom) {

        messagesContainer.scrollTop =
            messagesContainer.scrollHeight;

    } else {

        messagesContainer.scrollTop =
            previousScrollTop;
    }
}


// ============================================================
// HELPERS
// ============================================================

function getInitials(name) {

    if (!name) {
        return "?";
    }


    const parts =
        name
            .trim()
            .split(/\s+/);


    if (parts.length === 1) {

        return parts[0]
            .substring(0, 2)
            .toUpperCase();
    }


    return (
        parts[0][0] +
        parts[parts.length - 1][0]
    ).toUpperCase();
}


// ------------------------------------------------------------
// FORMAT TIME
// ------------------------------------------------------------

function formatTime(
    dateString
) {

    if (!dateString) {
        return "";
    }


    const date =
        new Date(dateString);


    if (Number.isNaN(
        date.getTime()
    )) {

        return "";
    }


    return date.toLocaleTimeString(
        "hu-HU",
        {
            hour: "2-digit",
            minute: "2-digit"
        }
    );
}


// ------------------------------------------------------------
// DATE HELPERS
// ------------------------------------------------------------

function isSameDay(a, b) {

    return a.toDateString() === b.toDateString();
}


// Elválasztó felirat az üzenetek között: Ma / Tegnap / dátum
function formatDateLabel(dateString) {

    const date = new Date(dateString);

    if (Number.isNaN(date.getTime())) {
        return "";
    }

    const today = new Date();

    const yesterday = new Date();
    yesterday.setDate(today.getDate() - 1);

    if (isSameDay(date, today)) {
        return "Ma";
    }

    if (isSameDay(date, yesterday)) {
        return "Tegnap";
    }

    return date.toLocaleDateString(
        "hu-HU",
        {
            year: "numeric",
            month: "long",
            day: "numeric"
        }
    );
}


// A bal oldali listában: ma idő, tegnap "Tegnap", régebbi rövid dátum
function formatConversationTime(dateString) {

    if (!dateString) {
        return "";
    }

    const date = new Date(dateString);

    if (Number.isNaN(date.getTime())) {
        return "";
    }

    const today = new Date();

    const yesterday = new Date();
    yesterday.setDate(today.getDate() - 1);

    if (isSameDay(date, today)) {
        return formatTime(dateString);
    }

    if (isSameDay(date, yesterday)) {
        return "Tegnap";
    }

    return date.toLocaleDateString(
        "hu-HU",
        {
            month: "short",
            day: "numeric"
        }
    );
}


// ------------------------------------------------------------
// SORT CONVERSATIONS
// ------------------------------------------------------------

function getConversationTimestamp(conversation) {

    const time = new Date(
        conversation.last_message_time ||
        conversation.created_at
    ).getTime();

    return Number.isNaN(time) ? 0 : time;
}


function sortConversations() {

    conversations.sort(
        (a, b) =>
            getConversationTimestamp(b) -
            getConversationTimestamp(a)
    );
}


// ------------------------------------------------------------
// CONVERSATION SEARCH
// ------------------------------------------------------------
//
// Kis- és nagybetűre, valamint ékezetekre érzéketlen keresés
// (pl. "arpad" megtalálja az "Árpád"-ot).

function normalizeText(value) {

    return String(value ?? "")
        .toLowerCase()
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "");
}


function getVisibleConversations() {

    const query =
        normalizeText(searchQuery).trim();

    if (!query) {
        return conversations;
    }

    return conversations.filter(
        (conversation) =>
            normalizeText(
                conversation.other_user_name
            ).includes(query) ||
            normalizeText(
                conversation.last_message
            ).includes(query)
    );
}


function setupConversationSearch() {

    const input =
        document.querySelector(
            ".conversation-search input"
        );

    if (!input) {

        console.error(
            "❌ A keresőmező nem található!"
        );

        return;
    }

    input.addEventListener(
        "input",
        () => {

            searchQuery = input.value;

            renderConversations();
        }
    );

    // Esc: a keresés törlése
    input.addEventListener(
        "keydown",
        (event) => {

            if (event.key === "Escape") {

                input.value = "";
                searchQuery = "";

                renderConversations();
            }
        }
    );
}


// ------------------------------------------------------------
// ESCAPE HTML
// ------------------------------------------------------------
//
// Az üzeneteket nem engedjük közvetlenül HTML-ként
// beilleszteni. Ez fontos biztonsági okból.

function escapeHtml(
    value
) {

    const div =
        document.createElement("div");

    div.textContent =
        value ?? "";

    return div.innerHTML;
}

// ============================================================
// MESSAGE FORM
// ============================================================

const messageForm = document.querySelector(".message-form");
const messageInput = document.querySelector(".message-form input[type='text']");

if (messageForm && messageInput) {

    // "Valaki gépel..." jelzés helye (az üzenetíró sáv felett)
    typingIndicator = document.createElement("div");
    typingIndicator.className = "typing-indicator";
    typingIndicator.innerHTML = "<span></span>";
    messageForm.parentNode.insertBefore(typingIndicator, messageForm);


    // Gépelés közben értesítjük a másik felet (legfeljebb 2 mp-enként)
    messageInput.addEventListener("input", () => {

        if (
            !currentConversationId ||
            !chatSocket ||
            chatSocket.readyState !== WebSocket.OPEN ||
            messageInput.value.trim() === ""
        ) {
            return;
        }

        const now = Date.now();

        if (now - lastTypingSent < 2000) {
            return;
        }

        lastTypingSent = now;

        chatSocket.send(JSON.stringify({
            type: "typing",
            conversation_id: currentConversationId
        }));
    });


    messageForm.addEventListener("submit", async (event) => {

        // Ne töltse újra az oldalt a form
        event.preventDefault();

        const content = messageInput.value.trim();

        if (!content || !currentConversationId || isSending) {
            return;
        }

        isSending = true;

        try {
            const response = await fetch(
                `/api/conversations/${currentConversationId}/messages`,
                {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ content })
                }
            );

            if (!response.ok) {
                throw new Error("Nem sikerült elküldeni az üzenetet.");
            }

            const savedMessage = await response.json();
            console.log("📨 Üzenet elküldve:", savedMessage);

            // Az input kiürítése
            messageInput.value = "";
            lastTypingSent = 0;

            // Az üzenetek újratöltése, saját üzenetnél mindig az aljára görgetünk
            await loadMessages(currentConversationId, true);

            // A bal oldali lista "utolsó üzenet" sorának frissítése
            const conversation = findConversation(currentConversationId);

            if (conversation) {
                conversation.last_message = savedMessage.content;
                conversation.last_message_time = savedMessage.created_at;
                renderConversations();
            }

        } catch (error) {
            console.error("❌ Üzenetküldési hiba:", error);
        } finally {
            isSending = false;
        }
    });
}


// ============================================================
// WEBSOCKET
// ============================================================

function connectWebSocket() {

    const protocol = location.protocol === "https:" ? "wss" : "ws";
    const socket = new WebSocket(`${protocol}://${location.host}`);

    chatSocket = socket;

    socket.addEventListener("open", () => {
        console.log("🔌 WebSocket kapcsolat él");
    });

    socket.addEventListener("message", async (event) => {

        const data = JSON.parse(event.data);

        if (data.type === "new_message") {
            await handleIncomingMessage(data.message);
        }

        if (data.type === "typing") {
            handleTyping(data.conversation_id);
        }
    });

    socket.addEventListener("close", () => {
        console.warn("🔌 WebSocket bezárult, újracsatlakozás 3 mp múlva...");
        setTimeout(connectWebSocket, 3000);
    });
}


async function handleIncomingMessage(message) {

    console.log("📩 Új üzenet érkezett:", message);

    const isOpen =
        Number(message.conversation_id) ===
        Number(currentConversationId);

    // Megérkezett az üzenet, a "gépel..." jelzés már nem kell
    if (isOpen) {
        hideTypingIndicator();
    }

    let conversation = findConversation(message.conversation_id);

    if (!conversation) {

        // Ez egy új beszélgetés (pl. egy diák most írt először),
        // ezért újratöltjük a listát. A szerver az olvasatlan
        // számot is megadja.
        await refreshConversations();

        conversation = findConversation(message.conversation_id);

        if (!conversation) {
            return;
        }

    } else {

        // A bal oldali lista frissítése
        conversation.last_message = message.content;
        conversation.last_message_time = message.created_at;

        // Ha nem ez a beszélgetés van megnyitva, olvasatlan
        if (!isOpen) {
            conversation.unread_count =
                (Number(conversation.unread_count) || 0) + 1;
        }

        renderConversations();
    }

    if (isOpen) {

        // Éppen ez a beszélgetés van megnyitva:
        // megjelenítjük, és rögtön olvasottnak jelöljük
        await loadMessages(currentConversationId);
        await markConversationAsRead(currentConversationId, true);

    } else if (!currentConversationId) {

        // Még nem volt megnyitva egyetlen beszélgetés sem
        await selectConversation(conversation.id);
    }
}


// ============================================================
// TYPING INDICATOR
// ============================================================

function handleTyping(conversationId) {

    // Csak a megnyitott beszélgetésnél jelezzük
    if (
        !typingIndicator ||
        Number(conversationId) !== Number(currentConversationId)
    ) {
        return;
    }

    const conversation = findConversation(conversationId);

    const name =
        conversation
            ? conversation.other_user_name
            : "A másik fél";

    typingIndicator.querySelector("span").textContent =
        `${name} gépel...`;

    typingIndicator.classList.add("visible");

    // 3 másodperc után magától eltűnik
    clearTimeout(typingTimer);
    typingTimer = setTimeout(hideTypingIndicator, 3000);
}


function hideTypingIndicator() {

    clearTimeout(typingTimer);

    if (typingIndicator) {
        typingIndicator.classList.remove("visible");
    }
}


// ============================================================
// NEW CONVERSATION
// ============================================================

function setupNewChatButton() {

    const button = document.querySelector(".new-chat-button");

    if (!button) {
        return;
    }

    // Új beszélgetést csak diák indíthat oktatóval
    if (!currentUser || currentUser.role !== "STUDENT") {
        button.style.display = "none";
        return;
    }

    button.addEventListener("click", openTutorPicker);
}


async function startConversationWith(tutorId) {

    const response = await fetch("/api/conversations", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ tutor_id: tutorId })
    });

    if (!response.ok) {
        throw new Error("Nem sikerült elindítani a beszélgetést.");
    }

    const conversation = await response.json();

    console.log("💬 Beszélgetés kész:", conversation);

    // Ha még nincs a listában, felülre tesszük
    if (!findConversation(conversation.id)) {
        conversations.unshift(conversation);
    }

    await selectConversation(conversation.id);

    const input = document.querySelector(".message-form input[type='text']");

    if (input) {
        input.focus();
    }
}


async function openTutorFromUrl() {

    const params = new URLSearchParams(location.search);
    const tutorId = Number(params.get("tutor"));

    if (!tutorId || !currentUser || currentUser.role !== "STUDENT") {
        return;
    }

    try {
        await startConversationWith(tutorId);
    } catch (error) {
        console.error("❌ Nem sikerült megnyitni az oktatóval a chatet:", error);
    }

    // Az URL-ből kivesszük a paramétert, hogy frissítéskor ne ismétlődjön
    history.replaceState(null, "", location.pathname);
}


async function openTutorPicker() {

    if (document.querySelector(".tutor-picker-overlay")) {
        return;
    }

    const overlay = document.createElement("div");
    overlay.className = "tutor-picker-overlay";

    overlay.innerHTML = `
        <div class="tutor-picker" role="dialog" aria-modal="true">

            <div class="tutor-picker-header">
                <h2>Új beszélgetés</h2>
                <button type="button" class="tutor-picker-close" aria-label="Bezárás">×</button>
            </div>

            <input
                type="text"
                class="tutor-picker-search"
                placeholder="Oktató vagy tantárgy keresése..."
            >

            <div class="tutor-picker-list">
                <p class="tutor-picker-empty">Betöltés...</p>
            </div>

        </div>
    `;

    document.body.appendChild(overlay);

    const list = overlay.querySelector(".tutor-picker-list");
    const search = overlay.querySelector(".tutor-picker-search");

    function closePicker() {
        document.removeEventListener("keydown", onKeyDown);
        overlay.remove();
    }

    function onKeyDown(event) {
        if (event.key === "Escape") {
            closePicker();
        }
    }

    document.addEventListener("keydown", onKeyDown);

    overlay.querySelector(".tutor-picker-close").addEventListener("click", closePicker);

    overlay.addEventListener("click", (event) => {
        if (event.target === overlay) {
            closePicker();
        }
    });

    search.focus();

    let tutors = [];

    try {

        const response = await fetch("/api/tutors");

        if (!response.ok) {
            throw new Error("Nem sikerült lekérni az oktatókat.");
        }

        tutors = await response.json();

    } catch (error) {

        console.error("❌ Oktatók betöltési hiba:", error);
        list.innerHTML = `<p class="tutor-picker-empty">Nem sikerült betölteni az oktatókat.</p>`;
        return;
    }

    function renderTutors() {

        const query = search.value.trim().toLowerCase();

        const filtered = tutors.filter((tutor) => {
            const text = `${tutor.full_name || ""} ${tutor.subjects || ""}`.toLowerCase();
            return text.includes(query);
        });

        list.innerHTML = "";

        if (filtered.length === 0) {
            list.innerHTML = `<p class="tutor-picker-empty">Nincs találat.</p>`;
            return;
        }

        filtered.forEach((tutor) => {

            const item = document.createElement("div");
            item.className = "tutor-picker-item";

            item.innerHTML = `
                <div class="avatar">${escapeHtml(getInitials(tutor.full_name))}</div>

                <div class="tutor-picker-info">
                    <h3>${escapeHtml(tutor.full_name)}</h3>
                    <p>${escapeHtml(tutor.subjects || "Nincs megadott tantárgy")}</p>
                </div>
            `;

            item.addEventListener("click", async () => {

                try {
                    await startConversationWith(tutor.id);
                    closePicker();
                } catch (error) {
                    console.error("❌ Beszélgetés indítási hiba:", error);
                    list.innerHTML = `<p class="tutor-picker-empty">Nem sikerült elindítani a beszélgetést.</p>`;
                }
            });

            list.appendChild(item);
        });
    }

    search.addEventListener("input", renderTutors);

    renderTutors();
}