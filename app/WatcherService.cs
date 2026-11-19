
using ChestCounter;
using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using FortniteReplayReader.Models;

namespace app;

public sealed class WatcherService : IDisposable
{

    public sealed record PlayerSelection(string Username, string? EpicId);

    public event Func<List<PlayerData>, Task<PlayerSelection?>>? PlayerSelectionRequested;

    private readonly SemaphoreSlim _selectionLock = new(1, 1);

    private readonly string _sourceFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FortniteGame", "Saved", "Demos");

    private readonly string _backupFolder = AppPaths.BackupFolder;

    private readonly string _databasePath = AppPaths.DatabasePath;

    private string _username;
    private string? _epicId;

    private readonly ConcurrentDictionary<string, byte> _activeFiles =
        new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cancellation;
    private Task? _scanTask;
    private bool _disposed;

    public event Action<string>? Log;
    public event Action? StatisticsUpdated;
    public event Action<bool>? RunningChanged;

    public bool IsRunning { get; private set; }

    public WatcherService(string username)
        : this(username, null)
    {
    }

    public WatcherService(string username, string? epicId)
    {
        _username = username ?? string.Empty;
        _epicId = epicId;
    }

    public void Start()
    {
        if (IsRunning)
            return;

        if (!Directory.Exists(_sourceFolder))
            throw new DirectoryNotFoundException(
                "Dossier des replays introuvable : " + _sourceFolder);

        Directory.CreateDirectory(_backupFolder);

        if (!File.Exists(_databasePath))
            throw new FileNotFoundException(
                "Base de données introuvable.", _databasePath);

        _cancellation = new CancellationTokenSource();

        _watcher = new FileSystemWatcher(_sourceFolder, "*.replay")
        {
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.Size
                         | NotifyFilters.LastWrite,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnFileChanged;
        _watcher.Changed += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnWatcherError;
        _watcher.EnableRaisingEvents = true;

        IsRunning = true;
        RunningChanged?.Invoke(true);

        WriteLog("Surveillance activée.");
        WriteLog("Dossier : " + _sourceFolder);
        WriteLog("Joueur : " + _username);

        _scanTask = ScanLoopAsync(_cancellation.Token);
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        IsRunning = false;

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _cancellation?.Cancel();

        if (_scanTask != null)
        {
            try
            {
                await _scanTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        while (!_activeFiles.IsEmpty)
            await Task.Delay(100);

        _cancellation?.Dispose();
        _cancellation = null;
        _scanTask = null;

        WriteLog("Surveillance arrêtée.");
        RunningChanged?.Invoke(false);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        QueueReplay(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        QueueReplay(e.FullPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        WriteLog("[ERREUR SURVEILLANCE] " + e.GetException().Message);
    }

    private void QueueReplay(string path)
    {
        if (!IsRunning || _cancellation == null)
            return;

        if (!_activeFiles.TryAdd(path, 0))
            return;

        CancellationToken token = _cancellation.Token;

        _ = ProcessTrackedAsync(path, token);
    }

    private async Task ProcessTrackedAsync(string path, CancellationToken token)
    {
        try
        {
            await ProcessReplayAsync(path, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            WriteLog("[ERREUR] " + Path.GetFileName(path));
            WriteLog(ex.Message);
        }
        finally
        {
            _activeFiles.TryRemove(path, out _);
        }
    }

    private async Task ScanLoopAsync(CancellationToken token)
    {
        WriteLog("Vérification des replays existants...");

        while (!token.IsCancellationRequested)
        {
            try
            {
                foreach (string file in Directory.GetFiles(
                    _sourceFolder, "*.replay"))
                {
                    token.ThrowIfCancellationRequested();
                    QueueReplay(file);
                }

                await Task.Delay(TimeSpan.FromSeconds(30), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                WriteLog("[ERREUR SCAN] " + ex.Message);

                try
                {
                    await Task.Delay(5000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private bool AlreadyAnalyzed(string filename, long size)
    {
        using var connection = OpenDatabase();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM replays
            WHERE filename = $filename
              AND file_size = $size
              AND status = 'analyzed'
            """;

        command.Parameters.AddWithValue("$filename", filename);
        command.Parameters.AddWithValue("$size", size);

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private string CalculateHash(string path)
    {
        using var stream = File.OpenRead(path);

        return Convert.ToHexString(
            SHA256.HashData(stream)).ToLowerInvariant();
    }

    private async Task<bool> EnsurePlayerSelectedAsync(
        string replayPath, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(_username) ||
            !string.IsNullOrWhiteSpace(_epicId))
            return true;

        await _selectionLock.WaitAsync(token);

        try
        {
            // Un autre replay a peut-être déjà déclenché la sélection.
            if (!string.IsNullOrWhiteSpace(_username) ||
                !string.IsNullOrWhiteSpace(_epicId))
                return true;

            using var stream = File.OpenRead(replayPath);

            var players = new ReplayAnalyzer().GetPlayers(stream);

            if (players.Count == 0)
            {
                WriteLog("[ERREUR] Aucun joueur détecté dans le replay.");
                return false;
            }

            WriteLog($"[JOUEURS] {players.Count} joueurs détectés.");

            var handler = PlayerSelectionRequested;

            if (handler == null)
            {
                WriteLog("[ERREUR] Fenêtre de sélection non disponible.");
                return false;
            }

            var selection = await handler(players);

            if (selection == null ||
                string.IsNullOrWhiteSpace(selection.Username))
            {
                WriteLog("[ANNULÉ] Sélection du joueur annulée.");
                return false;
            }

            _username = selection.Username;
            _epicId = selection.EpicId;

            WriteLog("[PROFIL] Joueur sélectionné : " + _username);

            return true;
        }
        finally
        {
            _selectionLock.Release();
        }
    }

    private async Task ProcessReplayAsync(
        string sourceFile, CancellationToken token)
    {
        if (!File.Exists(sourceFile))
            return;

        string filename = Path.GetFileName(sourceFile);
        long initialSize = new FileInfo(sourceFile).Length;

        if (AlreadyAnalyzed(filename, initialSize))
        {
            WriteLog("[DÉJÀ ANALYSÉ] " + filename);
            return;
        }

        WriteLog("[DÉTECTÉ] " + filename);

        long previousSize = -1;
        int stableChecks = 0;

        for (int i = 0; i < 300; i++)
        {
            token.ThrowIfCancellationRequested();

            await Task.Delay(2000, token);

            if (!File.Exists(sourceFile))
                return;

            try
            {
                using var test = new FileStream(
                    sourceFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);

                long currentSize = test.Length;

                if (currentSize > 0 && currentSize == previousSize)
                    stableChecks++;
                else
                    stableChecks = 0;

                previousSize = currentSize;

                if (stableChecks >= 2)
                    break;
            }
            catch (IOException)
            {
                stableChecks = 0;
            }
        }

        token.ThrowIfCancellationRequested();

        if (stableChecks < 2)
        {
            WriteLog("[ATTENTE] Fichier non stabilisé : " + filename);
            return;
        }

        string temporaryFile = Path.Combine(
            _backupFolder, filename + ".tmp");

        try
        {
            using (var input = new FileStream(
                sourceFile, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var output = new FileStream(
                temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, token);
                output.Flush(true);
            }

            token.ThrowIfCancellationRequested();

            string hash = CalculateHash(temporaryFile);
            string destination = Path.Combine(_backupFolder, filename);

            if (File.Exists(destination))
            {
                if (CalculateHash(destination) == hash)
                {
                    File.Delete(temporaryFile);
                }
                else
                {
                    string stem = Path.GetFileNameWithoutExtension(filename);
                    string extension = Path.GetExtension(filename);

                    destination = Path.Combine(
                        _backupFolder,
                        stem + "_" + hash[..12] + extension);

                    if (File.Exists(destination))
                    {
                        if (CalculateHash(destination) != hash)
                            throw new IOException("Collision de nom de fichier.");

                        File.Delete(temporaryFile);
                    }
                    else
                    {
                        File.Move(temporaryFile, destination);
                    }
                }
            }
            else
            {
                File.Move(temporaryFile, destination);
            }

            token.ThrowIfCancellationRequested();

            long fileSize = new FileInfo(destination).Length;

            using var connection = OpenDatabase();

            using (var check = connection.CreateCommand())
            {
                check.CommandText =
                    "SELECT status FROM replays WHERE lower(sha256) = $hash";

                check.Parameters.AddWithValue("$hash", hash);

                var existing = check.ExecuteScalar()?.ToString();

                if (existing == "analyzed")
                {
                    WriteLog("[DOUBLON] " + filename);
                    return;
                }
            }

            using (var insert = connection.CreateCommand())
            {
                insert.CommandText = """
                    INSERT INTO replays
                    (sha256, filename, file_path, file_size, status)
                    VALUES ($hash, $filename, $path, $size, 'pending')
                    ON CONFLICT(sha256) DO UPDATE SET
                        status = 'pending',
                        error_message = NULL,
                        filename = excluded.filename,
                        file_path = excluded.file_path,
                        file_size = excluded.file_size
                    """;

                insert.Parameters.AddWithValue("$hash", hash);
                insert.Parameters.AddWithValue("$filename", filename);
                insert.Parameters.AddWithValue("$path", destination);
                insert.Parameters.AddWithValue("$size", fileSize);

                insert.ExecuteNonQuery();
            }

            token.ThrowIfCancellationRequested();

            WriteLog("[ANALYSE] " + filename);

            int opened;
            int destroyed;
            DateTime? matchDate;

            try
            {
                if (!await EnsurePlayerSelectedAsync(destination, token))
                {
                    SetError(connection, hash, "Sélection du joueur nécessaire.");
                    WriteLog("[NON ANALYSÉ] " + filename);
                    return;
                }

                using var replayStream = File.OpenRead(destination);

                var result = new ReplayAnalyzer().Analyze(
                    replayStream, destination, _username, _epicId);

                if (!result.TrackedByUsername)
                {
                    SetError(connection, hash, "Joueur non reconnu.");
                    WriteLog("[NON RECONNU] " + filename);
                    return;
                }

                opened = result.Chests.Count(c => !c.Destroyed);
                destroyed = result.Chests.Count(c => c.Destroyed);
                matchDate = result.MatchStartUtc;
            }
            catch (Exception ex)
            {
                SetError(connection, hash, ex.Message);
                throw;
            }

            token.ThrowIfCancellationRequested();

            using (var update = connection.CreateCommand())
            {
                update.CommandText = """
                    UPDATE replays
                    SET status = 'analyzed',
                        chests_opened = $opened,
                        chests_destroyed = $destroyed,
                        match_date = $matchDate,
                        analyzed_at = CURRENT_TIMESTAMP,
                        error_message = NULL
                    WHERE lower(sha256) = $hash
                    """;

                update.Parameters.AddWithValue("$opened", opened);
                update.Parameters.AddWithValue("$destroyed", destroyed);
                update.Parameters.AddWithValue(
                    "$matchDate",
                    (object?)matchDate?.ToString("O") ?? DBNull.Value);
                update.Parameters.AddWithValue("$hash", hash);

                update.ExecuteNonQuery();
            }

            WriteLog("[TERMINÉ] " + filename);
            WriteLog($"   Ouverts : {opened} | Détruits : {destroyed} | Total : {opened + destroyed}");

            StatisticsUpdated?.Invoke();
        }
        finally
        {
            if (File.Exists(temporaryFile))
                File.Delete(temporaryFile);
        }
    }

    private SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection(
            $"Data Source={_databasePath}");

        connection.Open();
        return connection;
    }

    private void SetError(
        SqliteConnection connection, string hash, string message)
    {
        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE replays
            SET status = 'error',
                error_message = $message,
                analyzed_at = CURRENT_TIMESTAMP
            WHERE lower(sha256) = $hash
            """;

        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$hash", hash);

        command.ExecuteNonQuery();
    }

    private void WriteLog(string message)
    {
        Log?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _watcher?.Dispose();
        _cancellation?.Cancel();
        _cancellation?.Dispose();

        _disposed = true;
    }
}
