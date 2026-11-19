
using ChestCounter;
using System.Security.Cryptography;
using System.Text.Json;

const string replayFolder = @"C:\FortniteReplayAnalyzer\backups\replays";
const string outputFile = @"C:\FortniteReplayAnalyzer\data\analysis_results.json";
const string username = "BRK-_-S C Λ R Ξ";

Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

var files = Directory.GetFiles(replayFolder, "*.replay")
    .OrderBy(Path.GetFileName)
    .ToArray();

if (files.Length == 0)
{
    Console.WriteLine("Aucun replay trouvé.");
    return;
}

var results = new List<object>();

Console.WriteLine($"Replays trouvés : {files.Length}");
Console.WriteLine($"Joueur recherché : {username}");
Console.WriteLine();

foreach (var file in files)
{
    string filename = Path.GetFileName(file);
    Console.WriteLine("----------------------------------------");
    Console.WriteLine("Analyse : " + filename);

    try
    {
        string sha256;
        using (var hashStream = File.OpenRead(file))
        {
            sha256 = Convert.ToHexString(
                SHA256.HashData(hashStream)
            );
        }

        using var stream = File.OpenRead(file);
        var result = new ReplayAnalyzer().Analyze(stream, file, username);

        if (!result.TrackedByUsername)
        {
            Console.WriteLine("Joueur non reconnu.");

            results.Add(new
            {
                sha256,
                filename,
                status = "not_tracked",
                opened = 0,
                destroyed = 0,
                error = "Joueur non reconnu dans ce replay."
            });
        }
        else
        {
            int opened = result.Chests.Count(c => !c.Destroyed);
            int destroyed = result.Chests.Count(c => c.Destroyed);

            Console.WriteLine("Joueur reconnu : " + result.PlayerName);
            Console.WriteLine("Coffres ouverts : " + opened);
            Console.WriteLine("Coffres détruits : " + destroyed);
            Console.WriteLine("Total : " + (opened + destroyed));

            results.Add(new
            {
                sha256,
                filename,
                status = "analyzed",
                opened,
                destroyed,
                error = (string?)null
            });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("ERREUR : " + ex.Message);

        results.Add(new
        {
            sha256 = "",
            filename,
            status = "error",
            opened = 0,
            destroyed = 0,
            error = ex.Message
        });
    }

    File.WriteAllText(
        outputFile,
        JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true
        })
    );
}

int totalOpened = results.Sum(r =>
    (int)r.GetType().GetProperty("opened")!.GetValue(r)!);

int totalDestroyed = results.Sum(r =>
    (int)r.GetType().GetProperty("destroyed")!.GetValue(r)!);

Console.WriteLine();
Console.WriteLine("========================================");
Console.WriteLine("BILAN GLOBAL");
Console.WriteLine("Replays traités : " + results.Count);
Console.WriteLine("Coffres ouverts : " + totalOpened);
Console.WriteLine("Coffres détruits : " + totalDestroyed);
Console.WriteLine("TOTAL : " + (totalOpened + totalDestroyed));
Console.WriteLine("========================================");
Console.WriteLine("Résultats sauvegardés dans : " + outputFile);
