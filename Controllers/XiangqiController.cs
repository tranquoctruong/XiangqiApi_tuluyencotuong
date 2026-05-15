using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using XiangqiApi.Models;
using XiangqiApi.Services;
using XiangqiApi.Model;

namespace XiangqiAnalyzerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class XiangqiController : ControllerBase
{
    private readonly PikafishService _pikafishService;
    private readonly ILogger<XiangqiController> _logger;

    public XiangqiController(
        PikafishService pikafishService,
        ILogger<XiangqiController> logger)
    {
        _pikafishService = pikafishService;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Fen))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_fen",
                    Message = "FEN string is required"
                });
            }

            // Basic FEN validation (có thể thêm validation chi tiết hơn)
            var fenParts = request.Fen.Split(' ');
            if (fenParts.Length < 4)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_fen",
                    Message = "Invalid FEN format"
                });
            }

            // Validate parameters
            if (request.Depth.HasValue && (request.Depth.Value < 1 || request.Depth.Value > 30))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_depth",
                    Message = "Depth must be between 1 and 30"
                });
            }

            if (request.Multipv.HasValue && (request.Multipv.Value < 1 || request.Multipv.Value > 5))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_multipv",
                    Message = "MultiPV must be between 1 and 5"
                });
            }

            if (request.MovetimeMs.HasValue && (request.MovetimeMs.Value < 100 || request.MovetimeMs.Value > 30000))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_movetime",
                    Message = "Movetime must be between 100 and 30000 ms"
                });
            }

            _logger.LogInformation("Analyzing position: {Fen}, Depth: {Depth}, MultiPV: {MultiPV}",
                request.Fen, request.Depth ?? 14, request.Multipv ?? 3);

            var result = await _pikafishService.Analyze(request);

            return Ok(new { success = true, data = result });
        }
        catch (TimeoutException)
        {
            return StatusCode(408, new ErrorResponse
            {
                Error = "analysis_timeout",
                Message = "Analysis took too long. Please try with lower depth or increase timeout."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing position");
            return StatusCode(500, new ErrorResponse
            {
                Error = "internal_error",
                Message = "An error occurred during analysis. Please try again."
            });
        }
    }

    [HttpPost("bestmove")]
    public async Task<IActionResult> BestMove([FromBody] BestMoveRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Fen))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_fen",
                    Message = "FEN string is required"
                });
            }

            if (request.Level.HasValue && (request.Level.Value < 1 || request.Level.Value > 10))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_level",
                    Message = "Level must be between 1 and 10"
                });
            }

            _logger.LogInformation("Getting best move for position: {Fen}, Level: {Level}",
                request.Fen, request.Level ?? 5);

            var result = await _pikafishService.GetBestMove(request);

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting best move");
            return StatusCode(500, new ErrorResponse
            {
                Error = "internal_error",
                Message = "An error occurred. Please try again."
            });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}