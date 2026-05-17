using System.Diagnostics;
using System.Text.RegularExpressions;
using XiangqiApi.Models;
using XiangqiApi.Model;

namespace XiangqiApi.Services;

public class PikafishService : IDisposable
{
    private readonly string _enginePath;
    private readonly ILogger<PikafishService> _logger;
    private Process? _engineProcess;
    private StreamWriter? _engineInput;
    private StreamReader? _engineOutput;
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    private bool _disposed;

    public PikafishService(ILogger<PikafishService> logger)
    {
        _logger = logger;
        _enginePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Engines",
            PlatformSpecificEngineName());

        InitializeEngine().Wait();
    }

    private string PlatformSpecificEngineName()
    {
        if (OperatingSystem.IsWindows())
            return "pikafish.exe";
        else if (OperatingSystem.IsLinux())
            return "pikafish";
        else if (OperatingSystem.IsMacOS())
            return "pikafish-macos";

        return "pikafish";
    }

    private async Task InitializeEngine()
    {
        try
        {
            _engineProcess = new Process();
            _engineProcess.StartInfo.FileName = _enginePath;
            _engineProcess.StartInfo.UseShellExecute = false;
            _engineProcess.StartInfo.CreateNoWindow = true;
            _engineProcess.StartInfo.RedirectStandardInput = true;
            _engineProcess.StartInfo.RedirectStandardOutput = true;
            _engineProcess.StartInfo.RedirectStandardError = true;

            _engineProcess.Start();

            _engineInput = _engineProcess.StandardInput;
            _engineOutput = _engineProcess.StandardOutput;

            await _engineInput.WriteLineAsync("uci");
            await _engineInput.FlushAsync();

            string? line;
            while ((line = await _engineOutput.ReadLineAsync()) != null)
            {
                if (line == "uciok")
                    break;
            }

            await _engineInput.WriteLineAsync("isready");
            await _engineInput.FlushAsync();

            while ((line = await _engineOutput.ReadLineAsync()) != null)
            {
                if (line == "readyok")
                    break;
            }

            // Set default options
            await _engineInput.WriteLineAsync("setoption name Threads value 1");
            await _engineInput.WriteLineAsync("setoption name Hash value 16");
            await _engineInput.FlushAsync();

            _logger.LogInformation("Pikafish engine initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Pikafish engine");
            throw;
        }
    }

    public async Task<AnalyzeResponse> Analyze(AnalyzeRequest request)
    {
        await _engineLock.WaitAsync();
        try
        {
            var multipv = request.Multipv ?? 3;
            multipv = Math.Clamp(multipv, 1, 5);

            var depth = request.Depth ?? 14;
            depth = Math.Clamp(depth, 1, 20);

            await EnsureEngineReady();

            await _engineInput!.WriteLineAsync($"setoption name MultiPV value {multipv}");
            await _engineInput.FlushAsync();

            await _engineInput.WriteLineAsync($"position fen {request.Fen}");
            await _engineInput.FlushAsync();

            string goCommand;
            if (request.MovetimeMs.HasValue && request.MovetimeMs.Value > 0)
            {
                var movetime = Math.Clamp(request.MovetimeMs.Value, 100, 30000);
                goCommand = $"go movetime {movetime}";
            }
            else
            {
                goCommand = $"go depth {depth}";
            }

            await _engineInput.WriteLineAsync(goCommand);
            await _engineInput.FlushAsync();

            // Dictionary lưu dòng cuối cùng cho mỗi multipv
            var finalLines = new Dictionary<int, MoveLine>();
            int maxDepthReached = 0;

            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!timeoutCts.Token.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await _engineOutput!.ReadLineAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Analysis timeout");
                    break;
                }

                if (line == null) break;

                // Cập nhật depth tối đa
                var depthMatch = Regex.Match(line, @"depth (\d+)");
                if (depthMatch.Success)
                {
                    var currentDepth = int.Parse(depthMatch.Groups[1].Value);
                    if (currentDepth > maxDepthReached)
                        maxDepthReached = currentDepth;
                }

                // Parse info lines
                if ((line.Contains("score cp") || line.Contains("score mate")) && line.Contains(" pv "))
                {
                    var parsed = ParseInfoLine(line);
                    if (parsed != null)
                    {
                        // Lấy multipv index
                        var multipvMatch = Regex.Match(line, @"multipv (\d+)");
                        if (multipvMatch.Success)
                        {
                            int multipvIndex = int.Parse(multipvMatch.Groups[1].Value);
                            // Lưu đè, giữ lại dòng cuối cùng cho mỗi multipv
                            finalLines[multipvIndex] = parsed;
                        }
                    }
                }

                if (line.StartsWith("bestmove"))
                {
                    break;
                }
            }

            // Tạo danh sách lines từ finalLines, sắp xếp theo multipv
            var sortedLines = finalLines
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .Take(multipv)
                .ToList();

            // Best move là line đầu tiên (multipv 1)
            string bestMove = "";
            int bestScore = 0;
            int? bestMate = null;

            if (sortedLines.Any())
            {
                bestMove = sortedLines[0].Move;
                bestScore = sortedLines[0].ScoreCp;
                bestMate = sortedLines[0].Mate;
            }

            var response = new AnalyzeResponse
            {
                Bestmove = bestMove,
                ScoreCp = bestScore,
                Mate = bestMate,
                Depth = maxDepthReached > 0 ? maxDepthReached : depth,
                Lines = sortedLines
            };

            // Xử lý candidate move - lấy từ finalLines nếu có
            if (!string.IsNullOrEmpty(request.CandidateMove))
            {
                int? candidateScore = null;

                // Tìm candidate trong finalLines
                foreach (var line in finalLines.Values)
                {
                    if (line.Move == request.CandidateMove)
                    {
                        candidateScore = line.ScoreCp;
                        break;
                    }
                }

                if (candidateScore.HasValue)
                {
                    var delta = bestScore - candidateScore.Value;
                    response.Candidate = new CandidateInfo
                    {
                        Move = request.CandidateMove,
                        ScoreCp = candidateScore.Value,
                        DeltaCp = delta,
                        Classification = ClassifyMove(delta)
                    };
                }
                else
                {
                    // Fallback: chạy riêng nếu không tìm thấy
                    var fallbackScore = await GetMoveScore(request.Fen, request.CandidateMove, depth);
                    var delta = bestScore - fallbackScore;
                    response.Candidate = new CandidateInfo
                    {
                        Move = request.CandidateMove,
                        ScoreCp = fallbackScore,
                        DeltaCp = delta,
                        Classification = ClassifyMove(delta)
                    };
                }
            }

            return response;
        }
        finally
        {
            _engineLock.Release();
        }
    }

    public async Task<BestMoveResponse> GetBestMove(BestMoveRequest request)
    {
        await _engineLock.WaitAsync();
        try
        {
            var level = Math.Clamp(request.Level ?? 5, 1, 10);
            var depth = level switch
            {
                1 => 4,
                2 => 6,
                3 => 8,
                4 => 10,
                5 => 12,
                6 => 14,
                7 => 16,
                8 => 18,
                9 => 20,
                10 => 22,
                _ => 14
            };

            await EnsureEngineReady();

            await _engineInput!.WriteLineAsync("setoption name MultiPV value 1");
            await _engineInput.FlushAsync();

            await _engineInput.WriteLineAsync($"position fen {request.Fen}");
            await _engineInput.FlushAsync();

            await _engineInput.WriteLineAsync($"go depth {depth}");
            await _engineInput.FlushAsync();

            string? bestMove = null;
            string? ponder = null;
            int score = 0;

            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            while (!timeoutCts.Token.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await _engineOutput!.ReadLineAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (line == null) break;

                if (line.Contains("score cp"))
                {
                    var scoreMatch = Regex.Match(line, @"score cp (-?\d+)");
                    if (scoreMatch.Success)
                    {
                        score = int.Parse(scoreMatch.Groups[1].Value);
                    }
                }

                if (line.StartsWith("bestmove"))
                {
                    var bestMatch = Regex.Match(line, @"bestmove (\S+)");
                    if (bestMatch.Success)
                    {
                        bestMove = bestMatch.Groups[1].Value;
                    }

                    var ponderMatch = Regex.Match(line, @"ponder (\S+)");
                    if (ponderMatch.Success)
                    {
                        ponder = ponderMatch.Groups[1].Value;
                    }
                    break;
                }
            }

            return new BestMoveResponse
            {
                Bestmove = bestMove ?? "",
                ScoreCp = score,
                Ponder = ponder
            };
        }
        finally
        {
            _engineLock.Release();
        }
    }

    private async Task<int> GetMoveScore(string fen, string move, int depth)
    {
        await EnsureEngineReady();

        await _engineInput!.WriteLineAsync("setoption name MultiPV value 1");
        await _engineInput.FlushAsync();

        await _engineInput.WriteLineAsync($"position fen {fen}");
        await _engineInput.FlushAsync();

        await _engineInput.WriteLineAsync($"go depth {depth} searchmoves {move}");
        await _engineInput.FlushAsync();

        int score = 0;
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _engineOutput!.ReadLineAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line == null) break;

            var scoreMatch = Regex.Match(line, @"score cp (-?\d+)");
            if (scoreMatch.Success)
            {
                score = int.Parse(scoreMatch.Groups[1].Value);
            }

            if (line.StartsWith("bestmove"))
            {
                break;
            }
        }

        return score;
    }

    private MoveLine? ParseInfoLine(string line)
    {
        try
        {
            var pvMatch = Regex.Match(line, @" pv (.+)$");
            if (!pvMatch.Success) return null;

            var pv = pvMatch.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(8).ToList();
            if (!pv.Any()) return null;

            var move = pv[0];

            // Lấy score cp
            var scoreCpMatch = Regex.Match(line, @"score cp (-?\d+)");
            int scoreCp = 0;
            int? mate = null;

            if (scoreCpMatch.Success)
            {
                scoreCp = int.Parse(scoreCpMatch.Groups[1].Value);
            }
            else
            {
                var scoreMateMatch = Regex.Match(line, @"score mate (-?\d+)");
                if (scoreMateMatch.Success)
                {
                    mate = int.Parse(scoreMateMatch.Groups[1].Value);
                    scoreCp = mate.Value > 0 ? 30000 - mate.Value : -30000 - mate.Value;
                }
            }

            return new MoveLine
            {
                Move = move,
                ScoreCp = scoreCp,
                Mate = mate,
                Pv = pv
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing line: {Line}", line);
            return null;
        }
    }

    private async Task EnsureEngineReady()
    {
        if (_engineProcess == null || _engineProcess.HasExited)
        {
            await InitializeEngine();
        }

        await _engineInput!.WriteLineAsync("isready");
        await _engineInput.FlushAsync();

        string? line;
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeout)
        {
            if (_engineOutput!.Peek() >= 0)
            {
                line = await _engineOutput.ReadLineAsync();
                if (line == "readyok")
                    return;
            }
            await Task.Delay(10);
        }
    }

    private string ClassifyMove(int deltaCp)
    {
        return deltaCp switch
        {
            <= 15 => "Tuyệt vời",
            <= 50 => "Khá",
            <= 120 => "Chưa chính xác",
            <= 250 => "Sai lầm",
            _ => "Sai lầm nghiêm trọng"
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _engineLock.Wait();
        try
        {
            if (_engineInput != null)
            {
                _engineInput.WriteLine("quit");
                _engineInput.Flush();
                _engineInput.Dispose();
            }

            _engineProcess?.Kill();
            _engineProcess?.Dispose();
            _engineOutput?.Dispose();
        }
        finally
        {
            _engineLock.Release();
            _engineLock.Dispose();
            _disposed = true;
        }
    }
}
