
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using FortniteReplayReader.Models;

namespace app;

public partial class Form1 : Form
{
    // =========================================================
    // CONFIGURATION ET COULEURS
    // =========================================================

    private readonly Color background = Color.FromArgb(12, 13, 18);
    private readonly Color sidebar = Color.FromArgb(17, 18, 25);
    private readonly Color panel = Color.FromArgb(24, 25, 34);
    private readonly Color panelLight = Color.FromArgb(31, 32, 43);
    private readonly Color red = Color.FromArgb(225, 45, 55);
    private readonly Color muted = Color.FromArgb(145, 147, 163);
    private readonly Color green = Color.FromArgb(75, 205, 130);
    private readonly Color orange = Color.FromArgb(235, 170, 65);

    private string currentUsername = string.Empty;
    private string? currentEpicId;
    private bool hasSavedProfile = false;

    private static string ProfilePath => AppPaths.ProfilePath;
    private static string DatabasePath => AppPaths.DatabasePath;

    private const string AppVersion = "1.3";

    // =========================================================
    // INTERFACE
    // =========================================================

    private Panel contentPanel = null!;
    private Panel sidebarPanel = null!;
    private Label pageTitle = null!;
    private Label headerStatus = null!;

    private Label openedValue = null!;
    private Label destroyedValue = null!;
    private Label totalValue = null!;
    private Label gamesValue = null!;

    private Label watcherStatus = null!;
    private Label profileName = null!;
    private Button watcherButton = null!;

    private ListBox logList = null!;

    private DataGridView historyGrid = null!;
    private TextBox historySearch = null!;
    private ComboBox historyDateFilter = null!;
    private ComboBox historySort = null!;
    private Label historySummary = null!;
    private int hoveredHistoryRow = -1;

    private readonly List<string> activityHistory = new();
    private readonly List<Button> navigationButtons = new();

    private WatcherService? watcherService;
    private bool closeInProgress;
    private bool allowClose;

    private sealed class PlayerProfile
    {
        public string Username { get; set; } = "";
        public string? EpicId { get; set; }
    }

    // =========================================================
    // CONSTRUCTEUR
    // =========================================================

    public Form1()
    {
        InitializeComponent();

        AppPaths.Initialize();
        LoadPlayerProfile();

        Text = "Fortnite Chest Tracker";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1350, 850);
        MinimumSize = new Size(1050, 680);
        BackColor = background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.Sizable;
        DoubleBuffered = true;

        BuildInterface();
        ShowDashboard();

        AddLog("Interface graphique initialisée.");
        AddLog("Base de données : " + DatabasePath);
        AddLog("Profil actuel : " + currentUsername);

        FormClosing += Form1_FormClosing;
    }

    // =========================================================
    // CONSTRUCTION DE L'INTERFACE
    // =========================================================

    private void BuildInterface()
    {
        TableLayoutPanel root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = background,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        Controls.Add(root);

        // SIDEBAR

        sidebarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = sidebar,
            Margin = Padding.Empty
        };

        root.Controls.Add(sidebarPanel, 0, 0);

        Label logo = new Label
        {
            Text = "FORTNITE\nCHEST TRACKER",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            Location = new Point(22, 27),
            Size = new Size(190, 65)
        };

        sidebarPanel.Controls.Add(logo);

        Label subtitle = new Label
        {
            Text = "REPLAY ANALYTICS",
            ForeColor = red,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Location = new Point(24, 100),
            Size = new Size(180, 20)
        };

        sidebarPanel.Controls.Add(subtitle);

        Panel separator = new Panel
        {
            BackColor = Color.FromArgb(48, 48, 60),
            Location = new Point(20, 135),
            Size = new Size(185, 1)
        };

        sidebarPanel.Controls.Add(separator);

        AddNavigation("ACCUEIL", 165, ShowDashboard);
        AddNavigation("HISTORIQUE", 220, ShowHistory);
        AddNavigation("STATISTIQUES", 275, ShowStatistics);
        AddNavigation("MON PROFIL", 330, ShowProfile);
        AddNavigation("PARAMÈTRES", 385, ShowSettings);

        Panel bottomLine = new Panel
        {
            BackColor = Color.FromArgb(48, 48, 60),
            Location = new Point(20, 650),
            Size = new Size(185, 1),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        sidebarPanel.Controls.Add(bottomLine);

        Label version = new Label
        {
            Text = "FORTNITE CHEST TRACKER\nVersion " + AppVersion,
            ForeColor = muted,
            Font = new Font("Segoe UI", 8F),
            Location = new Point(22, 665),
            Size = new Size(190, 40),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        sidebarPanel.Controls.Add(version);

        // ZONE PRINCIPALE

        Panel main = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = background,
            Margin = Padding.Empty,
            Padding = new Padding(30, 20, 30, 20)
        };

        root.Controls.Add(main, 1, 0);

        TableLayoutPanel layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = background,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        main.Controls.Add(layout);

        Panel header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = background
        };

        pageTitle = new Label
        {
            Text = "Accueil",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 23F, FontStyle.Bold),
            Location = new Point(0, 5),
            AutoSize = true
        };

        header.Controls.Add(pageTitle);

        headerStatus = new Label
        {
            Text = "● SURVEILLANCE ARRÊTÉE",
            ForeColor = orange,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        header.Controls.Add(headerStatus);

        header.Resize += (_, _) =>
        {
            headerStatus.Location = new Point(
                Math.Max(0, header.ClientSize.Width - headerStatus.Width),
                20);
        };

        Panel headerLine = new Panel
        {
            BackColor = Color.FromArgb(42, 43, 54),
            Dock = DockStyle.Bottom,
            Height = 1
        };

        header.Controls.Add(headerLine);

        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = background,
            AutoScroll = true,
            Margin = Padding.Empty
        };

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(contentPanel, 0, 1);
    }

    // =========================================================
    // NAVIGATION
    // =========================================================

    private void AddNavigation(string text, int top, Action action)
    {
        Button button = new Button
        {
            Text = "   " + text,
            Location = new Point(12, top),
            Size = new Size(200, 44),
            FlatStyle = FlatStyle.Flat,
            BackColor = sidebar,
            ForeColor = Color.FromArgb(190, 192, 205),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Tag = text
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor =
            Color.FromArgb(42, 30, 38);

        button.Click += (_, _) =>
        {
            SetActiveNavigation(button);
            action();
        };

        navigationButtons.Add(button);
        sidebarPanel.Controls.Add(button);
    }

    private void SetActiveNavigation(Button selected)
    {
        foreach (Button button in navigationButtons)
        {
            bool active = button == selected;

            button.BackColor = active
                ? Color.FromArgb(65, 30, 39)
                : sidebar;

            button.ForeColor = active
                ? Color.White
                : Color.FromArgb(190, 192, 205);

            button.FlatAppearance.BorderSize = active ? 1 : 0;
            button.FlatAppearance.BorderColor = red;
        }
    }

    private void PreparePage(string title)
    {
        pageTitle.Text = title;
        contentPanel.Controls.Clear();
    }

    // =========================================================
    // OUTILS GRAPHIQUES
    // =========================================================

    private Label AddLabel(
        string text,
        int x,
        int y,
        int width,
        int height,
        float size,
        Color color,
        bool bold = false)
    {
        Label label = new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            ForeColor = color,
            Font = new Font(
                "Segoe UI",
                size,
                bold ? FontStyle.Bold : FontStyle.Regular)
        };

        contentPanel.Controls.Add(label);
        return label;
    }

    private Panel CreateCard(
        string title,
        string value,
        string description,
        int width,
        int height,
        out Label valueLabel)
    {
        Panel card = new Panel
        {
            Size = new Size(width, height),
            BackColor = panel,
            Margin = new Padding(0, 0, 18, 18)
        };

        Panel accent = new Panel
        {
            BackColor = red,
            Location = new Point(0, 0),
            Size = new Size(4, height)
        };

        card.Controls.Add(accent);

        Label heading = new Label
        {
            Text = title,
            Location = new Point(20, 17),
            Size = new Size(width - 35, 25),
            ForeColor = muted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };

        valueLabel = new Label
        {
            Text = value,
            Location = new Point(20, 48),
            Size = new Size(width - 35, 48),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 26F, FontStyle.Bold)
        };

        Label detail = new Label
        {
            Text = description,
            Location = new Point(20, height - 30),
            Size = new Size(width - 35, 20),
            ForeColor = muted,
            Font = new Font("Segoe UI", 8F)
        };

        card.Controls.Add(heading);
        card.Controls.Add(valueLabel);
        card.Controls.Add(detail);

        return card;
    }

    private void AddSectionTitle(string title, int y)
    {
        AddLabel(
            title.ToUpperInvariant(),
            5, y, 750, 25,
            10F, muted, true);
    }

    // =========================================================
    // ACCUEIL
    // =========================================================

    private void ShowDashboard()
    {
        PreparePage("Accueil");

        AddLabel(
            "APERÇU DE TON ACTIVITÉ",
            5, 5, 600, 25,
            10F, muted, true);

        FlowLayoutPanel cards = new FlowLayoutPanel
        {
            Location = new Point(5, 45),
            Size = new Size(1000, 155),
            BackColor = background,
            WrapContents = true,
            AutoScroll = false,
            FlowDirection = FlowDirection.LeftToRight
        };

        contentPanel.Controls.Add(cards);

        cards.Controls.Add(CreateCard(
            "COFFRES OUVERTS", "0",
            "Coffres ouverts au total",
            225, 135, out openedValue));

        cards.Controls.Add(CreateCard(
            "COFFRES DÉTRUITS", "0",
            "Coffres détruits au total",
            225, 135, out destroyedValue));

        cards.Controls.Add(CreateCard(
            "TOTAL DES COFFRES", "0",
            "Toutes parties confondues",
            225, 135, out totalValue));

        cards.Controls.Add(CreateCard(
            "PARTIES ANALYSÉES", "0",
            "Replays enregistrés",
            225, 135, out gamesValue));

        // SURVEILLANCE

        Panel watcherCard = new Panel
        {
            Location = new Point(5, 220),
            Size = new Size(930, 125),
            BackColor = panel,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        contentPanel.Controls.Add(watcherCard);

        Label watcherTitle = new Label
        {
            Text = "SURVEILLANCE DES REPLAYS",
            Location = new Point(22, 18),
            Size = new Size(450, 25),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };

        watcherCard.Controls.Add(watcherTitle);

        watcherStatus = new Label
        {
            Text = "● MOTEUR ARRÊTÉ",
            Location = new Point(22, 57),
            Size = new Size(450, 25),
            ForeColor = orange,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        watcherCard.Controls.Add(watcherStatus);

        watcherButton = new Button
        {
            Text = "DÉMARRER",
            Location = new Point(750, 38),
            Size = new Size(155, 45),
            BackColor = red,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        watcherButton.FlatAppearance.BorderSize = 0;
        watcherButton.Click += WatcherButton_Click;

        watcherCard.Controls.Add(watcherButton);

        // JOURNAL

        Panel logCard = new Panel
        {
            Location = new Point(5, 365),
            Size = new Size(930, 240),
            BackColor = panel,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        contentPanel.Controls.Add(logCard);

        Label logTitle = new Label
        {
            Text = "JOURNAL D'ACTIVITÉ",
            Location = new Point(20, 15),
            Size = new Size(400, 25),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        logCard.Controls.Add(logTitle);

        logList = new ListBox
        {
            Location = new Point(15, 50),
            Size = new Size(900, 175),
            BackColor = Color.FromArgb(17, 18, 24),
            ForeColor = Color.FromArgb(195, 197, 210),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9F),
            IntegralHeight = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                     AnchorStyles.Left | AnchorStyles.Right
        };

        logCard.Controls.Add(logList);

        foreach (string entry in activityHistory)
            logList.Items.Add(entry);

        if (logList.Items.Count > 0)
            logList.TopIndex = logList.Items.Count - 1;

        // PROFIL RAPIDE

        AddLabel(
            "PROFIL ACTUEL",
            5, 630, 300, 25,
            10F, muted, true);

        profileName = AddLabel(
            "Pseudo : " + currentUsername,
            5, 660, 650, 30,
            11F, Color.White, true);

        SetWatcherUi(watcherService?.IsRunning == true);
        RefreshStatistics();
    }

    // =========================================================
    // SURVEILLANCE DES REPLAYS
    // =========================================================

    private async void WatcherButton_Click(object? sender, EventArgs e)
    {
        if (watcherService?.IsRunning == true)
        {
            watcherButton.Enabled = false;

            try
            {
                await watcherService.StopAsync();
            }
            catch (Exception ex)
            {
                AddLog("[ERREUR ARRÊT] " + ex.Message);
            }
            finally
            {
                if (!IsDisposed && watcherButton != null)
                    watcherButton.Enabled = true;

                SetWatcherUi(false);
            }

            return;
        }

        try
        {
            watcherService ??= CreateWatcherService();

            watcherButton.Enabled = false;
            watcherService.Start();

            SetWatcherUi(true);
        }
        catch (Exception ex)
        {
            AddLog("[ERREUR DÉMARRAGE] " + ex.Message);

            SetWatcherUi(false);

            MessageBox.Show(
                "Impossible de démarrer la surveillance.\n\n" + ex.Message,
                "Fortnite Chest Tracker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed && watcherButton != null)
                watcherButton.Enabled = true;
        }
    }

    private WatcherService CreateWatcherService()
    {
        var service = new WatcherService(
            hasSavedProfile ? currentUsername : "",
            hasSavedProfile ? currentEpicId : null);

        service.Log += message =>
            RunOnUiThread(() => AddLog(message));

        service.StatisticsUpdated += () =>
            RunOnUiThread(() =>
            {
                RefreshStatistics();

                if (historyGrid != null && !historyGrid.IsDisposed)
                    LoadHistory(historySearch?.Text ?? "");
            });

        service.RunningChanged += running =>
            RunOnUiThread(() => SetWatcherUi(running));

        service.PlayerSelectionRequested += players =>
        {
            var completion =
                new TaskCompletionSource<WatcherService.PlayerSelection?>();

            RunOnUiThread(() =>
            {
                var selected = ShowPlayerSelectionDialog(players);

                if (selected != null)
                {
                    currentUsername = selected.Username;
                    currentEpicId = selected.EpicId;
                    hasSavedProfile = true;

                    SavePlayerProfile();

                    if (profileName != null && !profileName.IsDisposed)
                        profileName.Text = "Pseudo : " + currentUsername;
                }

                completion.TrySetResult(selected);
            });

            return completion.Task;
        };

        return service;
    }

    private void SetWatcherUi(bool running)
    {
        if (IsDisposed || Disposing)
            return;

        if (watcherStatus != null && !watcherStatus.IsDisposed)
        {
            watcherStatus.Text = running
                ? "● SURVEILLANCE ACTIVE"
                : "● MOTEUR ARRÊTÉ";

            watcherStatus.ForeColor = running ? green : orange;
        }

        if (headerStatus != null && !headerStatus.IsDisposed)
        {
            headerStatus.Text = running
                ? "● SURVEILLANCE ACTIVE"
                : "● SURVEILLANCE ARRÊTÉE";

            headerStatus.ForeColor = running ? green : orange;
        }

        if (watcherButton != null && !watcherButton.IsDisposed)
        {
            watcherButton.Text = running ? "ARRÊTER" : "DÉMARRER";
            watcherButton.BackColor = running
                ? Color.FromArgb(100, 40, 48)
                : red;
        }
    }

    // =========================================================
    // STATISTIQUES GLOBALES
    // =========================================================

    private void RefreshStatistics()
    {
        if (!File.Exists(DatabasePath))
            return;

        try
        {
            using var connection = new SqliteConnection(
                $"Data Source={DatabasePath}");

            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT
                    COALESCE(SUM(chests_opened), 0),
                    COALESCE(SUM(chests_destroyed), 0),
                    COUNT(*)
                FROM replays
                WHERE status = 'analyzed'
                """;

            using var reader = command.ExecuteReader();

            if (!reader.Read())
                return;

            long opened = Convert.ToInt64(reader.GetValue(0));
            long destroyed = Convert.ToInt64(reader.GetValue(1));
            long games = Convert.ToInt64(reader.GetValue(2));

            if (openedValue != null && !openedValue.IsDisposed)
                openedValue.Text = opened.ToString("N0");

            if (destroyedValue != null && !destroyedValue.IsDisposed)
                destroyedValue.Text = destroyed.ToString("N0");

            if (totalValue != null && !totalValue.IsDisposed)
                totalValue.Text = (opened + destroyed).ToString("N0");

            if (gamesValue != null && !gamesValue.IsDisposed)
                gamesValue.Text = games.ToString("N0");
        }
        catch (Exception ex)
        {
            AddLog("[ERREUR STATISTIQUES] " + ex.Message);
        }
    }

    // =========================================================
    // HISTORIQUE AVANCÉ
    // =========================================================


    private void ShowHistory()
    {
        PreparePage("Historique");

        AddLabel(
            "HISTORIQUE DES PARTIES",
            5, 3, 600, 25,
            10F, red, true);

        AddLabel(
            "RETROUVE ET ANALYSE CHACUNE DE TES PARTIES",
            5, 27, 700, 22,
            9F, muted);

        // PANNEAU DES FILTRES

        Panel toolbar = new Panel
        {
            Location = new Point(5, 62),
            Size = new Size(980, 95),
            BackColor = panel,
            Anchor = AnchorStyles.Top | AnchorStyles.Left |
                    AnchorStyles.Right
        };

        contentPanel.Controls.Add(toolbar);

        Panel accent = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(4, 95),
            BackColor = red
        };

        toolbar.Controls.Add(accent);

        Label searchTitle = new Label
        {
            Text = "RECHERCHER",
            Location = new Point(18, 12),
            Size = new Size(180, 18),
            ForeColor = muted,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold)
        };

        toolbar.Controls.Add(searchTitle);

        historySearch = new TextBox
        {
            Location = new Point(18, 35),
            Size = new Size(250, 32),
            BackColor = Color.FromArgb(13, 14, 20),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F)
        };

        historySearch.PlaceholderText = "Nom du replay...";
        toolbar.Controls.Add(historySearch);

        Label dateTitle = new Label
        {
            Text = "PÉRIODE",
            Location = new Point(285, 12),
            Size = new Size(150, 18),
            ForeColor = muted,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold)
        };

        toolbar.Controls.Add(dateTitle);

        historyDateFilter = new ComboBox
        {
            Location = new Point(285, 35),
            Size = new Size(150, 32),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(13, 14, 20),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F)
        };

        historyDateFilter.Items.AddRange(new object[]
        {
            "Toutes les dates",
            "7 derniers jours",
            "30 derniers jours"
        });

        historyDateFilter.SelectedIndex = 0;
        toolbar.Controls.Add(historyDateFilter);

        Label sortTitle = new Label
        {
            Text = "CLASSEMENT",
            Location = new Point(450, 12),
            Size = new Size(180, 18),
            ForeColor = muted,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold)
        };

        toolbar.Controls.Add(sortTitle);

        historySort = new ComboBox
        {
            Location = new Point(450, 35),
            Size = new Size(190, 32),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(13, 14, 20),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F)
        };

        historySort.Items.AddRange(new object[]
        {
            "Plus de coffres",
            "Moins de coffres",
            "Plus de coffres ouverts",
            "Plus de coffres détruits"
        });

        historySort.SelectedIndex = 0;
        toolbar.Controls.Add(historySort);

        Button exportButton = new Button
        {
            Text = "EXPORTER CSV",
            Location = new Point(660, 32),
            Size = new Size(145, 38),
            BackColor = red,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        exportButton.FlatAppearance.BorderSize = 0;
        exportButton.FlatAppearance.MouseOverBackColor =
            Color.FromArgb(255, 65, 75);

        exportButton.Click += (_, _) => ExportHistory();

        toolbar.Controls.Add(exportButton);

        // RÉSUMÉ DES STATISTIQUES

        Panel summaryPanel = new Panel
        {
            Location = new Point(5, 170),
            Size = new Size(980, 58),
            BackColor = Color.FromArgb(32, 21, 29),
            Anchor = AnchorStyles.Top | AnchorStyles.Left |
                    AnchorStyles.Right
        };

        contentPanel.Controls.Add(summaryPanel);

        Panel summaryAccent = new Panel
        {
            Dock = DockStyle.Left,
            Width = 4,
            BackColor = red
        };

        summaryPanel.Controls.Add(summaryAccent);

        historySummary = new Label
        {
            Text = "Chargement de l'historique...",
            Location = new Point(18, 17),
            Size = new Size(940, 25),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        summaryPanel.Controls.Add(historySummary);

        // TABLEAU

        historyGrid = new DataGridView
        {
            Location = new Point(5, 245),
            Size = new Size(980, 430),

            Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                    AnchorStyles.Left | AnchorStyles.Right,

            BackgroundColor = Color.FromArgb(12, 13, 18),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(45, 35, 44),
            BorderStyle = BorderStyle.None,

            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = true,

            ReadOnly = true,
            MultiSelect = false,
            RowHeadersVisible = false,

            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,

            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 48,
            RowTemplate = { Height = 46 },

            AllowUserToOrderColumns = true,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
        };

        // STYLE DE L'EN-TÊTE

        historyGrid.ColumnHeadersDefaultCellStyle.BackColor =
            Color.FromArgb(75, 24, 35);

        historyGrid.ColumnHeadersDefaultCellStyle.ForeColor =
            Color.White;

        historyGrid.ColumnHeadersDefaultCellStyle.Font =
            new Font("Segoe UI", 9F, FontStyle.Bold);

        historyGrid.ColumnHeadersDefaultCellStyle.Alignment =
            DataGridViewContentAlignment.MiddleCenter;

        historyGrid.ColumnHeadersDefaultCellStyle.Padding =
            new Padding(8, 0, 8, 0);

        // STYLE DES CELLULES

        historyGrid.DefaultCellStyle.BackColor =
            Color.FromArgb(22, 23, 31);

        historyGrid.DefaultCellStyle.ForeColor = Color.White;

        historyGrid.DefaultCellStyle.SelectionBackColor =
            Color.FromArgb(100, 30, 43);

        historyGrid.DefaultCellStyle.SelectionForeColor =
            Color.White;

        historyGrid.DefaultCellStyle.Font =
            new Font("Segoe UI", 9F);

        historyGrid.DefaultCellStyle.Padding =
            new Padding(8, 0, 8, 0);

        historyGrid.AlternatingRowsDefaultCellStyle.BackColor =
            Color.FromArgb(29, 29, 39);

        // COLONNES

        historyGrid.Columns.Add("sha256", "IDENTIFIANT");
        historyGrid.Columns.Add("filename", "NOM DU REPLAY");
        historyGrid.Columns.Add("date", "DATE D'ANALYSE");
        historyGrid.Columns.Add("opened", "OUVERTS");
        historyGrid.Columns.Add("destroyed", "DÉTRUITS");
        historyGrid.Columns.Add("total", "TOTAL");

        historyGrid.Columns["sha256"]!.Visible = false;

        historyGrid.Columns["filename"]!.FillWeight = 200;
        historyGrid.Columns["date"]!.FillWeight = 145;
        historyGrid.Columns["opened"]!.FillWeight = 75;
        historyGrid.Columns["destroyed"]!.FillWeight = 85;
        historyGrid.Columns["total"]!.FillWeight = 70;

        historyGrid.Columns["opened"]!.DefaultCellStyle.Alignment =
            DataGridViewContentAlignment.MiddleCenter;

        historyGrid.Columns["destroyed"]!.DefaultCellStyle.Alignment =
            DataGridViewContentAlignment.MiddleCenter;

        historyGrid.Columns["total"]!.DefaultCellStyle.Alignment =
            DataGridViewContentAlignment.MiddleCenter;

        historyGrid.Columns["date"]!.DefaultCellStyle.Alignment =
            DataGridViewContentAlignment.MiddleCenter;

        // COULEURS DES STATISTIQUES

        historyGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string column =
                historyGrid.Columns[e.ColumnIndex].Name;

            if (column == "opened")
            {
                e.CellStyle.ForeColor =
                    Color.FromArgb(90, 225, 145);

                e.CellStyle.Font =
                    new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            else if (column == "destroyed")
            {
                e.CellStyle.ForeColor =
                    Color.FromArgb(255, 85, 95);

                e.CellStyle.Font =
                    new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            else if (column == "total")
            {
                e.CellStyle.ForeColor =
                    Color.FromArgb(255, 190, 75);

                e.CellStyle.Font =
                    new Font("Segoe UI", 10F, FontStyle.Bold);
            }
        };

        // EFFET DE SURVOL

        historyGrid.CellMouseEnter += (_, e) =>
        {
            if (e.RowIndex < 0)
                return;

            hoveredHistoryRow = e.RowIndex;

            historyGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor =
                Color.FromArgb(65, 28, 40);
        };

        historyGrid.CellMouseLeave += (_, e) =>
        {
            if (e.RowIndex < 0 ||
                e.RowIndex >= historyGrid.Rows.Count)
                return;

            historyGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor =
                e.RowIndex % 2 == 0
                    ? Color.FromArgb(22, 23, 31)
                    : Color.FromArgb(29, 29, 39);

            hoveredHistoryRow = -1;
        };

        // DOUBLE-CLIC POUR LES DÉTAILS

        historyGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                ShowReplayDetails(e.RowIndex);
        };

        contentPanel.Controls.Add(historyGrid);

        // FILTRES

        historySearch.TextChanged += (_, _) =>
            LoadHistory(historySearch.Text);

        historyDateFilter.SelectedIndexChanged += (_, _) =>
            LoadHistory(historySearch.Text);

        historySort.SelectedIndexChanged += (_, _) =>
            LoadHistory(historySearch.Text);

        // CHARGEMENT

        LoadHistory();
    }



    private void LoadHistory(string search = "")
    {
        if (historyGrid == null || historyGrid.IsDisposed)
            return;

        historyGrid.Rows.Clear();

        try
        {
            using var connection = new SqliteConnection(
                $"Data Source={DatabasePath}");

            connection.Open();

            string dateCondition =
                historyDateFilter?.SelectedIndex switch
                {
                    1 => "AND datetime(COALESCE(match_date, analyzed_at, first_seen)) >= datetime('now', '-7 days')",
                    2 => "AND datetime(COALESCE(match_date, analyzed_at, first_seen)) >= datetime('now', '-30 days')",
                    _ => ""
                };

            string orderBy =
                historySort?.SelectedIndex switch
                {
                    0 => "ORDER BY (chests_opened + chests_destroyed) DESC",
                    1 => "ORDER BY (chests_opened + chests_destroyed) ASC",
                    2 => "ORDER BY chests_opened DESC",
                    3 => "ORDER BY chests_destroyed DESC",
                    _ => "ORDER BY (chests_opened + chests_destroyed) DESC"
                };

            string sql = $"""
                SELECT
                    sha256,
                    filename,
                    COALESCE(match_date, analyzed_at, first_seen),
                    chests_opened,
                    chests_destroyed
                FROM replays
                WHERE status = 'analyzed'
                AND ($search = '' OR filename LIKE $pattern)
                {dateCondition}
                {orderBy}
                """;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                command.Parameters.AddWithValue(
                    "$search", search.Trim());

                command.Parameters.AddWithValue(
                    "$pattern", "%" + search.Trim() + "%");

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string sha = reader.GetString(0);
                    string filename = reader.GetString(1);

                    string date = reader.IsDBNull(2)
                        ? "Date inconnue"
                        : reader.GetString(2);

                    long opened = Convert.ToInt64(reader.GetValue(3));
                    long destroyed = Convert.ToInt64(reader.GetValue(4));

                    string formattedDate = DateTime.TryParse(date, out DateTime parsedDate)
                        ? parsedDate.ToString("dd/MM/yyyy HH:mm:ss")
                        : date;

                    int index = historyGrid.Rows.Add(
                        sha,
                        filename,
                        formattedDate,
                        opened.ToString("N0"),
                        destroyed.ToString("N0"),
                        (opened + destroyed).ToString("N0"));

                    historyGrid.Rows[index].Height = 46;
                }
            }

            using var summaryCommand = connection.CreateCommand();

            summaryCommand.CommandText = $"""
                SELECT
                    COUNT(*),
                    COALESCE(SUM(chests_opened), 0),
                    COALESCE(SUM(chests_destroyed), 0)
                FROM replays
                WHERE status = 'analyzed'
                AND ($search = '' OR filename LIKE $pattern)
                {dateCondition}
                """;

            summaryCommand.Parameters.AddWithValue(
                "$search", search.Trim());

            summaryCommand.Parameters.AddWithValue(
                "$pattern", "%" + search.Trim() + "%");

            using var summaryReader = summaryCommand.ExecuteReader();

            if (summaryReader.Read())
            {
                long games = Convert.ToInt64(summaryReader.GetValue(0));
                long opened = Convert.ToInt64(summaryReader.GetValue(1));
                long destroyed = Convert.ToInt64(summaryReader.GetValue(2));
                long total = opened + destroyed;

                historySummary.Text =
                    $"  {games:N0} PARTIES     |     " +
                    $"{opened:N0} OUVERTS     |     " +
                    $"{destroyed:N0} DÉTRUITS     |     " +
                    $"{total:N0} COFFRES AU TOTAL";
            }

            historyGrid.ClearSelection();
        }
        catch (Exception ex)
        {
            historySummary.Text =
                "Erreur de chargement : " + ex.Message;

            AddLog("[ERREUR HISTORIQUE] " + ex.Message);
        }
    }


    // =========================================================
    // DÉTAILS D'UNE PARTIE
    // =========================================================

    private void ShowReplayDetails(int rowIndex)
    {
        if (historyGrid == null ||
            rowIndex < 0 ||
            rowIndex >= historyGrid.Rows.Count)
            return;

        string sha = Convert.ToString(
            historyGrid.Rows[rowIndex].Cells["sha256"].Value) ?? "";

        if (string.IsNullOrWhiteSpace(sha))
            return;

        try
        {
            using var connection = new SqliteConnection(
                $"Data Source={DatabasePath}");

            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT
                    filename,
                    file_path,
                    file_size,
                    status,
                    chests_opened,
                    chests_destroyed,
                    first_seen,
                    analyzed_at,
                    error_message
                FROM replays
                WHERE sha256 = $sha
                """;

            command.Parameters.AddWithValue("$sha", sha);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
                return;

            string filename = reader.GetString(0);
            string path = reader.GetString(1);
            long size = Convert.ToInt64(reader.GetValue(2));
            string status = reader.GetString(3);
            long opened = Convert.ToInt64(reader.GetValue(4));
            long destroyed = Convert.ToInt64(reader.GetValue(5));

            string firstSeen = reader.IsDBNull(6)
                ? "Inconnue"
                : reader.GetString(6);

            string analyzedAt = reader.IsDBNull(7)
                ? "Inconnue"
                : reader.GetString(7);

            string error = reader.IsDBNull(8)
                ? "Aucune"
                : reader.GetString(8);

            string details =
                $"REPLAY : {filename}\n\n" +
                $"Coffres ouverts : {opened:N0}\n" +
                $"Coffres détruits : {destroyed:N0}\n" +
                $"Total : {opened + destroyed:N0}\n\n" +
                $"Statut : {status}\n" +
                $"Taille : {FormatFileSize(size)}\n" +
                $"Première détection : {firstSeen}\n" +
                $"Analyse : {analyzedAt}\n\n" +
                $"Chemin : {path}\n\n" +
                $"Erreur : {error}";

            MessageBox.Show(
                details,
                "Détails de la partie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Impossible de charger les détails.\n\n" + ex.Message,
                "Fortnite Chest Tracker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] units = { "octets", "Ko", "Mo", "Go" };

        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    // =========================================================
    // EXPORT CSV
    // =========================================================

    private void ExportHistory()
    {
        if (historyGrid == null || historyGrid.Rows.Count == 0)
        {
            MessageBox.Show(
                "Il n'y a aucune partie à exporter.",
                "Export CSV",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        using SaveFileDialog dialog = new SaveFileDialog
        {
            Title = "Exporter l'historique",
            Filter = "Fichier CSV (*.csv)|*.csv",
            FileName = "FortniteChestTracker_Historique.csv"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            StringBuilder csv = new StringBuilder();

            csv.AppendLine(
                "Replay;Date d'analyse;Coffres ouverts;Coffres detruits;Total");

            foreach (DataGridViewRow row in historyGrid.Rows)
            {
                if (row.IsNewRow)
                    continue;

                string filename = Convert.ToString(
                    row.Cells["filename"].Value) ?? "";

                string date = Convert.ToString(
                    row.Cells["date"].Value) ?? "";

                string opened = Convert.ToString(
                    row.Cells["opened"].Value) ?? "0";

                string destroyed = Convert.ToString(
                    row.Cells["destroyed"].Value) ?? "0";

                string total = Convert.ToString(
                    row.Cells["total"].Value) ?? "0";

                csv.AppendLine(
                    $"{CsvEscape(filename)};" +
                    $"{CsvEscape(date)};" +
                    $"{CsvEscape(opened)};" +
                    $"{CsvEscape(destroyed)};" +
                    $"{CsvEscape(total)}");
            }

            File.WriteAllText(
                dialog.FileName,
                csv.ToString(),
                new UTF8Encoding(true));

            AddLog("[EXPORT] Historique exporté : " + dialog.FileName);

            MessageBox.Show(
                "L'historique a été exporté avec succès.",
                "Export terminé",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Erreur pendant l'export :\n\n" + ex.Message,
                "Export CSV",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private string CsvEscape(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // =========================================================
    // STATISTIQUES ET GRAPHIQUE
    // =========================================================

    private void ShowStatistics()
    {
        PreparePage("Statistiques");

        AddLabel(
            "ANALYSE DE TON ACTIVITÉ",
            5, 5, 650, 25,
            10F, muted, true);

        long opened = 0;
        long destroyed = 0;
        long games = 0;

        try
        {
            using var connection = new SqliteConnection(
                $"Data Source={DatabasePath}");

            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT
                    COALESCE(SUM(chests_opened), 0),
                    COALESCE(SUM(chests_destroyed), 0),
                    COUNT(*)
                FROM replays
                WHERE status = 'analyzed'
                """;

            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                opened = Convert.ToInt64(reader.GetValue(0));
                destroyed = Convert.ToInt64(reader.GetValue(1));
                games = Convert.ToInt64(reader.GetValue(2));
            }
        }
        catch (Exception ex)
        {
            AddLog("[ERREUR STATS] " + ex.Message);
        }

        long total = opened + destroyed;
        double average = games > 0 ? (double)total / games : 0;

        FlowLayoutPanel cards = new FlowLayoutPanel
        {
            Location = new Point(5, 45),
            Size = new Size(950, 150),
            BackColor = background,
            WrapContents = true
        };

        contentPanel.Controls.Add(cards);

        cards.Controls.Add(CreateCard(
            "PARTIES ANALYSÉES",
            games.ToString("N0"),
            "Historique complet",
            220, 130, out _));

        cards.Controls.Add(CreateCard(
            "TOTAL DES COFFRES",
            total.ToString("N0"),
            "Ouverts et détruits",
            220, 130, out _));

        cards.Controls.Add(CreateCard(
            "MOYENNE PAR PARTIE",
            average.ToString("0.0"),
            "Coffres par replay",
            220, 130, out _));

        cards.Controls.Add(CreateCard(
            "COFFRES OUVERTS",
            opened.ToString("N0"),
            "Total enregistré",
            220, 130, out _));

        AddLabel(
            "ACTIVITÉ DES 14 DERNIERS JOURS",
            5, 215, 600, 25,
            10F, muted, true);

        Panel chartPanel = new Panel
        {
            Location = new Point(5, 250),
            Size = new Size(950, 320),
            BackColor = panel,
            Anchor = AnchorStyles.Top | AnchorStyles.Left |
                     AnchorStyles.Right
        };

        contentPanel.Controls.Add(chartPanel);

        List<(string Date, long Total)> daily = LoadDailyStatistics();

        chartPanel.Paint += (_, e) =>
            DrawActivityChart(e.Graphics, chartPanel.ClientRectangle, daily);

        chartPanel.Invalidate();

        AddLabel(
            "Les données proviennent des replays analysés et enregistrés dans ta base locale.",
            5, 590, 900, 30,
            9F, muted);
    }

    private List<(string Date, long Total)> LoadDailyStatistics()
    {
        var results = new List<(string Date, long Total)>();

        try
        {
            using var connection = new SqliteConnection(
                $"Data Source={DatabasePath}");

            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT
                    date(COALESCE(analyzed_at, first_seen)) AS day,
                    SUM(chests_opened + chests_destroyed) AS total
                FROM replays
                WHERE status = 'analyzed'
                  AND datetime(COALESCE(analyzed_at, first_seen)) >= datetime('now', '-13 days')
                GROUP BY day
                ORDER BY day
                """;

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                results.Add((
                    reader.GetString(0),
                    Convert.ToInt64(reader.GetValue(1))));
            }
        }
        catch (Exception ex)
        {
            AddLog("[ERREUR GRAPHIQUE] " + ex.Message);
        }

        return results;
    }

    private void DrawActivityChart(
        Graphics graphics,
        Rectangle bounds,
        List<(string Date, long Total)> values)
    {
        graphics.Clear(panel);

        int left = 55;
        int right = 25;
        int top = 30;
        int bottom = 55;

        int chartWidth = bounds.Width - left - right;
        int chartHeight = bounds.Height - top - bottom;

        if (chartWidth <= 0 || chartHeight <= 0)
            return;

        using Pen gridPen = new Pen(
            Color.FromArgb(55, 56, 68), 1);

        using Brush textBrush = new SolidBrush(muted);
        using Brush barBrush = new SolidBrush(red);

        using Font smallFont = new Font("Segoe UI", 8F);

        long max = values.Count > 0
            ? Math.Max(1, values.Max(x => x.Total))
            : 1;

        for (int i = 0; i <= 4; i++)
        {
            int y = top + (chartHeight * i / 4);

            graphics.DrawLine(
                gridPen, left, y, left + chartWidth, y);

            long labelValue = max - (max * i / 4);

            graphics.DrawString(
                labelValue.ToString("N0"),
                smallFont,
                textBrush,
                5,
                y - 8);
        }

        if (values.Count == 0)
        {
            using Font emptyFont = new Font(
                "Segoe UI", 11F, FontStyle.Bold);

            graphics.DrawString(
                "Aucune activité enregistrée sur cette période.",
                emptyFont,
                textBrush,
                left + 20,
                top + chartHeight / 2);

            return;
        }

        int count = 14;
        float slotWidth = (float)chartWidth / count;
        float barWidth = Math.Max(8, slotWidth * 0.55F);

        DateTime today = DateTime.Today;
        var byDate = values
            .Select(x => (
                Date: DateTime.TryParse(x.Date, out DateTime d)
                    ? d.Date
                    : DateTime.MinValue,
                x.Total))
            .ToDictionary(x => x.Date, x => x.Total);

        for (int i = 0; i < count; i++)
        {
            DateTime date = today.AddDays(i - 13);

            long value = byDate.TryGetValue(date, out long found)
                ? found
                : 0;

            float height = (float)value / max * chartHeight;

            float x = left + i * slotWidth +
                      (slotWidth - barWidth) / 2;

            float y = top + chartHeight - height;

            if (value > 0)
            {
                graphics.FillRectangle(
                    barBrush,
                    x,
                    y,
                    barWidth,
                    Math.Max(3, height));
            }

            graphics.DrawString(
                date.ToString("dd/MM"),
                smallFont,
                textBrush,
                x - 5,
                top + chartHeight + 12);
        }
    }

    // =========================================================
    // PROFIL
    // =========================================================

    private WatcherService.PlayerSelection? ShowPlayerSelectionDialog(
        List<PlayerData> players)
    {
        using Form dialog = new Form
        {
            Text = "Sélection de ton joueur Fortnite",
            Size = new Size(620, 520),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = background,
            ForeColor = Color.White
        };

        Label title = new Label
        {
            Text = "QUI ES-TU DANS CE REPLAY ?",
            Location = new Point(20, 20),
            Size = new Size(560, 35),
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White
        };

        Label subtitle = new Label
        {
            Text = players.Count + " joueurs détectés. Sélectionne ton pseudo.",
            Location = new Point(20, 60),
            Size = new Size(560, 25),
            ForeColor = Color.Silver
        };

        ListBox list = new ListBox
        {
            Location = new Point(20, 100),
            Size = new Size(560, 300),
            BackColor = panel,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10)
        };

        foreach (var player in players)
        {
            string epicId = string.IsNullOrWhiteSpace(player.EpicId)
                ? "Epic ID indisponible"
                : player.EpicId;

            list.Items.Add($"{player.PlayerName}  |  {epicId}");
        }

        if (list.Items.Count > 0)
            list.SelectedIndex = 0;

        Button confirm = new Button
        {
            Text = "CONFIRMER",
            Location = new Point(340, 420),
            Size = new Size(150, 40),
            BackColor = red,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };

        Button cancel = new Button
        {
            Text = "ANNULER",
            Location = new Point(500, 420),
            Size = new Size(80, 40),
            BackColor = panelLight,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.AddRange(new Control[]
        {
            title, subtitle, list, confirm, cancel
        });

        dialog.AcceptButton = confirm;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK ||
            list.SelectedIndex < 0)
            return null;

        var selectedPlayer = players[list.SelectedIndex];

        return new WatcherService.PlayerSelection(
            selectedPlayer.PlayerName ?? "",
            selectedPlayer.EpicId);
    }

    private void LoadPlayerProfile()
    {
        try
        {
            if (!File.Exists(ProfilePath))
                return;

            var json = File.ReadAllText(ProfilePath);
            var saved = System.Text.Json.JsonSerializer.Deserialize<PlayerProfile>(json);

            if (saved == null)
                return;

            if (!string.IsNullOrWhiteSpace(saved.Username))
                currentUsername = saved.Username;

            currentEpicId = saved.EpicId;
            hasSavedProfile =
                !string.IsNullOrWhiteSpace(currentUsername) ||
                !string.IsNullOrWhiteSpace(currentEpicId);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Impossible de charger le profil : " + ex.Message,
                "Fortnite Chest Tracker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SavePlayerProfile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);

        var profile = new PlayerProfile
        {
            Username = currentUsername,
            EpicId = currentEpicId
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            profile,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(ProfilePath, json);
    }


    private void ShowProfile()
    {
        PreparePage("Mon profil");

        AddLabel(
            "PROFIL JOUEUR",
            5, 5, 500, 25,
            10F, muted, true);

        Panel profileCard = new Panel
        {
            Location = new Point(5, 50),
            Size = new Size(700, 210),
            BackColor = panel
        };

        contentPanel.Controls.Add(profileCard);

        Label username = new Label
        {
            Text = string.IsNullOrWhiteSpace(currentUsername)
                ? "Aucun joueur sélectionné"
                : currentUsername,
            Location = new Point(25, 35),
            Size = new Size(620, 35),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold)
        };

        profileCard.Controls.Add(username);

        Label description = new Label
        {
            Text = "Profil actuellement utilisé pour l'analyse des replays.",
            Location = new Point(25, 78),
            Size = new Size(620, 30),
            ForeColor = muted,
            Font = new Font("Segoe UI", 9F)
        };

        profileCard.Controls.Add(description);

        AddLabel(
            "La sélection automatique du joueur et la gestion de plusieurs profils seront ajoutées dans une prochaine mise à jour.",
            5, 285, 850, 45,
            9F, muted);
    }


    // =========================================================
    // PARAMÈTRES
    // =========================================================

    private void ShowSettings()
    {
        PreparePage("Paramètres");

        AddLabel(
            "CONFIGURATION DU LOGICIEL",
            5, 5, 600, 25,
            10F, muted, true);

        Panel databaseCard = new Panel
        {
            Location = new Point(5, 50),
            Size = new Size(750, 130),
            BackColor = panel
        };

        contentPanel.Controls.Add(databaseCard);

        Label databaseTitle = new Label
        {
            Text = "BASE DE DONNÉES",
            Location = new Point(20, 18),
            Size = new Size(350, 25),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        databaseCard.Controls.Add(databaseTitle);

        Label databasePath = new Label
        {
            Text = DatabasePath,
            Location = new Point(20, 55),
            Size = new Size(700, 25),
            ForeColor = muted,
            Font = new Font("Segoe UI", 9F)
        };

        databaseCard.Controls.Add(databasePath);

        Label databaseStatus = new Label
        {
            Text = File.Exists(DatabasePath)
                ? "● Base de données accessible"
                : "● Base de données introuvable",
            Location = new Point(20, 88),
            Size = new Size(500, 25),
            ForeColor = File.Exists(DatabasePath) ? green : orange,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        databaseCard.Controls.Add(databaseStatus);

        Panel replayCard = new Panel
        {
            Location = new Point(5, 200),
            Size = new Size(750, 130),
            BackColor = panel
        };

        contentPanel.Controls.Add(replayCard);

        Label replayTitle = new Label
        {
            Text = "DOSSIER DES REPLAYS",
            Location = new Point(20, 18),
            Size = new Size(350, 25),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        replayCard.Controls.Add(replayTitle);

        Label replayPath = new Label
        {
            Text = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "FortniteGame", "Saved", "Demos"),
            Location = new Point(20, 55),
            Size = new Size(700, 25),
            ForeColor = muted,
            Font = new Font("Segoe UI", 9F)
        };

        replayCard.Controls.Add(replayPath);

        AddLabel(
            "Les chemins seront personnalisables dans une prochaine mise à jour.",
            5, 355, 800, 30,
            9F, muted);
    }

    // =========================================================
    // JOURNAL D'ACTIVITÉ
    // =========================================================

    private void AddLog(string message)
    {
        if (IsDisposed || Disposing)
            return;

        if (InvokeRequired)
        {
            RunOnUiThread(() => AddLog(message));
            return;
        }

        string entry =
            $"[{DateTime.Now:HH:mm:ss}] {message}";

        activityHistory.Add(entry);

        while (activityHistory.Count > 300)
            activityHistory.RemoveAt(0);

        if (logList == null || logList.IsDisposed)
            return;

        logList.Items.Add(entry);

        while (logList.Items.Count > 300)
            logList.Items.RemoveAt(0);

        if (logList.Items.Count > 0)
            logList.TopIndex = logList.Items.Count - 1;
    }

    // =========================================================
    // THREAD UI
    // =========================================================

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
            return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // La fenêtre est en cours de fermeture.
            }

            return;
        }

        action();
    }

    // =========================================================
    // FERMETURE PROPRE
    // =========================================================

    private async void Form1_FormClosing(
        object? sender,
        FormClosingEventArgs e)
    {
        if (allowClose)
            return;

        if (closeInProgress)
        {
            e.Cancel = true;
            return;
        }

        if (watcherService == null)
        {
            allowClose = true;
            return;
        }

        e.Cancel = true;
        closeInProgress = true;
        Enabled = false;

        try
        {
            if (watcherService.IsRunning)
                await watcherService.StopAsync();

            watcherService.Dispose();
            watcherService = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Une erreur est survenue pendant l'arrêt de la surveillance :\n\n"
                + ex.Message,
                "Fortnite Chest Tracker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        allowClose = true;
        Close();
    }
}
