using System.Text.Json;
using System.Text.Json.Serialization;
using Subgate.Api;

namespace Subgate.Cli;

/// <summary>A job's report that could not be delivered: its meter readings and its usage (ADR 0008).</summary>
internal sealed record QueuedReport(SignalBody? Signal, UsageBody? Usage);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(QueuedReport))]
internal sealed partial class CliJsonContext : JsonSerializerContext;

/// <summary>
/// One consumer's reports that wait for subgate, a line of JSON each in <c>queue-&lt;consumer&gt;.jsonl</c>. The file holds numbers and
/// ids only, never a token; the next <c>subgate run</c> of that consumer sends it.
/// </summary>
internal sealed class ReportQueue(string directory, string consumer)
{
    private string FilePath => Path.Combine(directory, $"queue-{consumer}.jsonl");

    /// <summary>False when the file cannot be written; the caller says so and the job's result is not affected.</summary>
    public bool TryAppend(QueuedReport report)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(FilePath, JsonSerializer.Serialize(report, CliJsonContext.Default.QueuedReport) + "\n");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Offers the waiting reports to <paramref name="send"/>, oldest first, until it answers false (subgate is still away); those not
    /// sent stay in the file. The file is moved aside while it is read, so a report appended meanwhile by another run is not lost.
    /// False when subgate is still away, true when nothing is left waiting (or the file cannot be read, which must not stop the next report).
    /// </summary>
    public async Task<bool> Drain(Func<QueuedReport, Task<bool>> send)
    {
        var path = FilePath;
        if (!File.Exists(path))
        {
            return true;
        }

        var batch = $"{path}.{Environment.ProcessId}.sending";
        string[] lines;
        try
        {
            File.Move(path, batch);
            lines = await File.ReadAllLinesAsync(batch);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        var unsent = new List<string>();
        var away = false;
        foreach (var line in lines)
        {
            if (away)
            {
                unsent.Add(line);
                continue;
            }

            QueuedReport? report;
            try
            {
                report = JsonSerializer.Deserialize(line, CliJsonContext.Default.QueuedReport);
            }
            catch (JsonException)
            {
                continue;
            }

            if (report is not null && !await send(report))
            {
                away = true;
                unsent.Add(line);
            }
        }

        try
        {
            if (unsent.Count > 0)
            {
                File.AppendAllLines(path, unsent);
            }

            File.Delete(batch);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The batch file stays beside the queue; nothing is lost, it is only not retried.
        }

        return !away;
    }
}
