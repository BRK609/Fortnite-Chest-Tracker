
// =====================================================
// FORTNITE CHEST TRACKER - JAVASCRIPT
// =====================================================

// Navigation entre les pages
const navigationButtons = document.querySelectorAll("[data-page]");
const pages = document.querySelectorAll(".page");

const pageTitles = {
    dashboard: "Vue d'ensemble",
    history: "Historique",
    statistics: "Statistiques",
    profile: "Mon profil"
};

function navigateTo(pageName) {
    if (!pageTitles[pageName]) return;

    pages.forEach(page => {
        page.classList.toggle("active", page.id === pageName);
    });

    document.querySelectorAll(".nav-item").forEach(button => {
        button.classList.toggle(
            "active",
            button.dataset.page === pageName
        );
    });

    document.getElementById("page-title").textContent =
        pageTitles[pageName];

    document.getElementById("breadcrumb").textContent =
        pageTitles[pageName].toUpperCase();
}

navigationButtons.forEach(button => {
    button.addEventListener("click", () => {
        navigateTo(button.dataset.page);
    });
});

// =====================================================
// DONNÉES DE L'APPLICATION
// =====================================================

function updateSidebarTracker(running) {
    const status = document.getElementById("sidebar-tracker-status");
    const dot = document.querySelector(".status-dot");

    if (status) {
        status.textContent = running ? "Tracker actif" : "Tracker inactif";
    }

    if (dot) {
        dot.classList.toggle("active", running);
    }
}

function updateDashboard(data) {
    if (!data) return;

    setText("opened", formatNumber(data.opened));
    setText("destroyed", formatNumber(data.destroyed));
    setText("total", formatNumber(data.opened + data.destroyed));
    setText("games", formatNumber(data.games));

    // Profil Fortnite
    const username = data.playerName || "Aucun joueur sélectionné";
    const epicId = data.epicId || "Epic ID indisponible";
    const profileSaved = data.profileSaved === true;

    setText("player-name", username);
    setText("profile-username", username);
    setText("profile-epic-id", "Epic ID : " + epicId);
    setText(
        "profile-current-player",
        data.playerName && data.playerName !== "Aucun joueur sélectionné"
            ? "Sélectionné"
            : "Non configuré"
    );
    setText(
        "profile-save-status",
        profileSaved ? "Enregistré" : "Non enregistré"
    );
    setText(
        "profile-status",
        profileSaved ? "Profil configuré" : "Profil non configuré"
    );

    if (data.watcherRunning !== undefined) {
        setText(
            "tracker-status",
            data.watcherRunning ? "Actif" : "Inactif"
        );

        updateWatcherButton(data.watcherRunning);
        updateSidebarTracker(data.watcherRunning);
    }

    renderRecentActivity(data.recentActivity || []);
}


function renderRecentActivity(activities) {
    const container = document.getElementById("activity");

    if (!container) return;

    container.replaceChildren();

    if (!activities.length) {
        const empty = document.createElement("div");
        empty.className = "empty-state";

        const icon = document.createElement("div");
        icon.className = "empty-icon";
        icon.textContent = "⌁";

        const title = document.createElement("strong");
        title.textContent = "Aucune activité récente";

        const description = document.createElement("p");
        description.textContent = "Les nouvelles analyses apparaîtront ici.";

        empty.append(icon, title, description);
        container.appendChild(empty);
        return;
    }

    activities.forEach(activity => {
        const item = document.createElement("div");
        item.className = "activity-item";

        const main = document.createElement("div");
        main.className = "activity-main";

        const name = document.createElement("div");
        name.className = "activity-name";
        name.textContent = activity.filename || "Replay sans nom";

        const date = document.createElement("div");
        date.className = "activity-date";

        const parsedDate = new Date(
            (activity.date || "").endsWith("Z")
                ? activity.date
                : (activity.date || "").replace(" ", "T") + "Z"
        );

        date.textContent = Number.isNaN(parsedDate.getTime())
            ? activity.date || "Date inconnue"
            : new Intl.DateTimeFormat("fr-FR", {
                dateStyle: "short",
                timeStyle: "short"
            }).format(parsedDate);

        main.append(name, date);

        const stats = document.createElement("div");
        stats.className = "activity-stats";

        const opened = document.createElement("span");
        opened.className = "activity-stat";
        opened.textContent = `+${formatNumber(activity.opened)} ouverts`;

        const destroyed = document.createElement("span");
        destroyed.className = "activity-stat";
        destroyed.textContent = `${formatNumber(activity.destroyed)} détruits`;

        stats.append(opened, destroyed);
        item.append(main, stats);

        container.appendChild(item);
    });
}


function updateWatcherButton(running) {
    const button = document.getElementById("watcher-toggle");

    if (!button) return;

    button.innerHTML = running
        ? 'Arrêter la surveillance <span>■</span>'
        : 'Démarrer la surveillance <span>▶</span>';

    button.classList.toggle("watcher-active", running);
}

function toggleWatcher() {
    notifyHost("watcherToggle");
}

function setText(id, value) {
    const element = document.getElementById(id);

    if (element) {
        element.textContent = value ?? "—";
    }
}

function formatNumber(value) {
    return new Intl.NumberFormat("fr-FR").format(value ?? 0);
}

// =====================================================
// COMMUNICATION AVEC C# / WEBVIEW2
// =====================================================

// C# pourra envoyer des données à cette interface.
window.updateDashboard = updateDashboard;

// Prévenir C# lorsqu'une page est ouverte.
function notifyHost(type, data = {}) {
    if (window.chrome?.webview) {
        window.chrome.webview.postMessage({
            type,
            ...data
        });
    }
}

const watcherToggle = document.getElementById("watcher-toggle");

if (watcherToggle) {
    watcherToggle.addEventListener("click", toggleWatcher);
}

// Demander les données initiales lorsque WebView2 est prêt.
if (window.chrome?.webview) {
    window.chrome.webview.addEventListener("message", event => {
        const message = event.data;

        if (!message || typeof message !== "object") return;

        if (message.type === "dashboard") {
            updateDashboard(message.data);
        }

        if (message.type === "history") {
            renderHistory(message.data);
        }

        if (message.type === "statistics") {
            renderStatistics(message.data);
        }

        if (message.type === "showPlayerSelection") {
            showPlayerSelection(message.players);
        }

        if (message.type === "watcherState") {
            updateWatcherButton(message.running);
            updateSidebarTracker(message.running);

            setText(
                "tracker-status",
                message.running ? "Actif" : "Inactif"
            );
        }
    });

    notifyHost("ready");
}

// Signaler les changements de page à C#.
navigationButtons.forEach(button => {
    button.addEventListener("click", () => {
        notifyHost("navigate", {
            page: button.dataset.page
        });
    });
});

// Afficher la page d'accueil par défaut.
navigateTo("dashboard");

// =====================================================
// SÉLECTION DU JOUEUR
// =====================================================

// Bouton de changement de joueur depuis le profil
document.getElementById("profile-change-player")
    ?.addEventListener("click", () => {
        notifyHost("profileChangePlayer");
    });

let selectedPlayerIndex = null;

function showPlayerSelection(players) {
    const modal = document.getElementById("player-selection-modal");
    const list = document.getElementById("player-selection-list");
    const subtitle = document.getElementById("player-selection-subtitle");
    const confirmButton = document.getElementById("player-selection-confirm");

    if (!modal || !list || !confirmButton) return;

    selectedPlayerIndex = null;
    list.innerHTML = "";

    subtitle.textContent =
        `${players.length} joueurs détectés. Sélectionne ton pseudo.`;

    confirmButton.disabled = true;

    players.forEach(player => {
        const card = document.createElement("div");
        card.className = "player-option";

        const initials = (player.username || "?")
            .trim()
            .substring(0, 2)
            .toUpperCase();

        const avatar = document.createElement("div");
        avatar.className = "player-option-avatar";
        avatar.textContent = initials || "?";

        const info = document.createElement("div");
        info.className = "player-option-info";

        const name = document.createElement("div");
        name.className = "player-option-name";
        name.textContent = player.username || "Joueur inconnu";

        const epicId = document.createElement("div");
        epicId.className = "player-option-id";
        epicId.textContent = player.epicId || "Epic ID indisponible";

        const check = document.createElement("div");
        check.className = "player-option-check";
        check.textContent = "✓";

        info.append(name, epicId);
        card.append(avatar, info, check);

        card.addEventListener("click", () => {
            document.querySelectorAll(".player-option").forEach(option => {
                option.classList.remove("selected");
            });

            card.classList.add("selected");
            selectedPlayerIndex = player.index;
            confirmButton.disabled = false;
        });

        list.appendChild(card);
    });

    modal.classList.remove("hidden");
}

function closePlayerSelection() {
    document.getElementById("player-selection-modal")
        ?.classList.add("hidden");
}

document.getElementById("player-selection-confirm")
    ?.addEventListener("click", () => {
        if (selectedPlayerIndex === null) return;

        notifyHost("playerSelection", {
            index: selectedPlayerIndex
        });

        closePlayerSelection();
    });

document.getElementById("player-selection-cancel")
    ?.addEventListener("click", () => {
        notifyHost("playerSelectionCancel");
        closePlayerSelection();
    });



let historyCurrentPage = 1;
let historyData = null;

function renderHistory(data) {
    if (!data) return;

    historyData = data;

    const tbody = document.getElementById("history-table-body");
    if (!tbody) return;

    tbody.replaceChildren();

    const replays = data.replays || [];
    const pageSize = Number(
        document.getElementById("history-page-size")?.value || 10
    );

    const totalPages = Math.max(1, Math.ceil(replays.length / pageSize));

    historyCurrentPage = Math.min(historyCurrentPage, totalPages);

    const start = (historyCurrentPage - 1) * pageSize;
    const displayedReplays = replays.slice(start, start + pageSize);

    setText("history-count", `${formatNumber(data.games)} partie(s)`);
    setText("history-total", `${formatNumber(data.total)} coffre(s) au total`);

    setText(
        "history-page-info",
        `Page ${historyCurrentPage} sur ${totalPages}`
    );

    const previousButton = document.getElementById("history-prev");
    const nextButton = document.getElementById("history-next");

    if (previousButton) previousButton.disabled = historyCurrentPage <= 1;
    if (nextButton) nextButton.disabled = historyCurrentPage >= totalPages;

    if (replays.length === 0) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");

        cell.colSpan = 5;
        cell.className = "history-empty";
        cell.textContent = "Aucun replay trouvé.";

        row.appendChild(cell);
        tbody.appendChild(row);
        return;
    }

    displayedReplays.forEach(replay => {
        const row = document.createElement("tr");

        const filename = document.createElement("td");
        filename.textContent = replay.filename || "Replay inconnu";

        const date = document.createElement("td");

        if (replay.date && replay.date !== "Date inconnue") {
            const parsedDate = new Date(
                replay.date.endsWith("Z")
                    ? replay.date
                    : replay.date.replace(" ", "T") + "Z"
            );

            date.textContent = !isNaN(parsedDate.getTime())
                ? new Intl.DateTimeFormat("fr-FR", {
                    dateStyle: "short",
                    timeStyle: "short"
                }).format(parsedDate)
                : replay.date;
        } else {
            date.textContent = "Date inconnue";
        }

        const opened = document.createElement("td");
        opened.textContent = formatNumber(replay.opened);

        const destroyed = document.createElement("td");
        destroyed.textContent = formatNumber(replay.destroyed);

        const total = document.createElement("td");
        total.textContent = formatNumber(replay.total);
        total.className = "history-total";

        row.append(filename, date, opened, destroyed, total);
        tbody.appendChild(row);
    });
}

function requestHistory() {
    historyCurrentPage = 1;

    const search = document.getElementById("history-search")?.value || "";
    const dateFilter = document.getElementById("history-date-filter")?.value || "all";
    const sort = document.getElementById("history-sort")?.value || "recent";

    notifyHost("historyRequest", {
        search,
        dateFilter,
        sort
    });
}

document.getElementById("history-search")
    ?.addEventListener("input", requestHistory);

document.getElementById("history-date-filter")
    ?.addEventListener("change", requestHistory);

document.getElementById("history-sort")
    ?.addEventListener("change", requestHistory);

document.getElementById("history-page-size")
    ?.addEventListener("change", () => {
        historyCurrentPage = 1;
        renderHistory(historyData);
    });

document.getElementById("history-prev")
    ?.addEventListener("click", () => {
        if (historyCurrentPage > 1) {
            historyCurrentPage--;
            renderHistory(historyData);
        }
    });

document.getElementById("history-next")
    ?.addEventListener("click", () => {
        const pageSize = Number(
            document.getElementById("history-page-size")?.value || 10
        );

        const totalPages = Math.ceil(
            (historyData?.replays?.length || 0) / pageSize
        );

        if (historyCurrentPage < totalPages) {
            historyCurrentPage++;
            renderHistory(historyData);
        }
    });


function renderStatistics(data) {
    if (!data) return;

    const opened = Number(data.opened) || 0;
    const destroyed = Number(data.destroyed) || 0;
    const total = Number(data.total) || 0;
    const games = Number(data.games) || 0;
    const best = Number(data.best) || 0;
    const average = Number(data.average) || 0;

    setText("stats-opened", formatNumber(opened));
    setText("stats-destroyed", formatNumber(destroyed));
    setText("stats-total", formatNumber(total));
    setText("stats-games", formatNumber(games));
    setText("stats-average", average.toLocaleString("fr-FR", {
        maximumFractionDigits: 1
    }));
    setText("stats-best", formatNumber(best));

    const openedPercent = total > 0
        ? Math.round(opened / total * 100)
        : 0;

    const destroyedPercent = total > 0
        ? 100 - openedPercent
        : 0;

    setText("stats-opened-percent", `${openedPercent} %`);
    setText("stats-destroyed-percent", `${destroyedPercent} %`);

    const openedBar = document.getElementById("stats-opened-bar");
    const destroyedBar = document.getElementById("stats-destroyed-bar");

    if (openedBar) openedBar.style.width = `${openedPercent}%`;
    if (destroyedBar) destroyedBar.style.width = `${destroyedPercent}%`;

    const chart = document.getElementById("stats-chart");

    if (!chart) return;

    chart.replaceChildren();

    const activity = data.activity || [];

    if (activity.length === 0) {
        const empty = document.createElement("span");
        empty.className = "history-empty";
        empty.textContent = "Pas encore assez de données.";
        chart.appendChild(empty);
        return;
    }

    const maxValue = Math.max(
        1,
        ...activity.map(item => Number(item.total) || 0)
    );

    activity.slice().reverse().forEach((item, index) => {
        const value = Number(item.total) || 0;

        const bar = document.createElement("div");
        bar.className = "stats-chart-bar";

        const number = document.createElement("span");
        number.className = "stats-chart-value";
        number.textContent = formatNumber(value);

        const column = document.createElement("div");
        column.className = "stats-chart-column";
        column.style.height = `${Math.max(3, value / maxValue * 105)}px`;

        const label = document.createElement("span");
        label.className = "stats-chart-label";
        label.textContent = `${index + 1}`;

        bar.title = item.filename || `Partie ${index + 1}`;

        bar.append(number, column, label);
        chart.appendChild(bar);
    });
}
