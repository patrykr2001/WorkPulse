using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPlanner.Api.Data;
using WorkPlanner.Api.Models;

namespace WorkPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkEntriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public WorkEntriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkEntry>>> GetWorkEntries()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        return await _context.WorkEntries
            .AsNoTracking()
            .Include(we => we.TaskItem)
            .Where(we => we.TaskItem.ProjectId.HasValue)
            .Where(we => we.TaskItem.Project!.Members.Any(m => m.UserId == userId))
            .OrderByDescending(we => we.StartTime)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkEntry>> GetWorkEntry(int id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var workEntry = await _context.WorkEntries
            .AsNoTracking()
            .Include(we => we.TaskItem)
            .FirstOrDefaultAsync(we => we.Id == id && we.TaskItem.ProjectId.HasValue && we.TaskItem.Project!.Members.Any(m => m.UserId == userId));

        if (workEntry == null)
        {
            return NotFound();
        }

        return workEntry;
    }

    [HttpGet("by-task/{taskId}")]
    public async Task<ActionResult<IEnumerable<WorkEntry>>> GetWorkEntriesByTask(int taskId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        return await _context.WorkEntries
            .AsNoTracking()
            .Where(we => we.TaskItemId == taskId)
            .Where(we => we.TaskItem.ProjectId.HasValue)
            .Where(we => we.TaskItem.Project!.Members.Any(m => m.UserId == userId))
            .OrderByDescending(we => we.StartTime)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<WorkEntry>> CreateWorkEntry(WorkEntry workEntry)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var hasAccess = await _context.TaskItems
            .AnyAsync(t => t.Id == workEntry.TaskItemId && t.ProjectId.HasValue && t.Project!.Members.Any(m => m.UserId == userId));

        if (!hasAccess)
        {
            return Forbid();
        }

        workEntry.CreatedAt = DateTime.UtcNow;
        _context.WorkEntries.Add(workEntry);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWorkEntry), new { id = workEntry.Id }, workEntry);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateWorkEntry(int id, WorkEntry workEntry)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (id != workEntry.Id)
        {
            return BadRequest();
        }

        var hasAccess = await _context.TaskItems
            .AnyAsync(t => t.Id == workEntry.TaskItemId && t.ProjectId.HasValue && t.Project!.Members.Any(m => m.UserId == userId));

        if (!hasAccess)
        {
            return Forbid();
        }

        _context.Entry(workEntry).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!WorkEntryExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkEntry(int id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var workEntry = await _context.WorkEntries.FindAsync(id);
        if (workEntry == null)
        {
            return NotFound();
        }

        var hasAccess = await _context.TaskItems
            .AnyAsync(t => t.Id == workEntry.TaskItemId && t.ProjectId.HasValue && t.Project!.Members.Any(m => m.UserId == userId));

        if (!hasAccess)
        {
            return Forbid();
        }

        _context.WorkEntries.Remove(workEntry);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool WorkEntryExists(int id)
    {
        return _context.WorkEntries.Any(e => e.Id == id);
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
