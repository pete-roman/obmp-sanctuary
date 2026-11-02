using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Sanctuary.Server;

// One incident as it goes into the log: one JSON object per line. Verdict is "suspect" (one player was
// fighting next to the victim), "several" (more were) or "none" (nobody: a fall, an NPC). Every player in
// Aggressors lost the Sanctuary's protection for it.
public sealed record IncidentRecord(
    DateTimeOffset Time,
    string Area,
    string AreaName,
    PersonRecord Victim,
    float Damage,
    bool Died,
    string Verdict,
    IReadOnlyList<PersonRecord> Aggressors);

public sealed record PersonRecord(string? Account, string Name);

// Appends incidents to <server root>/moddata/CyrodiilRP.Sanctuary/incidents-<date>.jsonl on its own
// thread, so disk IO never holds up the server tick. The folder sits outside the mod folder, so a new
// version of the mod never wipes it. Once the log is stopping, writes go straight to disk instead, so
// incidents closed while the server shuts down are not lost.
[Service]
public sealed partial class IncidentLog(Mod mod, ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly BlockingCollection<IncidentRecord> _queue = new();
    private readonly object _disk = new();
    private Thread? _thread;

    // Set by the tests; otherwise worked out from where the server loaded the mod.
    public string? FolderOverride { get; init; }

    public string Folder => FolderOverride ?? Resolve(mod.ModDirectory);

    private void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Sanctuary incident log" };
        _thread.Start();
        logger.LogInformation("Sanctuary: incidents are written to {Folder}.", Folder);
    }

    private void Stop()
    {
        _queue.CompleteAdding();
        if (_thread is not null && !_thread.Join(TimeSpan.FromSeconds(5)))
            logger.LogWarning("Sanctuary: the incident log did not finish writing within 5 seconds.");
    }

    public void Write(IncidentRecord record)
    {
        try
        {
            _queue.Add(record);
        }
        catch (InvalidOperationException)
        {
            Append(record); // stopping: the writer thread takes no more
        }
    }

    // The line a record becomes, and the file it goes to.
    public static string LineOf(IncidentRecord record) => JsonSerializer.Serialize(record, Json);

    public string FileOf(IncidentRecord record) =>
        Path.Combine(Folder, $"incidents-{record.Time.UtcDateTime:yyyy-MM-dd}.jsonl");

    // The mod's server half loads from <server root>/mods/<mod>/server.
    public static string Resolve(string modDirectory) =>
        Path.Combine(Path.GetFullPath(Path.Combine(modDirectory, "..", "..", "..")), "moddata", "CyrodiilRP.Sanctuary");

    private void Run()
    {
        foreach (var record in _queue.GetConsumingEnumerable())
            Append(record);
    }

    private void Append(IncidentRecord record)
    {
        try
        {
            var file = FileOf(record);
            lock (_disk)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.AppendAllText(file, LineOf(record) + "\n", Utf8);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Sanctuary: could not write an incident to {Folder}.", Folder);
        }
    }
}
