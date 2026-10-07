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

// Csatolmány (még el nem küldött, kiválasztott fájl)
let pendingAttachment = null;
let pendingPreviewUrl = null;
let attachmentBar = null;
let attachmentErrorTimer = null;

const MAX_ATTACHMENT_SIZE = 10 * 1024 * 1024; // 10 MB

const IMAGE_EXTENSIONS = ["jpg", "jpeg", "png", "gif", "webp"];

const ALLOWED_EXTENSIONS = [
    ...IMAGE_EXTENSIONS,
    "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt"
];


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

        // 4/b. A fejléc menüje (profil megtekintése)
        setupChatMenu();

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


    // Másik beszélgetésre váltva a kiválasztott fájl nem mehet át
    if (Number(currentConversationId) !== Number(conversationId)) {
        clearAttachment();
    }

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

                <div class="message${
                    message.attachment_name ? " has-attachment" : ""
                }">
                    ${buildMessageContent(message)}
                </div>

            `;


            messagesContainer.appendChild(
                group
            );
        }
    );


    const stickToBottom =
        forceScroll || wasAtBottom;

    if (stickToBottom) {

        messagesContainer.scrollTop =
            messagesContainer.scrollHeight;

    } else {

        messagesContainer.scrollTop =
            previousScrollTop;
    }

    // A képek betöltődés után megnövelik a magasságot,
    // ilyenkor újra az aljára görgetünk (ha ott voltunk)
    messagesContainer
        .querySelectorAll("img.message-image")
        .forEach((image) => {

            image.addEventListener(
                "load",
                () => {

                    if (stickToBottom) {

                        messagesContainer.scrollTop =
                            messagesContainer.scrollHeight;
                    }
                }
            );
        });
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
// ATTACHMENT HELPERS
// ============================================================

function getFileExtension(fileName) {

    const name = String(fileName || "");
    const index = name.lastIndexOf(".");

    return index === -1
        ? ""
        : name.slice(index + 1).toLowerCase();
}


function formatFileSize(bytes) {

    const size = Number(bytes);

    if (!size || size < 0) {
        return "";
    }

    if (size < 1024) {
        return `${size} B`;
    }

    if (size < 1024 * 1024) {
        return `${Math.round(size / 1024)} KB`;
    }

    return `${(size / (1024 * 1024)).toFixed(1).replace(".", ",")} MB`;
}


// Attribútumba illesztett szöveghez (az idézőjeleket is kezeli)
function escapeAttribute(value) {

    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}


// Egy üzenet buborékjának tartalma (szöveg, kép vagy fájlkártya)
function buildMessageContent(message) {

    const text = message.content || "";

    if (!message.attachment_name) {
        return escapeHtml(text);
    }

    const url =
        `/api/messages/${encodeURIComponent(message.id)}/attachment`;

    const name = message.attachment_name;

    let html;

    if (String(message.attachment_type || "").startsWith("image/")) {

        html = `
            <a class="message-image-link" href="${url}" target="_blank" rel="noopener">
                <img class="message-image" src="${url}" alt="${escapeAttribute(name)}">
            </a>
        `;

    } else {

        html = `
            <a class="message-file" href="${url}" download="${escapeAttribute(name)}">
                <span class="file-badge">${escapeHtml(getFileExtension(name).slice(0, 4))}</span>
                <span class="file-info">
                    <span class="file-name">${escapeHtml(name)}</span>
                    <span class="file-size">${escapeHtml(formatFileSize(message.attachment_size))}</span>
                </span>
            </a>
        `;
    }

    // Ha nincs külön szöveg, az üzenet szövege a fájl neve volt,
    // azt nem írjuk ki még egyszer.
    if (text && text !== message.attachment_name) {

        html += `<div class="message-caption">${escapeHtml(text)}</div>`;
    }

    return html;
}


// ------------------------------------------------------------
// KIVÁLASZTOTT (MÉG NEM ELKÜLDÖTT) CSATOLMÁNY
// ------------------------------------------------------------

function selectAttachment(file) {

    if (!file) {
        return;
    }

    const extension = getFileExtension(file.name);

    if (!ALLOWED_EXTENSIONS.includes(extension)) {

        showAttachmentError("Ez a fájltípus nem engedélyezett.");
        return;
    }

    if (file.size === 0) {

        showAttachmentError("A fájl üres.");
        return;
    }

    if (file.size > MAX_ATTACHMENT_SIZE) {

        showAttachmentError("A fájl túl nagy (legfeljebb 10 MB).");
        return;
    }

    clearAttachment();

    pendingAttachment = file;

    if (IMAGE_EXTENSIONS.includes(extension)) {
        pendingPreviewUrl = URL.createObjectURL(file);
    }

    renderAttachmentBar();
}


function clearAttachment() {

    if (pendingPreviewUrl) {
        URL.revokeObjectURL(pendingPreviewUrl);
    }

    pendingAttachment = null;
    pendingPreviewUrl = null;

    renderAttachmentBar();
}


function renderAttachmentBar() {

    if (!attachmentBar) {
        return;
    }

    clearTimeout(attachmentErrorTimer);

    attachmentBar.innerHTML = "";

    if (!pendingAttachment) {

        attachmentBar.className = "attachment-bar";
        return;
    }

    attachmentBar.className = "attachment-bar visible";

    const chip = document.createElement("div");
    chip.className = "attachment-chip";

    if (pendingPreviewUrl) {

        const image = document.createElement("img");
        image.className = "attachment-thumb";
        image.src = pendingPreviewUrl;
        image.alt = "";
        chip.appendChild(image);

    } else {

        const badge = document.createElement("span");
        badge.className = "file-badge";
        badge.textContent = getFileExtension(pendingAttachment.name).slice(0, 4);
        chip.appendChild(badge);
    }

    const info = document.createElement("div");
    info.className = "attachment-chip-info";

    const name = document.createElement("span");
    name.className = "attachment-chip-name";
    name.textContent = pendingAttachment.name;

    const size = document.createElement("span");
    size.className = "attachment-chip-size";
    size.textContent = formatFileSize(pendingAttachment.size);

    info.appendChild(name);
    info.appendChild(size);
    chip.appendChild(info);

    const remove = document.createElement("button");
    remove.type = "button";
    remove.className = "attachment-remove";
    remove.setAttribute("aria-label", "Csatolmány eltávolítása");
    remove.textContent = "×";
    remove.addEventListener("click", clearAttachment);
    chip.appendChild(remove);

    attachmentBar.appendChild(chip);
}


function showAttachmentError(text) {

    if (!attachmentBar) {
        return;
    }

    clearTimeout(attachmentErrorTimer);

    attachmentBar.className = "attachment-bar visible error";
    attachmentBar.textContent = text;

    // 4 másodperc után visszaáll (vagy eltűnik)
    attachmentErrorTimer = setTimeout(renderAttachmentBar, 4000);
}


// ============================================================
// MESSAGE FORM
// ============================================================

const messageForm = document.querySelector(".message-form");
const messageInput = document.querySelector(".message-form input[type='text']");
const attachmentButton = document.querySelector(".message-form .attachment-button");

if (messageForm && messageInput) {

    // "Valaki gépel..." jelzés helye (az üzenetíró sáv felett)
    typingIndicator = document.createElement("div");
    typingIndicator.className = "typing-indicator";
    typingIndicator.innerHTML = "<span></span>";
    messageForm.parentNode.insertBefore(typingIndicator, messageForm);

    // A kiválasztott csatolmány sávja (az üzenetíró sáv felett)
    attachmentBar = document.createElement("div");
    attachmentBar.className = "attachment-bar";
    messageForm.parentNode.insertBefore(attachmentBar, messageForm);


    // Rejtett fájlválasztó, a + gomb nyitja meg
    if (attachmentButton) {

        const fileInput = document.createElement("input");
        fileInput.type = "file";
        fileInput.hidden = true;
        fileInput.accept = ALLOWED_EXTENSIONS.map((ext) => "." + ext).join(",");
        document.body.appendChild(fileInput);

        attachmentButton.title = "Fájl csatolása";

        attachmentButton.addEventListener("click", (event) => {

            // A gomb ne küldje el a formot
            event.preventDefault();

            if (!currentConversationId) {
                return;
            }

            fileInput.click();
        });

        fileInput.addEventListener("change", () => {

            selectAttachment(fileInput.files[0]);

            // Így ugyanaz a fájl újra kiválasztható
            fileInput.value = "";
        });
    }


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

        // Szöveg vagy csatolmány kell hozzá
        if (
            (!content && !pendingAttachment) ||
            !currentConversationId ||
            isSending
        ) {
            return;
        }

        isSending = true;

        try {

            let response;

            if (pendingAttachment) {

                // Fájl + (opcionális) szöveg együtt
                const formData = new FormData();
                formData.append("content", content);
                formData.append("file", pendingAttachment);

                response = await fetch(
                    `/api/conversations/${currentConversationId}/attachments`,
                    {
                        method: "POST",
                        body: formData
                    }
                );

            } else {

                response = await fetch(
                    `/api/conversations/${currentConversationId}/messages`,
                    {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ content })
                    }
                );
            }

            if (!response.ok) {

                const errorData = await response.json().catch(() => ({}));

                throw new Error(
                    errorData.error || "Nem sikerült elküldeni az üzenetet."
                );
            }

            const savedMessage = await response.json();
            console.log("📨 Üzenet elküldve:", savedMessage);

            // Az input és a csatolmány kiürítése
            messageInput.value = "";
            lastTypingSent = 0;
            clearAttachment();

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

            // Fájlküldésnél a hibát a felhasználó is látja
            if (pendingAttachment) {
                showAttachmentError(error.message);
            }

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


// ============================================================
// CHAT MENÜ (⋮) ÉS OKTATÓ PROFIL
// ============================================================
//
// A fejléc ⋮ gombja egy kis menüt nyit. Jelenleg egyetlen eleme van:
// "Profil megtekintése". Ez csak diákoknak jelenik meg, mert a másik
// fél ilyenkor oktató, akinek van profilja (óradíj, értékelések).

let chatMenu = null;
let profileModal = null;
let tutorsCache = null;


function getOtherTutorId() {

    if (!currentUser || currentUser.role !== "STUDENT") {
        return null;
    }

    const conversation = findConversation(currentConversationId);

    return conversation ? conversation.tutor_id : null;
}


function setupChatMenu() {

    const button = document.querySelector(".chat-options");
    const header = document.querySelector(".chat-header");

    if (!button || !header) {
        return;
    }

    chatMenu = document.createElement("div");
    chatMenu.className = "chat-menu";
    header.appendChild(chatMenu);

    button.setAttribute("aria-haspopup", "menu");
    button.setAttribute("aria-expanded", "false");

    button.addEventListener("click", (event) => {

        event.stopPropagation();

        if (chatMenu.classList.contains("open")) {
            closeChatMenu();
        } else {
            openChatMenu();
        }
    });

    // Kattintás a menün kívülre vagy Esc: bezárás
    document.addEventListener("click", closeChatMenu);

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeChatMenu();
        }
    });
}


function openChatMenu() {

    if (!chatMenu) {
        return;
    }

    chatMenu.innerHTML = "";

    // A menü elemei a kiválasztott beszélgetéstől függenek
    if (currentConversationId && getOtherTutorId()) {

        const item = document.createElement("button");
        item.type = "button";
        item.className = "chat-menu-item";
        item.textContent = "Profil megtekintése";

        item.addEventListener("click", () => {
            closeChatMenu();
            openTutorProfile(getOtherTutorId());
        });

        chatMenu.appendChild(item);

    } else {

        const empty = document.createElement("div");
        empty.className = "chat-menu-empty";
        empty.textContent = "Nincs elérhető művelet.";

        chatMenu.appendChild(empty);
    }

    chatMenu.classList.add("open");

    document
        .querySelector(".chat-options")
        .setAttribute("aria-expanded", "true");
}


function closeChatMenu() {

    if (!chatMenu || !chatMenu.classList.contains("open")) {
        return;
    }

    chatMenu.classList.remove("open");

    document
        .querySelector(".chat-options")
        .setAttribute("aria-expanded", "false");
}


// ------------------------------------------------------------
// PROFIL ABLAK
// ------------------------------------------------------------

function ensureProfileModal() {

    if (profileModal) {
        return profileModal;
    }

    profileModal = document.createElement("div");
    profileModal.className = "profile-modal";
    profileModal.setAttribute("aria-hidden", "true");

    profileModal.innerHTML = `
        <div class="profile-overlay"></div>

        <div class="profile-window" role="dialog" aria-modal="true">
            <button type="button" class="profile-close" aria-label="Profil bezárása">×</button>
            <div class="profile-content"></div>
        </div>
    `;

    document.body.appendChild(profileModal);

    profileModal
        .querySelector(".profile-overlay")
        .addEventListener("click", closeTutorProfile);

    profileModal
        .querySelector(".profile-close")
        .addEventListener("click", closeTutorProfile);

    document.addEventListener("keydown", (event) => {
        if (
            event.key === "Escape" &&
            profileModal.classList.contains("active")
        ) {
            closeTutorProfile();
        }
    });

    return profileModal;
}


function closeTutorProfile() {

    if (!profileModal) {
        return;
    }

    profileModal.classList.remove("active");
    profileModal.setAttribute("aria-hidden", "true");
}


async function getTutorById(tutorId) {

    // Az oktatók listáját egyszer kérjük le, utána gyorsítótárból megy
    if (!tutorsCache) {

        const response = await fetch("/api/tutors");

        if (!response.ok) {
            throw new Error("Nem sikerült lekérni az oktatókat.");
        }

        tutorsCache = await response.json();
    }

    return tutorsCache.find(
        (tutor) => Number(tutor.id) === Number(tutorId)
    );
}


function getStars(rating) {

    const rounded = Math.max(
        0,
        Math.min(5, Math.round(Number(rating) || 0))
    );

    return "★".repeat(rounded) + "☆".repeat(5 - rounded);
}


async function openTutorProfile(tutorId) {

    const modal = ensureProfileModal();
    const content = modal.querySelector(".profile-content");

    content.innerHTML = `<p class="profile-loading">Betöltés...</p>`;

    modal.classList.add("active");
    modal.setAttribute("aria-hidden", "false");

    let tutor;
    let reviews = [];

    try {

        tutor = await getTutorById(tutorId);

        if (!tutor) {
            throw new Error("Az oktató nem található.");
        }

        const reviewsResponse = await fetch(
            `/api/tutors/${encodeURIComponent(tutorId)}/reviews`
        );

        // Az értékelések nélkül is megjelenhet a profil
        if (reviewsResponse.ok) {
            reviews = await reviewsResponse.json();
        }

    } catch (error) {

        console.error("❌ Profil betöltési hiba:", error);

        content.innerHTML = `
            <p class="profile-loading">Nem sikerült betölteni a profilt.</p>
        `;

        return;
    }

    const name = tutor.full_name || "Ismeretlen oktató";
    const subjects = tutor.subjects || "Nincs megadott tantárgy";
    const bio = tutor.bio || "Az oktató még nem adott meg bemutatkozást.";
    const price = Number(tutor.hourly_rate) || 0;
    const rating = Number(tutor.average_rating) || 0;
    const reviewCount = Array.isArray(reviews) ? reviews.length : 0;

    const reviewsHtml = reviewCount === 0
        ? `<p class="profile-no-reviews">Még nincs értékelés.</p>`
        : reviews.map((review) => {

            const date = new Date(review.created_at);

            const dateText = Number.isNaN(date.getTime())
                ? ""
                : date.toLocaleDateString("hu-HU");

            return `
                <article class="profile-review">
                    <div class="profile-review-top">
                        <strong>${escapeHtml(review.reviewer_name || "Névtelen")}</strong>
                        <span class="profile-review-stars">${getStars(review.rating)}</span>
                    </div>
                    ${dateText ? `<span class="profile-review-date">${escapeHtml(dateText)}</span>` : ""}
                    <p>${escapeHtml(review.comment || "Az értékelő nem írt szöveges értékelést.")}</p>
                </article>
            `;
        }).join("");

    content.innerHTML = `
        <div class="profile-head">
            <div class="avatar large">${escapeHtml(getInitials(name))}</div>

            <div>
                <span class="profile-label">OKTATÓ PROFIL</span>
                <h2>${escapeHtml(name)}</h2>
                <p class="profile-subjects">${escapeHtml(subjects)}</p>

                <div class="profile-rating">
                    <strong>${rating > 0 ? rating.toFixed(1) : "0.0"}</strong>
                    <span class="profile-review-stars">${getStars(rating)}</span>
                    <small>${reviewCount} értékelés</small>
                </div>
            </div>
        </div>

        <div class="profile-facts">
            <div>
                <span>Óradíj</span>
                <strong>${price.toLocaleString("hu-HU")} Ft / óra</strong>
            </div>

            <div>
                <span>E-mail</span>
                <strong>${escapeHtml(tutor.email || "Nincs megadva")}</strong>
            </div>
        </div>

        <h3>Rólam</h3>
        <p class="profile-bio">${escapeHtml(bio)}</p>

        <h3>Vélemények</h3>
        <div class="profile-reviews">${reviewsHtml}</div>
    `;
}
