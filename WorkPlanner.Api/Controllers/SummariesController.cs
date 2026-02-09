using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPlanner.Api.Data;

namespace WorkPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SummariesController : ControllerBase
{
    private readonly AppDbContext _context;

    public SummariesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("daily")]
    public async Task<ActionResult<IEnumerable<DailySummary>>> GetDailySummary([FromQuery] DateTime? date)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var targetDate = date?.Date ?? DateTime.UtcNow.Date;
        var nextDay = targetDate.AddDays(1);

        var entries = await _context.WorkEntries
            .Where(we => we.StartTime >= targetDate && we.StartTime < nextDay && we.EndTime != null)
            .Where(we => we.TaskItem.ProjectId.HasValue)
            .Where(we => we.TaskItem.Project!.Members.Any(m => m.UserId == userId))
            .Include(we => we.TaskItem)
            .AsNoTracking()
            .ToListAsync();

        var summary = entries
            .GroupBy(we => we.TaskItemId)
            .Select(g => new DailySummary
            {
                TaskId = g.Key,
                TaskTitle = g.First().TaskItem?.Title ?? "Unknown",
                TotalHours = g.Sum(we => (we.EndTime!.Value - we.StartTime).TotalHours),
                EntryCount = g.Count()
            })
            .ToList();

        return summary;
    }

    [HttpGet("weekly")]
    public async Task<ActionResult<WeeklySummary>> GetWeeklySummary([FromQuery] DateTime? weekStart)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var startOfWeek = weekStart?.Date ?? DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek).Date;
        var endOfWeek = startOfWeek.AddDays(7);

        var entries = await _context.WorkEntries
            .Where(we => we.StartTime >= startOfWeek && we.StartTime < endOfWeek && we.EndTime != null)
            .Where(we => we.TaskItem.ProjectId.HasValue)
            .Where(we => we.TaskItem.Project!.Members.Any(m => m.UserId == userId))
            .Include(we => we.TaskItem)
            .AsNoTracking()
            .ToListAsync();

        var dailyHours = entries
            .GroupBy(we => we.StartTime.Date)
            .Select(g => new DailyHours
            {
                Date = g.Key,
                Hours = g.Sum(we => (we.EndTime!.Value - we.StartTime).TotalHours)
            })
            .ToList();

        var taskSummaries = entries
            .GroupBy(we => we.TaskItemId)
            .Select(g => new TaskSummary
            {
                TaskId = g.Key,
                TaskTitle = g.First().TaskItem?.Title ?? "Unknown",
                TotalHours = g.Sum(we => (we.EndTime!.Value - we.StartTime).TotalHours)
            })
            .ToList();

        return new WeeklySummary
        {
            WeekStart = startOfWeek,
            WeekEnd = endOfWeek.AddDays(-1),
            TotalHours = entries.Sum(we => (we.EndTime!.Value - we.StartTime).TotalHours),
            DailyHours = dailyHours,
            TaskSummaries = taskSummaries
        };
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}

public class DailySummary
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public double TotalHours { get; set; }
    public int EntryCount { get; set; }
}

public class WeeklySummary
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public double TotalHours { get; set; }
    public List<DailyHours> DailyHours { get; set; } = new();
    public List<TaskSummary> TaskSummaries { get; set; } = new();
}

public class DailyHours
{
    public DateTime Date { get; set; }
    public double Hours { get; set; }
}

public class TaskSummary
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public double TotalHours { get; set; }
}
