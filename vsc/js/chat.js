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

            <p>
                ${escapeHtml(lastMessage)}
            </p>

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