using System.Text.Json;

namespace Subgate.Providers.Anthropic;

/// <summary>Parses the final `result` message of a `-p` or SDK run (ADR 0012): per-model cost.</summary>
public static class RunResultParser
{
    public static UsageObservation Parse(string json)
    {
        var dto = JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.RunResultDto)
            ?? throw new JsonException("empty run result");

        var modelUsages = new List<ModelUsage>();
        var totalFromModels = 0.0;
        if (dto.ModelUsage is { } modelUsage)
        {
            foreach (var (model, usage) in modelUsage)
            {
                modelUsages.Add(new ModelUsage(model, usage.CostUsd));
                totalFromModels += usage.CostUsd;
            }
        }

        return new UsageObservation(dto.SessionId, dto.TotalCostUsd ?? totalFromModels, modelUsages);
    }
}
