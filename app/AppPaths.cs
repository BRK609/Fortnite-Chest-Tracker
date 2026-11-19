
using Microsoft.Data.Sqlite;

namespace app;

internal static class AppPaths
{
    public static readonly string RootFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteChestTracker-GitHub");

    public static readonly string DataFolder = Path.Combine(RootFolder, "data");
    public static readonly string BackupFolder = Path.Combine(RootFolder, "backups", "replays");
    public static readonly string DatabasePath = Path.Combine(DataFolder, "statistics.db");
    public static readonly string ProfilePath = Path.Combine(DataFolder, "player_profile.json");

    public static void Initialize()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(BackupFolder);

        using var connection = new SqliteConnection(
            $"Data Source={DatabasePath}");

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS replays (
                sha256 TEXT PRIMARY KEY,
                filename TEXT NOT NULL,
                file_path TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending'
                    CHECK(status IN ('pending', 'analyzed', 'error')),
                chests_opened INTEGER NOT NULL DEFAULT 0,
                chests_destroyed INTEGER NOT NULL DEFAULT 0,
                first_seen TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                analyzed_at TEXT,
                error_message TEXT
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;

        command.ExecuteNonQuery();


        // Migration : ajouter match_date si elle n'existe pas encore.
        bool hasMatchDate = false;

        using (var check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA table_info(replays);";

            using var reader = check.ExecuteReader();

            while (reader.Read())
            {
                if (reader.GetString(1) == "match_date")
                {
                    hasMatchDate = true;
                    break;
                }
            }
        }

        if (!hasMatchDate)
        {
            using var migration = connection.CreateCommand();
            migration.CommandText =
                "ALTER TABLE replays ADD COLUMN match_date TEXT;";
            migration.ExecuteNonQuery();
        }

    }
}
