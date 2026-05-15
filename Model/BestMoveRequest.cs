using System.Text.Json.Serialization;

namespace XiangqiApi.Model
{
    public class BestMoveRequest
    {
        [JsonPropertyName("fen")]
        public string Fen { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public int? Level { get; set; } = 5;
    }
}
