using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using WorkPlanner.Client.Models;
using WorkPlanner.Client.Services;
using TaskStatus = WorkPlanner.Client.Models.TaskStatus;

namespace WorkPlanner.Client.Pages;

public partial class Backlog : ComponentBase
{
    protected const string AssigneeFilterAll = "all";
    protected const string AssigneeFilterMe = "me";
    protected const string AssigneeFilterUnassigned = "unassigned";
    [Inject] private ProjectService ProjectService { get; set; } = null!;
    [Inject] private SprintService SprintService { get; set; } = null!;
    [Inject] private TaskService TaskService { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    private const string SelectedProjectStorageKey = "workplanner.selectedProjectId";
    protected List<Project> Projects { get; private set; } = new();
    protected int SelectedProjectId
    {
        get => _selectedProjectId;
        set
        {
            if (_selectedProjectId == value)
            {
                return;
            }

            _selectedProjectId = value;
            _ = SaveSelectedProjectAsync();
        }
    }

    private int _selectedProjectId;

    protected List<Sprint> Sprints { get; private set; } = new();
    protected List<TaskItem> BacklogTasks { get; private set; } = new();
    protected Dictionary<int, List<TaskItem>> SprintTasks { get; private set; } = new();
    protected List<ProjectMember> Members { get; private set; } = new();
    protected string SelectedAssigneeId { get; set; } = AssigneeFilterAll;
    private TaskItem? _draggedTask;
    protected bool IsUnauthorized { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        Projects = await ProjectService.GetProjectsAsync();
        IsUnauthorized = Projects.Count == 0;

        var storedProjectId = await JsRuntime.InvokeAsync<string>("localStorage.getItem", SelectedProjectStorageKey);
        if (int.TryParse(storedProjectId, out var projectId) && Projects.Any(p => p.Id == projectId))
        {
            SelectedProjectId = projectId;
        }
        else if (Projects.Count > 0)
        {
            SelectedProjectId = Projects[0].Id;
            await JsRuntime.InvokeVoidAsync("localStorage.setItem", SelectedProjectStorageKey, SelectedProjectId.ToString());
        }

        if (SelectedProjectId > 0)
        {
            await LoadSprintDataAsync();
        }
    }

    private async Task SaveSelectedProjectAsync()
    {
        if (SelectedProjectId <= 0)
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync("localStorage.setItem", SelectedProjectStorageKey, SelectedProjectId.ToString());
        await LoadSprintDataAsync();
    }

    [Inject] private AuthService AuthService { get; set; } = null!;

    protected IEnumerable<TaskItem> FilteredBacklogTasks => BacklogTasks
        .Where(ApplyAssigneeFilter)
        .OrderBy(t => t.Order);

    protected IEnumerable<TaskItem> FilteredSprintTasks(int sprintId)
    {
        if (!SprintTasks.TryGetValue(sprintId, out var tasks))
        {
            return Enumerable.Empty<TaskItem>();
        }

        return tasks.Where(ApplyAssigneeFilter).OrderBy(t => t.Order);
    }

    private bool ApplyAssigneeFilter(TaskItem task)
    {
        return SelectedAssigneeId switch
        {
            AssigneeFilterAll => true,
            AssigneeFilterMe => task.AssigneeId == AuthService.CurrentUser?.Id,
            AssigneeFilterUnassigned => string.IsNullOrWhiteSpace(task.AssigneeId),
            _ => task.AssigneeId == SelectedAssigneeId
        };
    }

    private async Task LoadSprintDataAsync()
    {
        Sprints = await SprintService.GetSprintsAsync(SelectedProjectId, includeArchived: false);
        BacklogTasks = await TaskService.GetTasksAsync(SelectedProjectId, sprintId: null);
        IsUnauthorized = Projects.Count == 0 && BacklogTasks.Count == 0 && Sprints.Count == 0;
        Members = await ProjectService.GetMembersAsync(SelectedProjectId);
        SelectedAssigneeId = AssigneeFilterAll;
        SprintTasks = new Dictionary<int, List<TaskItem>>();

        foreach (var sprint in Sprints)
        {
            var tasks = await TaskService.GetTasksAsync(SelectedProjectId, sprint.Id);
            SprintTasks[sprint.Id] = tasks;
        }

        StateHasChanged();
    }

    protected void OnDragStart(TaskItem task)
    {
        _draggedTask = task;
    }


    protected async Task OnDropToBacklog()
    {
        if (_draggedTask == null)
        {
            return;
        }

        if (IsUnauthorized)
        {
            return;
        }

        var request = new MoveTaskRequest
        {
            SprintId = null,
            Status = TaskStatus.Backlog,
            NewOrder = BacklogTasks.Count
        };

        await TaskService.MoveTaskAsync(_draggedTask.Id, request);
        await LoadSprintDataAsync();
        _draggedTask = null;
    }

    protected async Task OnDropToSprint(int sprintId)
    {
        if (_draggedTask == null)
        {
            return;
        }

        if (IsUnauthorized)
        {
            return;
        }

        if (!SprintTasks.TryGetValue(sprintId, out var tasks))
        {
            tasks = new List<TaskItem>();
        }

        var request = new MoveTaskRequest
        {
            SprintId = sprintId,
            Status = TaskStatus.Todo,
            NewOrder = tasks.Count
        };

        await TaskService.MoveTaskAsync(_draggedTask.Id, request);
        await LoadSprintDataAsync();
        _draggedTask = null;
    }
}
