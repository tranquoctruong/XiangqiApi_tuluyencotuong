using System.Text.Json.Serialization;


namespace XiangqiApi.Model
{
    public class AnalyzeResponse
    {
        [JsonPropertyName("bestmove")]
        public string Bestmove { get; set; } = string.Empty;

        [JsonPropertyName("score_cp")]
        public int ScoreCp { get; set; }

        [JsonPropertyName("mate")]
        public int? Mate { get; set; }

        [JsonPropertyName("depth")]
        public int Depth { get; set; }

        [JsonPropertyName("lines")]
        public List<MoveLine> Lines { get; set; } = new();

        [JsonPropertyName("candidate")]
        public CandidateInfo? Candidate { get; set; }
    }

    public class CandidateInfo
    {
        [JsonPropertyName("move")]
        public string Move { get; set; } = string.Empty;

        [JsonPropertyName("score_cp")]
        public int ScoreCp { get; set; }

        [JsonPropertyName("delta_cp")]
        public int DeltaCp { get; set; }

        [JsonPropertyName("classification")]
        public string Classification { get; set; } = string.Empty;
    }
}
