using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkPlanner.Api.Data;
using WorkPlanner.Api.Models;

namespace WorkPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProjectsController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .OrderBy(p => p.Name)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                OwnerId = p.OwnerId,
                CreatedAt = p.CreatedAt,
                IsArchived = p.IsArchived
            })
            .ToListAsync();

        return projects;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDto>> GetProject(int id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == id && p.Members.Any(m => m.UserId == userId))
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                OwnerId = p.OwnerId,
                CreatedAt = p.CreatedAt,
                IsArchived = p.IsArchived
            })
            .FirstOrDefaultAsync();

        if (project == null)
        {
            return NotFound();
        }

        return project;
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject(CreateProjectRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Project name is required.");
        }

        var project = new Project
        {
            Name = request.Name.Trim(),
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow
        };

        var ownerMember = new ProjectMember
        {
            Project = project,
            UserId = userId,
            Role = ProjectRole.Owner,
            JoinedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.ProjectMembers.Add(ownerMember);
        await _context.SaveChangesAsync();

        var dto = new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            OwnerId = project.OwnerId,
            CreatedAt = project.CreatedAt,
            IsArchived = project.IsArchived
        };

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, dto);
    }

    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> AddMember(int id, AddProjectMemberRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
        {
            return NotFound();
        }

        var isOwner = project.OwnerId == userId;
        if (!isOwner)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email is required.");
        }

        var memberUser = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (memberUser == null)
        {
            return NotFound("User not found.");
        }

        var exists = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == memberUser.Id);

        if (exists)
        {
            return Conflict("User is already a member of this project.");
        }

        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = id,
            UserId = memberUser.Id,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/members")]
    public async Task<ActionResult<IEnumerable<ProjectMemberDto>>> GetMembers(int id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var hasAccess = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid();
        }

        var members = await _context.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == id)
            .Select(m => new ProjectMemberDto
            {
                UserId = m.UserId,
                Email = m.User.Email ?? string.Empty,
                FirstName = m.User.FirstName,
                LastName = m.User.LastName,
                Role = m.Role
            })
            .ToListAsync();

        return members;
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProject(int id, UpdateProjectRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
        {
            return NotFound();
        }

        if (project.OwnerId != userId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Project name is required.");
        }

        project.Name = request.Name.Trim();
        project.IsArchived = request.IsArchived;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int id, string memberId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
        {
            return NotFound();
        }

        if (project.OwnerId != userId)
        {
            return Forbid();
        }

        var member = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == memberId);

        if (member == null)
        {
            return NotFound();
        }

        if (member.Role == ProjectRole.Owner)
        {
            return BadRequest("Owner cannot be removed from the project.");
        }

        _context.ProjectMembers.Remove(member);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
}

public class AddProjectMemberRequest
{
    public string Email { get; set; } = string.Empty;
}

public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

public class ProjectMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
}

public class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
}
