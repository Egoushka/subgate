using System.Text.Json.Serialization;
using Switchknife.Pace;

namespace Subgate.Store;

/// <summary>How the engine's state is written as JSON: source-generated, so it stays Native AOT safe.</summary>
[JsonSourceGenerationOptions(NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(LedgerState))]
internal sealed partial class StateJsonContext : JsonSerializerContext;
