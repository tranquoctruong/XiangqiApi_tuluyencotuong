namespace XiangqiApi.Model
{
    public class AnalyzeMoveResponse
    {
        public string YourMove { get; set; } = "";

        public int YourMoveScore { get; set; }

        public string BestMove { get; set; } = "";

        public int BestMoveScore { get; set; }

        public int Accuracy { get; set; }

        public double WinRate { get; set; }

        public string MoveType { get; set; } = "";

        public List<TopMoveDto> TopMoves { get; set; } = [];
    }

    public class TopMoveDto
    {
        public string Move { get; set; } = "";

        public int Score { get; set; }

        public double WinRate { get; set; }
        public int Depth { get; set; }
    }
}
