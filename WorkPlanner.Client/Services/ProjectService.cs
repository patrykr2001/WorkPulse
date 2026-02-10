using System.Net.Http.Json;
using WorkPlanner.Client.Models;

namespace WorkPlanner.Client.Services;

public class ProjectService
{
    private readonly HttpClient _httpClient;

    public ProjectService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Project>> GetProjectsAsync()
    {
        var response = await _httpClient.GetAsync("api/projects");
        if (!response.IsSuccessStatusCode)
        {
            return new List<Project>();
        }

        return await response.Content.ReadFromJsonAsync<List<Project>>() ?? new List<Project>();
    }

    public async Task<Project?> GetProjectAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/projects/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<Project>();
    }

    public async Task<Project> CreateProjectAsync(CreateProjectRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/projects", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? $"Create project failed: {response.StatusCode}"
                : body);
        }

        return await response.Content.ReadFromJsonAsync<Project>() ?? new Project();
    }

    public async Task UpdateProjectAsync(int id, UpdateProjectRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/projects/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<ProjectMember>> GetMembersAsync(int projectId)
    {
        var response = await _httpClient.GetAsync($"api/projects/{projectId}/members");
        if (!response.IsSuccessStatusCode)
        {
            return new List<ProjectMember>();
        }

        return await response.Content.ReadFromJsonAsync<List<ProjectMember>>() ?? new List<ProjectMember>();
    }

    public async Task AddMemberAsync(int projectId, AddProjectMemberRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/projects/{projectId}/members", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveMemberAsync(int projectId, string userId)
    {
        var response = await _httpClient.DeleteAsync($"api/projects/{projectId}/members/{userId}");
        response.EnsureSuccessStatusCode();
    }
}
