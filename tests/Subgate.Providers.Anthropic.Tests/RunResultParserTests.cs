namespace Subgate.Providers.Anthropic.Tests;

public class RunResultParserTests
{
    [Fact]
    public void Several_models_in_modelUsage_yields_one_usage_report_with_per_model_cost()
    {
        var json = Fixtures.Load("run-result-multi-model.json");

        var result = RunResultParser.Parse(json);

        Assert.Equal("session-placeholder", result.SessionId);
        Assert.Equal(0.07, result.TotalCostUsd);
        Assert.Equal(2, result.ModelUsages.Count);
        Assert.Contains(result.ModelUsages, m => m.Model == "model-a" && m.CostUsd == 0.05);
        Assert.Contains(result.ModelUsages, m => m.Model == "model-b" && m.CostUsd == 0.02);
    }
}
