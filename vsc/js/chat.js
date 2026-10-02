// ============================================================
// TUTORCONNECT CHAT
// ============================================================

let currentUser = null;
let conversations = [];
let currentConversationId = null;


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

        // 3. Új beszélgetés gomb (csak diákoknak)
        setupNewChatButton();

        // 4. Ha az URL-ben van ?tutor=ID, azonnal megnyitjuk vele a chatet
        await openTutorFromUrl();

        // 5. Valós idejű kapcsolat
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


    conversations.forEach(
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
        formatTime(
            conversation.last_message_time
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


    // Üzenetek betöltése
    await loadMessages(
        conversationId
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
    conversationId
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
        messages
    );
}


// ============================================================
// RENDER MESSAGES
// ============================================================

function renderMessages(
    messages
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


    messagesContainer.innerHTML = "";


    messages.forEach(
        (message) => {

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


    // Automatikusan görgessünk az aljára
    messagesContainer.scrollTop =
        messagesContainer.scrollHeight;
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

    messageForm.addEventListener("submit", async (event) => {

        // Ne töltse újra az oldalt a form
        event.preventDefault();

        const content = messageInput.value.trim();

        if (!content || !currentConversationId) {
            return;
        }

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

            // Az üzenetek újratöltése (így az új is megjelenik)
            await loadMessages(currentConversationId);

            // A bal oldali lista "utolsó üzenet" sorának frissítése
            const conversation = conversations.find(
                item => item.id === currentConversationId
            );

            if (conversation) {
                conversation.last_message = savedMessage.content;
                conversation.last_message_time = savedMessage.created_at;
                renderConversations();
            }

        } catch (error) {
            console.error("❌ Üzenetküldési hiba:", error);
        }
    });
}


// ============================================================
// WEBSOCKET
// ============================================================

function connectWebSocket() {

    const protocol = location.protocol === "https:" ? "wss" : "ws";
    const socket = new WebSocket(`${protocol}://${location.host}`);

    socket.addEventListener("open", () => {
        console.log("🔌 WebSocket kapcsolat él");
    });

    socket.addEventListener("message", async (event) => {

        const data = JSON.parse(event.data);

        if (data.type === "new_message") {
            await handleIncomingMessage(data.message);
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