using System.Text.Json.Serialization;

namespace XiangqiApi.Model
{
    public class MoveLine
    {
        [JsonPropertyName("move")]
        public string Move { get; set; } = string.Empty;

        [JsonPropertyName("score_cp")]
        public int ScoreCp { get; set; }

        [JsonPropertyName("mate")]
        public int? Mate { get; set; }

        [JsonPropertyName("pv")]
        public List<string> Pv { get; set; } = new();
    }
}
