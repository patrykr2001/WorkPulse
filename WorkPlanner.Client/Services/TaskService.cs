using System.Net.Http.Json;
using WorkPlanner.Client.Models;

namespace WorkPlanner.Client.Services;

public class TaskService
{
    private readonly HttpClient _httpClient;

    public TaskService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TaskItem>> GetTasksAsync(int? projectId = null, int? sprintId = null)
    {
        var url = "api/tasks";
        if (projectId.HasValue)
        {
            url += $"?projectId={projectId.Value}";
        }

        if (sprintId.HasValue)
        {
            url += projectId.HasValue ? $"&sprintId={sprintId.Value}" : $"?sprintId={sprintId.Value}";
        }
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return new List<TaskItem>();
        }

        return await response.Content.ReadFromJsonAsync<List<TaskItem>>() ?? new List<TaskItem>();
    }

    public async Task<TaskItem?> GetTaskAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<TaskItem>($"api/tasks/{id}");
    }

    public async Task<TaskItem> CreateTaskAsync(TaskItem task)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tasks", new CreateTaskRequest
        {
            ProjectId = task.ProjectId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            AssigneeId = task.AssigneeId,
            SprintId = task.SprintId,
            Order = task.Order
        });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? $"Create task failed: {response.StatusCode}"
                : body);
        }

        return await response.Content.ReadFromJsonAsync<TaskItem>() ?? task;
    }

    public async Task UpdateTaskAsync(TaskItem task)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{task.Id}", new UpdateTaskRequest
        {
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            AssigneeId = task.AssigneeId,
            SprintId = task.SprintId,
            Order = task.Order
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task MoveTaskAsync(int taskId, MoveTaskRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{taskId}/move", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTaskAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{id}");
        response.EnsureSuccessStatusCode();
    }
}
