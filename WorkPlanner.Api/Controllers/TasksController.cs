using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPlanner.Api.Data;
using WorkPlanner.Api.Models;
using TaskStatus = WorkPlanner.Api.Models.TaskStatus;

namespace WorkPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _context;

    public TasksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskItemDto>>> GetTasks([FromQuery] int? projectId, [FromQuery] int? sprintId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var query = _context.TaskItems
            .AsNoTracking()
            .Where(t => t.ProjectId.HasValue)
            .Where(t => t.Project!.Members.Any(m => m.UserId == userId));

        if (projectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == projectId.Value);
        }

        if (sprintId.HasValue)
        {
            query = query.Where(t => t.SprintId == sprintId.Value);
        }

        var tasks = await query
            .OrderBy(t => t.Order)
            .Select(t => new TaskItemDto
            {
                Id = t.Id,
                ProjectId = t.ProjectId ?? 0,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                CompletedAt = t.CompletedAt,
                AssigneeId = t.AssigneeId,
                SprintId = t.SprintId,
                Order = t.Order
            })
            .ToListAsync();

        return tasks;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskItemDto>> GetTask(int id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var task = await _context.TaskItems
            .AsNoTracking()
            .Where(t => t.Id == id && t.ProjectId.HasValue)
            .Where(t => t.Project!.Members.Any(m => m.UserId == userId))
            .Select(t => new TaskItemDto
            {
                Id = t.Id,
                ProjectId = t.ProjectId ?? 0,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                CompletedAt = t.CompletedAt,
                AssigneeId = t.AssigneeId,
                SprintId = t.SprintId,
                Order = t.Order
            })
            .FirstOrDefaultAsync();

        if (task == null)
        {
            return NotFound();
        }

        return task;
    }

    [HttpPost]
    public async Task<ActionResult<TaskItemDto>> CreateTask(CreateTaskRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (request.ProjectId <= 0)
        {
            return BadRequest("ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Title is required.");
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == request.ProjectId && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(request.AssigneeId))
        {
            var assigneeHasAccess = await _context.ProjectMembers
                .AnyAsync(m => m.ProjectId == request.ProjectId && m.UserId == request.AssigneeId);

            if (!assigneeHasAccess)
            {
                return BadRequest("Assignee must be a member of the project.");
            }
        }

        if (request.SprintId.HasValue)
        {
            var sprintExists = await _context.Sprints
                .AnyAsync(s => s.Id == request.SprintId.Value && s.ProjectId == request.ProjectId && !s.IsArchived);

            if (!sprintExists)
            {
                return BadRequest("Sprint does not exist or is archived.");
            }
        }

        if (request.SprintId == null)
        {
            if (request.Status != TaskStatus.Backlog)
            {
                return BadRequest("Backlog items must use Backlog status.");
            }
        }
        else if (request.Status == TaskStatus.Backlog)
        {
            return BadRequest("Sprint items cannot use Backlog status.");
        }

        var status = request.Status == TaskStatus.Backlog ? TaskStatus.Backlog : request.Status;

        var task = new TaskItem
        {
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            AssigneeId = string.IsNullOrWhiteSpace(request.AssigneeId) ? null : request.AssigneeId,
            SprintId = request.SprintId,
            Order = request.Order
        };

        if (task.Order <= 0)
        {
            var maxOrder = await _context.TaskItems
                .Where(t => t.ProjectId == request.ProjectId)
                .Where(t => t.SprintId == request.SprintId)
                .Where(t => t.Status == task.Status)
                .Select(t => (int?)t.Order)
                .MaxAsync() ?? 0;

            task.Order = maxOrder + 1;
        }

        if (task.Status == TaskStatus.Done)
        {
            task.CompletedAt = DateTime.UtcNow;
        }

        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();

        var dto = new TaskItemDto
        {
            Id = task.Id,
            ProjectId = task.ProjectId ?? 0,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            CreatedAt = task.CreatedAt,
            CompletedAt = task.CompletedAt,
            AssigneeId = task.AssigneeId,
            SprintId = task.SprintId,
            Order = task.Order
        };

        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, dto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(int id, UpdateTaskRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var task = await _context.TaskItems
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
        {
            return NotFound();
        }

        if (!task.ProjectId.HasValue)
        {
            return BadRequest("Task is missing ProjectId.");
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == task.ProjectId.Value && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(request.AssigneeId))
        {
            var assigneeHasAccess = await _context.ProjectMembers
                .AnyAsync(m => m.ProjectId == task.ProjectId.Value && m.UserId == request.AssigneeId);

            if (!assigneeHasAccess)
            {
                return BadRequest("Assignee must be a member of the project.");
            }
        }

        if (request.SprintId.HasValue)
        {
            var sprintExists = await _context.Sprints
                .AnyAsync(s => s.Id == request.SprintId.Value && s.ProjectId == task.ProjectId.Value && !s.IsArchived);

            if (!sprintExists)
            {
                return BadRequest("Sprint does not exist or is archived.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Title is required.");
        }

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim() ?? string.Empty;
        task.Status = request.Status;
        task.AssigneeId = string.IsNullOrWhiteSpace(request.AssigneeId) ? null : request.AssigneeId;
        task.SprintId = request.SprintId;
        task.Order = request.Order;

        if (task.Order <= 0)
        {
            var maxOrder = await _context.TaskItems
                .Where(t => t.ProjectId == task.ProjectId)
                .Where(t => t.SprintId == task.SprintId)
                .Where(t => t.Status == task.Status)
                .Select(t => (int?)t.Order)
                .MaxAsync() ?? 0;

            task.Order = maxOrder + 1;
        }

        if (task.Status == TaskStatus.Done && !task.CompletedAt.HasValue)
        {
            task.CompletedAt = DateTime.UtcNow;
        }
        else if (task.Status != TaskStatus.Done)
        {
            task.CompletedAt = null;
        }

        _context.Entry(task).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TaskExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var task = await _context.TaskItems.FindAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        if (!task.ProjectId.HasValue)
        {
            return BadRequest("Task is missing ProjectId.");
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == task.ProjectId.Value && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool TaskExists(int id)
    {
        return _context.TaskItems.Any(e => e.Id == id);
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}

public class CreateTaskRequest
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Backlog;
    public string? AssigneeId { get; set; }
    public int? SprintId { get; set; }
    public int Order { get; set; }
}

public class UpdateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; }
    public string? AssigneeId { get; set; }
    public int? SprintId { get; set; }
    public int Order { get; set; }
}

public class TaskItemDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AssigneeId { get; set; }
    public int? SprintId { get; set; }
    public int Order { get; set; }
}
