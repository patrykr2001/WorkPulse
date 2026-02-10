using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WorkPlanner.Client.Models;
using WorkPlanner.Client.Services;
using TaskStatus = WorkPlanner.Client.Models.TaskStatus;

namespace WorkPlanner.Client.Pages;

public partial class Kanban : ComponentBase, IDisposable
{
    protected const string AssigneeFilterAll = "all";
    protected const string AssigneeFilterMe = "me";
    protected const string AssigneeFilterUnassigned = "unassigned";
    [Inject] private ProjectService ProjectService { get; set; } = null!;
    [Inject] private SprintService SprintService { get; set; } = null!;
    [Inject] private TaskService TaskService { get; set; } = null!;
    [Inject] private AuthService AuthService { get; set; } = null!;
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
    protected List<ProjectMember> Members { get; private set; } = new();
    protected List<TaskStatus> EnabledStatuses { get; private set; } = new();
    protected string SelectedAssigneeId { get; set; } = AssigneeFilterAll;
    protected int SelectedSprintId
    {
        get => _selectedSprintId;
        set
        {
            if (_selectedSprintId == value)
            {
                return;
            }

            _selectedSprintId = value;
            _ = SaveSelectedSprintAsync();
        }
    }

    private int _selectedSprintId;
    protected List<TaskItem> SprintTasks { get; private set; } = new();
    private TaskItem? _draggedTask;
    protected bool IsUnauthorized { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        Projects = await ProjectService.GetProjectsAsync();
        IsUnauthorized = !AuthService.IsAuthenticated;

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
            await LoadSprintsAsync();
        }

        AuthService.OnAuthStateChanged += HandleAuthChanged;
    }

    public void Dispose()
    {
        AuthService.OnAuthStateChanged -= HandleAuthChanged;
    }

    private async void HandleAuthChanged()
    {
        IsUnauthorized = !AuthService.IsAuthenticated;
        if (IsUnauthorized)
        {
            Projects = new List<Project>();
            Sprints = new List<Sprint>();
            Members = new List<ProjectMember>();
            SprintTasks = new List<TaskItem>();
            SelectedProjectId = 0;
            SelectedSprintId = 0;
            StateHasChanged();
            return;
        }

        Projects = await ProjectService.GetProjectsAsync();
        if (SelectedProjectId == 0 && Projects.Count > 0)
        {
            SelectedProjectId = Projects[0].Id;
        }
        await LoadSprintsAsync();
    }

    private async Task SaveSelectedProjectAsync()
    {
        if (SelectedProjectId <= 0)
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync("localStorage.setItem", SelectedProjectStorageKey, SelectedProjectId.ToString());
        await LoadSprintsAsync();
    }

    private async Task LoadSprintsAsync()
    {
        Sprints = await SprintService.GetSprintsAsync(SelectedProjectId, includeArchived: false);
        Members = await ProjectService.GetMembersAsync(SelectedProjectId);
        var project = Projects.FirstOrDefault(p => p.Id == SelectedProjectId);
        EnabledStatuses = GetEnabledStatuses(project?.EnabledStatuses);
        if (EnabledStatuses.Count == 0)
        {
            EnabledStatuses = new List<TaskStatus>
            {
                TaskStatus.Todo,
                TaskStatus.InProgress,
                TaskStatus.Done
            };
        }
        SelectedAssigneeId = AssigneeFilterAll;

        var activeSprint = Sprints.FirstOrDefault(s => s.IsActive);
        if (activeSprint != null)
        {
            SelectedSprintId = activeSprint.Id;
        }
        else if (Sprints.Count > 0)
        {
            SelectedSprintId = Sprints[0].Id;
        }
        else
        {
            SelectedSprintId = 0;
        }

        await LoadSprintTasksAsync();
    }

    private async Task SaveSelectedSprintAsync()
    {
        if (SelectedSprintId <= 0)
        {
            SprintTasks = new List<TaskItem>();
            StateHasChanged();
            return;
        }

        await LoadSprintTasksAsync();
    }

    private async Task LoadSprintTasksAsync()
    {
        if (SelectedSprintId <= 0)
        {
            SprintTasks = new List<TaskItem>();
            return;
        }

        SprintTasks = await TaskService.GetTasksAsync(SelectedProjectId, SelectedSprintId);
        SprintTasks = SprintTasks
            .Where(t => EnabledStatuses.Contains(t.Status))
            .ToList();
        IsUnauthorized = !AuthService.IsAuthenticated;
        StateHasChanged();
    }

    protected IEnumerable<TaskItem> FilteredColumnTasks(TaskStatus status)
    {
        return SprintTasks
            .Where(t => t.Status == status)
            .Where(ApplyAssigneeFilter)
            .OrderBy(t => t.Order);
    }

    protected string GetStatusLabel(TaskStatus status)
    {
        return status switch
        {
            TaskStatus.InProgress => "In Progress",
            TaskStatus.Refine => "Refine",
            TaskStatus.Review => "Review",
            _ => status.ToString()
        };
    }

    private static List<TaskStatus> GetEnabledStatuses(string? statuses)
    {
        if (string.IsNullOrWhiteSpace(statuses))
        {
            return new List<TaskStatus>();
        }

        var parsed = statuses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<TaskStatus>(s, true, out var value) ? value : (TaskStatus?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        if (!parsed.Contains(TaskStatus.Todo))
        {
            parsed.Add(TaskStatus.Todo);
        }
        if (!parsed.Contains(TaskStatus.InProgress))
        {
            parsed.Add(TaskStatus.InProgress);
        }
        if (!parsed.Contains(TaskStatus.Done))
        {
            parsed.Add(TaskStatus.Done);
        }

        var order = new[]
        {
            TaskStatus.Refine,
            TaskStatus.Todo,
            TaskStatus.InProgress,
            TaskStatus.Review,
            TaskStatus.Done
        };

        return parsed.OrderBy(status => Array.IndexOf(order, status)).ToList();
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

    protected string GetMemberLabel(ProjectMember member)
    {
        var name = member.FullName?.Trim();
        var email = member.Email?.Trim();

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
        {
            return $"{name} ({email})";
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(email) ? member.UserId : email;
    }

    protected void OnDragStart(TaskItem task)
    {
        _draggedTask = task;
    }

    protected async Task OnDropToColumn(TaskStatus status)
    {
        if (_draggedTask == null)
        {
            return;
        }

        if (IsUnauthorized)
        {
            return;
        }

        var tasksInColumn = SprintTasks.Where(t => t.Status == status).ToList();
        var request = new MoveTaskRequest
        {
            SprintId = SelectedSprintId,
            Status = status,
            NewOrder = tasksInColumn.Count
        };

        await TaskService.MoveTaskAsync(_draggedTask.Id, request);
        await LoadSprintTasksAsync();
        _draggedTask = null;
    }
}
