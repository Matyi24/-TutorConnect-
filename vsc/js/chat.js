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

// Melyik beszélgetés és melyik legnagyobb ID-jú üzenet lett utoljára
// kirajzolva. Ebből tudjuk, mely üzenetek ÚJAK (azok kapnak animációt).
let lastRenderedConversationId = null;
let lastRenderedMessageId = 0;

// A legutóbb kirajzolt üzenetek (a szerkesztéshez kell az eredeti szöveg)
let currentMessages = [];

// Szerkesztés alatt álló üzenet és a begépelt, még el nem mentett szöveg.
// Az üzenetek újrarajzolásakor (pl. új üzenet érkezik) is megmaradnak.
let editingMessageId = null;
let editingDraft = "";

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

        // 4/c. Telefonos nézet: vissza gomb a beszélgetéslistához
        setupMobileNav();

        // 4/d. Saját üzenet visszavonása
        setupMessageRetract();

        // 4/e. Saját üzenet szerkesztése
        setupMessageEdit();

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
    // automatikusan megnyitjuk az elsőt. Telefonon nem: ott a lista
    // látszik először, és a felhasználó választ belőle.
    if (conversations.length > 0 && !isMobileLayout()) {

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
        Number(conversation.last_message_retracted)
            ? "Az üzenet vissza lett vonva"
            : conversation.last_message ||
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

            showChatView();
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

        editingMessageId = null;
        editingDraft = "";
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


    // Csak akkor animálunk, ha ugyanazt a beszélgetést frissítjük
    // (új üzenet érkezett/elmentve), beszélgetésváltáskor nem.
    const animateNew =
        Number(lastRenderedConversationId) ===
        Number(currentConversationId);

    currentMessages = messages;


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


            if (
                animateNew &&
                Number(message.id) > lastRenderedMessageId
            ) {

                group.classList.add("message-new");
            }


            const time =
                formatTime(
                    message.created_at
                );


            const isRetracted = Boolean(message.deleted_at);

            // Éppen szerkesztjük-e ezt az üzenetet?
            const isEditing =
                isOwnMessage &&
                !isRetracted &&
                Number(message.id) === Number(editingMessageId);

            // Csak a saját, még meglévő üzenet vonható vissza
            const retractButton =
                isOwnMessage && !isRetracted && !isEditing
                    ? `<button type="button" class="retract-button"
                            data-message-id="${escapeAttribute(message.id)}"
                            title="Üzenet visszavonása"
                            aria-label="Üzenet visszavonása">↩</button>`
                    : "";

            // Csak a saját, szöveges (csatolmány nélküli) üzenet szerkeszthető
            const editButton =
                isOwnMessage && !isRetracted && !isEditing &&
                !message.attachment_name
                    ? `<button type="button" class="edit-button"
                            data-message-id="${escapeAttribute(message.id)}"
                            title="Üzenet szerkesztése"
                            aria-label="Üzenet szerkesztése">✎</button>`
                    : "";

            const editedLabel =
                message.edited_at && !isRetracted
                    ? " · szerkesztve"
                    : "";

            let bubble;

            if (isRetracted) {

                bubble = `<div class="message retracted">Az üzenet vissza lett vonva</div>`;

            } else if (isEditing) {

                bubble = `
                    <div class="message editing">
                        <input type="text" class="edit-input" maxlength="2000"
                               value="${escapeAttribute(editingDraft)}"
                               aria-label="Üzenet szerkesztése">
                        <div class="edit-actions">
                            <button type="button" class="edit-cancel">Mégse</button>
                            <button type="button" class="edit-save">Mentés</button>
                        </div>
                    </div>`;

            } else {

                bubble = `<div class="message${
                        message.attachment_name ? " has-attachment" : ""
                    }">
                        ${buildMessageContent(message)}
                    </div>`;
            }

            group.innerHTML = `

                <span class="message-time">
                    ${escapeHtml(time + editedLabel)}
                </span>

                <div class="message-line">
                    <span class="message-actions">
                        ${editButton}
                        ${retractButton}
                    </span>
                    ${bubble}
                </div>

            `;


            messagesContainer.appendChild(
                group
            );
        }
    );


    // Szerkesztés közben a beviteli mező kapja vissza a fókuszt
    // (az újrarajzolás ezt elvenné)
    const editInput = messagesContainer.querySelector(".edit-input");

    if (editInput) {

        editInput.focus();

        editInput.setSelectionRange(
            editInput.value.length,
            editInput.value.length
        );
    }

    lastRenderedConversationId = currentConversationId;

    lastRenderedMessageId = messages.reduce(
        (max, message) => Math.max(max, Number(message.id) || 0),
        0
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
                conversation.last_message_retracted = 0;
                conversation.last_message_time = savedMessage.created_at;
                renderConversations();
            }

        } catch (error) {

            console.error("❌ Üzenetküldési hiba:", error);

            // A hibát (pl. a másik fél törölte a fiókját) a felhasználó is látja
            showAttachmentError(error.message);

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

        if (
            data.type === "message_retracted" ||
            data.type === "message_edited"
        ) {
            await handleMessageUpdated(data);
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
        conversation.last_message_retracted = 0;
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

    } else if (!currentConversationId && !isMobileLayout()) {

        // Még nem volt megnyitva egyetlen beszélgetés sem
        // (telefonon a lista marad, az olvasatlan-jelvény jelzi az újat)
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

    showChatView();

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

    let closing = false;

    function closePicker() {

        if (closing) {
            return;
        }

        closing = true;

        document.removeEventListener("keydown", onKeyDown);

        // A kifutó animáció után töröljük az elemet (lásd chat.css)
        overlay.classList.add("closing");

        setTimeout(() => overlay.remove(), 180);
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

    // A lépcsőzetes belépés csak az első kirajzolásnál fusson, különben
    // minden betűnél újra animálódna a lista
    let animateList = true;

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

        list.classList.toggle("animate-in", animateList);
        animateList = false;

        if (filtered.length === 0) {
            list.innerHTML = `<p class="tutor-picker-empty">Nincs találat.</p>`;
            return;
        }

        filtered.forEach((tutor, index) => {

            const item = document.createElement("div");
            item.className = "tutor-picker-item";
            item.style.setProperty("--i", index);

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


// A másik fél diákja? (oktatóként beszélgetünk vele)
function getOtherStudentId() {

    if (!currentUser || currentUser.role !== "TUTOR") {
        return null;
    }

    const conversation = findConversation(currentConversationId);

    return conversation ? conversation.student_id : null;
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

    } else if (currentConversationId && getOtherStudentId()) {

        // Oktatóként a diák profilját nézhetjük meg
        const item = document.createElement("button");
        item.type = "button";
        item.className = "chat-menu-item";
        item.textContent = "Diák profilja";

        item.addEventListener("click", () => {
            closeChatMenu();
            openStudentProfile(getOtherStudentId());
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


// Az /api/tutors "id|név|ár;;id|név|ár" formában adja a tantárgyankénti
// árakat -> [{ id, name, hourly_rate }]
function parseSubjectPrices(value) {

    if (!value) {
        return [];
    }

    return String(value)
        .split(";;")
        .map((item) => {

            const [id, name, rate] = item.split("|");

            return {
                id: Number(id),
                name: name || "",
                hourly_rate: Number(rate) || 0
            };
        });
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
    const rating = Number(tutor.average_rating) || 0;
    const reviewCount = Array.isArray(reviews) ? reviews.length : 0;

    // Az óradíj tantárgyanként más lehet: tartományt és listát mutatunk
    const prices = parseSubjectPrices(tutor.subject_prices);

    let priceSummary = "Nincs megadva";

    if (prices.length > 0) {

        const rates = prices.map((item) => item.hourly_rate);
        const min = Math.min(...rates);
        const max = Math.max(...rates);

        priceSummary = min === max
            ? `${min.toLocaleString("hu-HU")} Ft / óra`
            : `${min.toLocaleString("hu-HU")} – ${max.toLocaleString("hu-HU")} Ft / óra`;
    }

    const pricesHtml = prices.length === 0
        ? ""
        : `
            <h3>Tantárgyak és óradíjak</h3>
            <ul class="profile-prices">
                ${prices.map((item) => `
                    <li>
                        <span>${escapeHtml(item.name)}</span>
                        <strong>${item.hourly_rate.toLocaleString("hu-HU")} Ft / óra</strong>
                    </li>
                `).join("")}
            </ul>
        `;

    const reviewsHtml = reviewCount === 0
        ? `<p class="profile-no-reviews">Még nincs értékelés.</p>`
        : reviews.map((review, index) => {

            const date = new Date(review.created_at);

            const dateText = Number.isNaN(date.getTime())
                ? ""
                : date.toLocaleDateString("hu-HU");

            return `
                <article class="profile-review" style="--i:${index}">
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
                <strong>${escapeHtml(priceSummary)}</strong>
            </div>

            <div>
                <span>E-mail</span>
                <strong>${escapeHtml(tutor.email || "Nincs megadva")}</strong>
            </div>
        </div>

        <div id="chatBookingSlot"></div>

        ${pricesHtml}

        <h3>Rólam</h3>
        <p class="profile-bio">${escapeHtml(bio)}</p>

        <h3>Vélemények</h3>
        <div class="profile-reviews">${reviewsHtml}</div>
    `;

    // Időpont foglalása gomb (a diák az oktató profiljából is foglalhat)
    TCBooking.renderButton(
        content.querySelector("#chatBookingSlot"),
        tutorId,
        name
    );
}


// ============================================================
// TELEFONOS NÉZET (lista <-> chat)
// ============================================================
//
// Keskeny képernyőn (<= 700px) egyszerre csak az egyik panel látszik:
// a beszélgetések listája VAGY a megnyitott chat. A váltást a
// .chat-container "show-chat" osztálya vezérli (lásd chat.css).
// Széles képernyőn ennek nincs hatása, mindkét panel látszik.

function isMobileLayout() {

    return window.matchMedia("(max-width: 700px)").matches;
}


function showChatView() {

    const container = document.querySelector(".chat-container");

    if (container) {
        container.classList.add("show-chat");
    }
}


function showListView() {

    const container = document.querySelector(".chat-container");

    if (container) {
        container.classList.remove("show-chat");
    }

    // A lista nézetben nincs "megnyitott" beszélgetés: az új üzenet
    // olvasatlan marad, és a jelvény megjelenik a listában.
    hideTypingIndicator();
    currentConversationId = null;
    renderConversations();
}


function setupMobileNav() {

    const header = document.querySelector(".chat-header");

    if (!header) {
        return;
    }

    const back = document.createElement("button");
    back.type = "button";
    back.className = "chat-back";
    back.setAttribute("aria-label", "Vissza a beszélgetésekhez");
    back.textContent = "←";

    back.addEventListener("click", showListView);

    header.insertBefore(back, header.firstChild);
}


// ============================================================
// ÜZENET VISSZAVONÁSA
// ============================================================
//
// A saját üzenet mellett megjelenik egy ↩ gomb (egérrel a buborék
// fölé víve, érintőképernyőn mindig). Az első kattintás megerősítést
// kér ("Visszavonás?"), a második elküldi a kérést. A másik félnek a
// websocket jelzi a változást.

function setupMessageRetract() {

    const container = document.querySelector(".messages");

    if (!container) {
        return;
    }

    container.addEventListener("click", async (event) => {

        const button = event.target.closest(".retract-button");

        if (!button) {
            return;
        }

        // 1. kattintás: megerősítés kérése (3 mp-ig)
        if (!button.classList.contains("confirm")) {

            button.classList.add("confirm");
            button.textContent = "Visszavonás?";

            clearTimeout(button.confirmTimer);

            button.confirmTimer = setTimeout(() => {
                button.classList.remove("confirm");
                button.textContent = "↩";
            }, 3000);

            return;
        }

        // 2. kattintás: visszavonás
        clearTimeout(button.confirmTimer);

        button.disabled = true;

        try {

            const response = await fetch(
                `/api/messages/${encodeURIComponent(button.dataset.messageId)}`,
                { method: "DELETE" }
            );

            if (!response.ok) {

                const errorData = await response.json().catch(() => ({}));

                throw new Error(
                    errorData.error || "Nem sikerült visszavonni az üzenetet."
                );
            }

            await loadMessages(currentConversationId);
            await refreshConversations();

        } catch (error) {

            console.error("❌ Visszavonási hiba:", error);

            button.disabled = false;
            button.classList.remove("confirm");
            button.textContent = "↩";
        }
    });
}


// A másik fél (vagy a saját másik fülünk) visszavont vagy szerkesztett
// egy üzenetet
async function handleMessageUpdated(data) {

    if (
        Number(data.conversation_id) === Number(currentConversationId)
    ) {
        await loadMessages(currentConversationId);
    }

    await refreshConversations();
}


// ============================================================
// ÜZENET SZERKESZTÉSE
// ============================================================
//
// A ✎ gomb a buborékot szerkesztővé alakítja (beviteli mező, Mentés és
// Mégse). Enter ment, Esc megszakít. A begépelt szöveget az editingDraft
// őrzi, így egy közben érkező új üzenet sem törli el.

function startEditingMessage(messageId) {

    const message = currentMessages.find(
        (item) => Number(item.id) === Number(messageId)
    );

    if (!message) {
        return;
    }

    editingMessageId = message.id;
    editingDraft = message.content || "";

    renderMessages(currentMessages);
}


function cancelEditingMessage() {

    editingMessageId = null;
    editingDraft = "";

    renderMessages(currentMessages);
}


async function saveEditedMessage() {

    const content = editingDraft.trim();

    const message = currentMessages.find(
        (item) => Number(item.id) === Number(editingMessageId)
    );

    if (!message || !content) {
        return;
    }

    // Nem változott a szöveg: nincs mit menteni
    if (content === message.content) {
        cancelEditingMessage();
        return;
    }

    const messageId = editingMessageId;

    try {

        const response = await fetch(
            `/api/messages/${encodeURIComponent(messageId)}`,
            {
                method: "PATCH",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ content })
            }
        );

        if (!response.ok) {

            const errorData = await response.json().catch(() => ({}));

            throw new Error(
                errorData.error || "Nem sikerült menteni a szerkesztést."
            );
        }

        editingMessageId = null;
        editingDraft = "";

        await loadMessages(currentConversationId);
        await refreshConversations();

    } catch (error) {

        // A szerkesztő nyitva marad, a szöveg nem vész el
        console.error("❌ Szerkesztési hiba:", error);
    }
}


function setupMessageEdit() {

    const container = document.querySelector(".messages");

    if (!container) {
        return;
    }

    container.addEventListener("click", (event) => {

        const editButton = event.target.closest(".edit-button");

        if (editButton) {
            startEditingMessage(editButton.dataset.messageId);
            return;
        }

        if (event.target.closest(".edit-save")) {
            saveEditedMessage();
            return;
        }

        if (event.target.closest(".edit-cancel")) {
            cancelEditingMessage();
        }
    });

    container.addEventListener("input", (event) => {

        if (event.target.classList.contains("edit-input")) {
            editingDraft = event.target.value;
        }
    });

    container.addEventListener("keydown", (event) => {

        if (!event.target.classList.contains("edit-input")) {
            return;
        }

        if (event.key === "Enter") {
            event.preventDefault();
            saveEditedMessage();
        }

        if (event.key === "Escape") {
            cancelEditingMessage();
        }
    });
}


// ============================================================
// DIÁK PROFIL (oktatóknak)
// ============================================================
//
// Az oktató a ⋮ menüből megnézheti, kivel beszélget: iskolai szint,
// osztály és hogy miből kér segítséget. E-mail cím nem jelenik meg.

async function openStudentProfile(studentId) {

    const modal = ensureProfileModal();
    const content = modal.querySelector(".profile-content");

    content.innerHTML = `<p class="profile-loading">Betöltés...</p>`;

    modal.classList.add("active");
    modal.setAttribute("aria-hidden", "false");

    let student;

    try {

        const response = await fetch(
            `/api/students/${encodeURIComponent(studentId)}/profile`
        );

        if (!response.ok) {
            throw new Error("Nem sikerült lekérni a diák profilját.");
        }

        student = await response.json();

    } catch (error) {

        console.error("❌ Diák profil betöltési hiba:", error);

        content.innerHTML = `
            <p class="profile-loading">Nem sikerült betölteni a profilt.</p>
        `;

        return;
    }

    const name = student.name || "Ismeretlen diák";

    const summary = [student.school_level, student.grade]
        .filter(Boolean)
        .join(" · ");

    const subjectsHtml = student.subjects.length === 0
        ? `<p class="profile-no-reviews">A diák még nem adott meg tantárgyat.</p>`
        : `<div class="profile-chips">${
            student.subjects.map((subject) =>
                `<span class="profile-chip">${escapeHtml(subject)}</span>`
            ).join("")
        }</div>`;

    content.innerHTML = `
        <div class="profile-head">
            <div class="avatar large">${escapeHtml(getInitials(name))}</div>

            <div>
                <span class="profile-label">DIÁK PROFIL</span>
                <h2>${escapeHtml(name)}</h2>
                <p class="profile-subjects">${escapeHtml(summary || "Nincs megadott adat")}</p>
            </div>
        </div>

        <div class="profile-facts">
            <div>
                <span>Iskolai szint</span>
                <strong>${escapeHtml(student.school_level || "Nincs megadva")}</strong>
            </div>

            <div>
                <span>Osztály / évfolyam</span>
                <strong>${escapeHtml(student.grade || "Nincs megadva")}</strong>
            </div>
        </div>

        <h3>Ebből kér segítséget</h3>
        ${subjectsHtml}
    `;
}
