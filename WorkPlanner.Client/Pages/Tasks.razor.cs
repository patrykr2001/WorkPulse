using Microsoft.AspNetCore.Components;
using MudBlazor;
using WorkPlanner.Client.Models;
using WorkPlanner.Client.Services;
using TaskStatus = WorkPlanner.Client.Models.TaskStatus;

namespace WorkPlanner.Client.Pages;

public partial class Tasks : ComponentBase
{
    [Inject] private TaskService TaskService { get; set; } = null!;
    [Inject] private ProjectService ProjectService { get; set; } = null!;
    [Inject] private SprintService SprintService { get; set; } = null!;
    [Inject] private AuthService AuthService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    protected List<Project> Projects { get; private set; } = new();
    protected List<Sprint> Sprints { get; private set; } = new();
    protected List<TaskItem> MyTasks { get; private set; } = new();

    protected int SelectedProjectId { get; set; }
    protected int SelectedSprintId { get; set; }
    protected TaskStatus SelectedStatus { get; set; } = TaskStatus.Todo;

    protected List<TaskItem> FilteredTasks => MyTasks
        .Where(t => SelectedProjectId == 0 || t.ProjectId == SelectedProjectId)
        .Where(t => SelectedSprintId == 0 || t.SprintId == SelectedSprintId)
        .Where(t => t.Status == SelectedStatus)
        .OrderBy(t => t.Order)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        Projects = await ProjectService.GetProjectsAsync();
        await LoadTasksAsync();
    }

    protected async Task OnFiltersChanged()
    {
        await LoadTasksAsync();
    }

    protected async Task LoadTasksAsync()
    {
        var projectId = SelectedProjectId == 0 ? (int?)null : SelectedProjectId;
        var sprintId = SelectedSprintId == 0 ? (int?)null : SelectedSprintId;

        if (projectId.HasValue)
        {
            Sprints = await SprintService.GetSprintsAsync(projectId.Value, includeArchived: false);
        }
        else
        {
            Sprints = new List<Sprint>();
        }

        var tasks = await TaskService.GetTasksAsync(projectId, sprintId);
        var currentUserId = AuthService.CurrentUser?.Id;

        MyTasks = string.IsNullOrWhiteSpace(currentUserId)
            ? new List<TaskItem>()
            : tasks.Where(t => t.AssigneeId == currentUserId).ToList();
    }

    protected async Task OpenNewTaskDialog()
    {
        var dialog = await DialogService.ShowAsync<global::WorkPlanner.Client.Shared.TaskDialog>("New task");
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadTasksAsync();
        }
    }

    protected string GetProjectName(int projectId)
    {
        return Projects.FirstOrDefault(p => p.Id == projectId)?.Name ?? "-";
    }

    protected string GetSprintName(int? sprintId)
    {
        if (!sprintId.HasValue)
        {
            return "-";
        }

        return Sprints.FirstOrDefault(s => s.Id == sprintId.Value)?.Name ?? "-";
    }

    protected async Task OpenTaskDialog(TaskItem task)
    {
        var parameters = new DialogParameters
        {
            ["Task"] = task
        };

        var dialog = await DialogService.ShowAsync<global::WorkPlanner.Client.Shared.TaskDialog>("Task", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadTasksAsync();
        }
    }
}
