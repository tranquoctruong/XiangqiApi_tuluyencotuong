using System.Text.Json.Serialization;

namespace XiangqiApi.Models;

public class BestMoveResponse
{
    [JsonPropertyName("bestmove")]
    public string Bestmove { get; set; } = string.Empty;
    
    [JsonPropertyName("score_cp")]
    public int ScoreCp { get; set; }
    
    [JsonPropertyName("ponder")]
    public string? Ponder { get; set; }
}