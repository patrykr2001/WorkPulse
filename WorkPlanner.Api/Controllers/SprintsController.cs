using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPlanner.Api.Data;
using WorkPlanner.Api.Models;

namespace WorkPlanner.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:int}/sprints")]
[Authorize]
public class SprintsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SprintsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SprintDto>>> GetSprints(int projectId, [FromQuery] bool includeArchived = false)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        var query = _context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId);

        if (!includeArchived)
        {
            query = query.Where(s => !s.IsArchived);
        }

        var sprints = await query
            .OrderBy(s => s.Order)
            .Select(s => new SprintDto
            {
                Id = s.Id,
                ProjectId = s.ProjectId,
                Name = s.Name,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                IsActive = s.IsActive,
                IsArchived = s.IsArchived,
                CreatedAt = s.CreatedAt,
                Order = s.Order
            })
            .ToListAsync();

        return sprints;
    }

    [HttpPost]
    public async Task<ActionResult<SprintDto>> CreateSprint(int projectId, CreateSprintRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Sprint name is required.");
        }

        if (request.EndDate < request.StartDate)
        {
            return BadRequest("EndDate must be after StartDate.");
        }

        var maxOrder = await _context.Sprints
            .Where(s => s.ProjectId == projectId)
            .Select(s => (int?)s.Order)
            .MaxAsync() ?? 0;

        var sprint = new Sprint
        {
            ProjectId = projectId,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            Order = maxOrder + 1
        };

        if (sprint.IsActive)
        {
            await DeactivateOtherSprints(projectId);
        }

        _context.Sprints.Add(sprint);
        await _context.SaveChangesAsync();

        var dto = new SprintDto
        {
            Id = sprint.Id,
            ProjectId = sprint.ProjectId,
            Name = sprint.Name,
            StartDate = sprint.StartDate,
            EndDate = sprint.EndDate,
            IsActive = sprint.IsActive,
            IsArchived = sprint.IsArchived,
            CreatedAt = sprint.CreatedAt,
            Order = sprint.Order
        };

        return CreatedAtAction(nameof(GetSprints), new { projectId }, dto);
    }

    [HttpPut("{sprintId:int}")]
    public async Task<IActionResult> UpdateSprint(int projectId, int sprintId, UpdateSprintRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        var sprint = await _context.Sprints
            .FirstOrDefaultAsync(s => s.Id == sprintId && s.ProjectId == projectId);

        if (sprint == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Sprint name is required.");
        }

        if (request.EndDate < request.StartDate)
        {
            return BadRequest("EndDate must be after StartDate.");
        }

        sprint.Name = request.Name.Trim();
        sprint.StartDate = request.StartDate;
        sprint.EndDate = request.EndDate;
        sprint.IsArchived = request.IsArchived;

        if (request.IsActive && !sprint.IsArchived)
        {
            await DeactivateOtherSprints(projectId);
            sprint.IsActive = true;
        }
        else if (!request.IsActive)
        {
            sprint.IsActive = false;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{sprintId:int}/activate")]
    public async Task<IActionResult> ActivateSprint(int projectId, int sprintId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        var sprint = await _context.Sprints
            .FirstOrDefaultAsync(s => s.Id == sprintId && s.ProjectId == projectId);

        if (sprint == null)
        {
            return NotFound();
        }

        if (sprint.IsArchived)
        {
            return BadRequest("Cannot activate archived sprint.");
        }

        await DeactivateOtherSprints(projectId);
        sprint.IsActive = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{sprintId:int}/archive")]
    public async Task<IActionResult> ArchiveSprint(int projectId, int sprintId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        var sprint = await _context.Sprints
            .FirstOrDefaultAsync(s => s.Id == sprintId && s.ProjectId == projectId);

        if (sprint == null)
        {
            return NotFound();
        }

        sprint.IsArchived = true;
        sprint.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task DeactivateOtherSprints(int projectId)
    {
        var activeSprints = await _context.Sprints
            .Where(s => s.ProjectId == projectId && s.IsActive)
            .ToListAsync();

        foreach (var sprint in activeSprints)
        {
            sprint.IsActive = false;
        }
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}

public class CreateSprintRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateSprintRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
}

public class SprintDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Order { get; set; }
}
