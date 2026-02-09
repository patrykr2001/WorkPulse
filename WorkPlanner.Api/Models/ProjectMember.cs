namespace WorkPlanner.Api.Models;

public class ProjectMember
{
    public int ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ProjectRole Role { get; set; } = ProjectRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}

public enum ProjectRole
{
    Owner,
    Member
}
