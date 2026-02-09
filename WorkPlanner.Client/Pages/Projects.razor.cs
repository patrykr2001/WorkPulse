using Microsoft.AspNetCore.Components;
using WorkPlanner.Client.Models;
using WorkPlanner.Client.Services;

namespace WorkPlanner.Client.Pages;

public partial class Projects : ComponentBase
{
    [Inject] private ProjectService ProjectService { get; set; } = null!;

    protected List<Project> ProjectList { get; private set; } = new();
    protected List<Project> FilteredProjects => ShowArchived
        ? ProjectList
        : ProjectList.Where(p => !p.IsArchived).ToList();

    protected string NewProjectName { get; set; } = string.Empty;
    protected bool ShowArchived { get; set; }

    protected int? EditingProjectId { get; set; }
    protected string EditProjectName { get; set; } = string.Empty;

    protected Project? SelectedProject { get; set; }
    protected List<ProjectMember> Members { get; private set; } = new();
    protected string NewMemberEmail { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadProjectsAsync();
    }

    protected async Task LoadProjectsAsync()
    {
        ProjectList = await ProjectService.GetProjectsAsync();
        StateHasChanged();
    }

    protected async Task CreateProject()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            return;
        }

        await ProjectService.CreateProjectAsync(new CreateProjectRequest { Name = NewProjectName.Trim() });
        NewProjectName = string.Empty;
        await LoadProjectsAsync();
    }

    protected void StartEdit(Project project)
    {
        EditingProjectId = project.Id;
        EditProjectName = project.Name;
    }

    protected void CancelEdit()
    {
        EditingProjectId = null;
        EditProjectName = string.Empty;
    }

    protected async Task SaveProject(Project project)
    {
        if (EditingProjectId != project.Id)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(EditProjectName) ? project.Name : EditProjectName.Trim();
        await ProjectService.UpdateProjectAsync(project.Id, new UpdateProjectRequest
        {
            Name = name,
            IsArchived = project.IsArchived
        });

        EditingProjectId = null;
        EditProjectName = string.Empty;
        await LoadProjectsAsync();
    }

    protected async Task ToggleArchive(Project project)
    {
        await ProjectService.UpdateProjectAsync(project.Id, new UpdateProjectRequest
        {
            Name = project.Name,
            IsArchived = !project.IsArchived
        });

        await LoadProjectsAsync();
    }

    protected async Task SelectProject(Project project)
    {
        SelectedProject = project;
        Members = await ProjectService.GetMembersAsync(project.Id);
        NewMemberEmail = string.Empty;
    }

    protected async Task AddMember()
    {
        if (SelectedProject == null || string.IsNullOrWhiteSpace(NewMemberEmail))
        {
            return;
        }

        await ProjectService.AddMemberAsync(SelectedProject.Id, new AddProjectMemberRequest
        {
            Email = NewMemberEmail.Trim()
        });

        NewMemberEmail = string.Empty;
        Members = await ProjectService.GetMembersAsync(SelectedProject.Id);
    }

    protected async Task RemoveMember(ProjectMember member)
    {
        if (SelectedProject == null)
        {
            return;
        }

        await ProjectService.RemoveMemberAsync(SelectedProject.Id, member.UserId);
        Members = await ProjectService.GetMembersAsync(SelectedProject.Id);
    }
}
