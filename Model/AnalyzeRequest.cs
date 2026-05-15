using System.Text.Json.Serialization;

namespace XiangqiApi.Models;

public class AnalyzeRequest
{
    [JsonPropertyName("fen")]
    public string Fen { get; set; } = string.Empty;

    [JsonPropertyName("depth")]
    public int? Depth { get; set; }

    [JsonPropertyName("multipv")]
    public int? Multipv { get; set; }

    [JsonPropertyName("movetime_ms")]
    public int? MovetimeMs { get; set; }

    [JsonPropertyName("candidate_move")]
    public string? CandidateMove { get; set; }
}