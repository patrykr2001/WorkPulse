namespace WorkPlanner.Api.Models;

public class Sprint
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Order { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
