using Microsoft.AspNetCore.Components;
using MudBlazor;
using WorkPlanner.Client.Models;
using WorkPlanner.Client.Services;
using WorkPlanner.Client.Shared;

namespace WorkPlanner.Client.Pages;

public partial class Projects : ComponentBase
{
    [Inject] private ProjectService ProjectService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    protected List<Project> ProjectList { get; private set; } = new();
    protected List<Project> FilteredProjects => ProjectList
        .Where(p => ShowArchived || !p.IsArchived)
        .Where(p => string.IsNullOrWhiteSpace(SearchText)
            || p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
        .ToList();

    protected string NewProjectName { get; set; } = string.Empty;
    protected string NewProjectStatuses { get; set; } = string.Empty;
    protected bool ShowArchived { get; set; }
    protected string SearchText { get; set; } = string.Empty;

    protected List<Models.TaskStatus> NewProjectStatusSelections { get; set; } = new();

    protected List<Models.TaskStatus> ProjectStatusOptions { get; } = new()
    {
        Models.TaskStatus.Refine,
        Models.TaskStatus.Todo,
        Models.TaskStatus.InProgress,
        Models.TaskStatus.Review,
        Models.TaskStatus.Done
    };

    protected Project? SelectedProject { get; set; }
    protected List<ProjectMember> Members { get; private set; } = new();
    protected string NewMemberEmail { get; set; } = string.Empty;

    private bool _isCreating;

    protected override async Task OnInitializedAsync()
    {
        await LoadProjectsAsync();
    }

    protected async Task LoadProjectsAsync()
    {
        try
        {
            ProjectList = await ProjectService.GetProjectsAsync();
            foreach (var project in ProjectList)
            {
                if (string.IsNullOrWhiteSpace(project.EnabledStatuses))
                {
                    project.EnabledStatuses = "Todo,InProgress,Done";
                }
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load projects: {ex.Message}", Severity.Error);
        }
    }

    protected async Task CreateProject()
    {
        if (_isCreating)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            Snackbar.Add("Project name is required.", Severity.Warning);
            return;
        }

        var statuses = NormalizeStatuses(NewProjectStatusSelections);
        if (string.IsNullOrWhiteSpace(statuses))
        {
            statuses = "Todo,InProgress,Done";
        }

        _isCreating = true;
        try
        {
            await ProjectService.CreateProjectAsync(new CreateProjectRequest
            {
                Name = NewProjectName.Trim(),
                EnabledStatuses = statuses
            });

            NewProjectName = string.Empty;
            NewProjectStatuses = string.Empty;
            NewProjectStatusSelections = new List<Models.TaskStatus>();
            Snackbar.Add("Project created.", Severity.Success);
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to create project: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isCreating = false;
        }
    }

    protected async Task OpenCreateProjectDialog()
    {
        var parameters = new DialogParameters
        {
            ["OnSaved"] = EventCallback.Factory.Create(this, LoadProjectsAsync)
        };

        var dialog = await DialogService.ShowAsync<WorkPlanner.Client.Shared.ProjectCreateDialog>(
            "New project",
            parameters,
            new DialogOptions
            {
                MaxWidth = MaxWidth.Medium,
                FullWidth = true
            });

        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await LoadProjectsAsync();
        }
    }

    protected async Task OpenEditProjectDialog(Project project)
    {
        var parameters = new DialogParameters
        {
            ["Project"] = project,
            ["OnSaved"] = EventCallback.Factory.Create(this, LoadProjectsAsync)
        };

        var dialog = await DialogService.ShowAsync<WorkPlanner.Client.Shared.ProjectCreateDialog>(
            "Edit project",
            parameters,
            new DialogOptions
            {
                MaxWidth = MaxWidth.Medium,
                FullWidth = true
            });

        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await LoadProjectsAsync();
        }
    }

    protected async Task ToggleArchive(Project project)
    {
        var statuses = NormalizeStatuses(ParseStatuses(project.EnabledStatuses));
        if (string.IsNullOrWhiteSpace(statuses))
        {
            statuses = "Todo,InProgress,Done";
        }

        try
        {
            await ProjectService.UpdateProjectAsync(project.Id, new UpdateProjectRequest
            {
                Name = project.Name,
                IsArchived = !project.IsArchived,
                EnabledStatuses = statuses
            });

            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to update project: {ex.Message}", Severity.Error);
        }
    }

    protected async Task SelectProject(Project project)
    {
        SelectedProject = project;
        try
        {
            Members = await ProjectService.GetMembersAsync(project.Id);
            NewMemberEmail = string.Empty;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load members: {ex.Message}", Severity.Error);
        }
    }

    protected async Task AddMember()
    {
        if (SelectedProject == null || string.IsNullOrWhiteSpace(NewMemberEmail))
        {
            return;
        }

        try
        {
            await ProjectService.AddMemberAsync(SelectedProject.Id, new AddProjectMemberRequest
            {
                Email = NewMemberEmail.Trim()
            });

            NewMemberEmail = string.Empty;
            Members = await ProjectService.GetMembersAsync(SelectedProject.Id);
            Snackbar.Add("Member added.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to add member: {ex.Message}", Severity.Error);
        }
    }

    protected async Task RemoveMember(ProjectMember member)
    {
        if (SelectedProject == null)
        {
            return;
        }

        try
        {
            await ProjectService.RemoveMemberAsync(SelectedProject.Id, member.UserId);
            Members = await ProjectService.GetMembersAsync(SelectedProject.Id);
            Snackbar.Add("Member removed.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to remove member: {ex.Message}", Severity.Error);
        }
    }

    protected string GetOwnerLabel(Project project)
    {
        var name = string.Join(" ", new[] { project.OwnerFirstName, project.OwnerLastName }
            .Where(n => !string.IsNullOrWhiteSpace(n))).Trim();
        var email = project.OwnerEmail?.Trim();

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
        {
            return $"{name} ({email})";
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(email) ? project.OwnerId : email;
    }

    protected string GetProjectStatuses(Project project)
    {
        var normalized = NormalizeStatuses(project.EnabledStatuses ?? string.Empty);
        return string.IsNullOrWhiteSpace(normalized) ? "Todo,InProgress,Done" : normalized;
    }

    private static string NormalizeStatuses(string statuses)
    {
        if (string.IsNullOrWhiteSpace(statuses))
        {
            return string.Empty;
        }

        var order = new[]
        {
            Models.TaskStatus.Refine.ToString(),
            Models.TaskStatus.Todo.ToString(),
            Models.TaskStatus.InProgress.ToString(),
            Models.TaskStatus.Review.ToString(),
            Models.TaskStatus.Done.ToString()
        };

        var allowed = new HashSet<string>(order, StringComparer.OrdinalIgnoreCase);
        var normalized = statuses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => allowed.Contains(s))
            .Select(s => order.First(o => o.Equals(s, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(',', normalized);
    }

    private static string NormalizeStatuses(IEnumerable<Models.TaskStatus> statuses)
    {
        var normalized = statuses
            .Where(status => status != Models.TaskStatus.Backlog)
            .Distinct()
            .Select(status => status.ToString())
            .ToList();

        return NormalizeStatuses(string.Join(',', normalized));
    }

    private static List<Models.TaskStatus> ParseStatuses(string? statuses)
    {
        if (string.IsNullOrWhiteSpace(statuses))
        {
            return new List<Models.TaskStatus>();
        }

        return statuses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<Models.TaskStatus>(s, true, out var value) ? value : (Models.TaskStatus?)null)
            .Where(v => v.HasValue && v.Value != Models.TaskStatus.Backlog)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();
    }

    protected string GetStatusLabel(Models.TaskStatus status)
    {
        return status switch
        {
            Models.TaskStatus.InProgress => "In Progress",
            Models.TaskStatus.Refine => "Refine",
            Models.TaskStatus.Review => "Review",
            _ => status.ToString()
        };
    }
}
