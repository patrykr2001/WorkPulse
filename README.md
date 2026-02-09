# WorkPlanner - Work time tracking app

App for tracking time, tasks, and generating work summaries.

## Tech stack
- **Backend**: ASP.NET Core Web API (.NET 10) + SQLite + EF Core
- **Frontend**: Blazor WebAssembly (.NET 10)

## Project structure
```
WorkPlanner/
├── WorkPlanner.Api/           # ASP.NET Core Web API
│   ├── Controllers/           # API controllers
│   ├── Data/                  # DbContext
│   ├── Migrations/            # EF Core migrations
│   ├── Models/                # Entities
│   └── workplanner.db         # SQLite database (local)
├── WorkPlanner.Client/        # Blazor WebAssembly
│   ├── Models/                # Client models
│   ├── Services/              # HTTP services
│   └── Pages/                 # Blazor components
└── WorkPlanner.sln            # Solution
```

## Running locally

### 1. Run API (port 7191 HTTPS)
```bash
cd WorkPlanner.Api
dotnet run
```

### 2. Run Client (port 7127 HTTPS)
```bash
cd WorkPlanner.Client
dotnet run
```

### 3. Run both
```bash
dotnet run --project WorkPlanner.Api &
dotnet run --project WorkPlanner.Client
```

## API endpoints

### Tasks
- `GET /api/tasks?projectId={id}&sprintId={id?}` - List tasks in project/sprint
- `GET /api/tasks/{id}` - Task details
- `POST /api/tasks` - Create task
- `PUT /api/tasks/{id}` - Update task
- `PUT /api/tasks/{id}/move` - Move task and set priority
- `DELETE /api/tasks/{id}` - Delete task

### Projects
- `GET /api/projects` - List user projects
- `GET /api/projects/{id}` - Project details
- `POST /api/projects` - Create project
- `POST /api/projects/{id}/members` - Add member
- `DELETE /api/projects/{id}/members/{userId}` - Remove member

### Sprints
- `GET /api/projects/{projectId}/sprints?includeArchived=false` - List sprints
- `POST /api/projects/{projectId}/sprints` - Create sprint
- `PUT /api/projects/{projectId}/sprints/{sprintId}` - Update sprint
- `POST /api/projects/{projectId}/sprints/{sprintId}/activate` - Set active sprint
- `POST /api/projects/{projectId}/sprints/{sprintId}/archive` - Archive sprint

### Work entries
- `GET /api/workentries` - List entries
- `GET /api/workentries/by-task/{taskId}` - Entries for task
- `POST /api/workentries` - Add entry
- `PUT /api/workentries/{id}` - Update entry
- `DELETE /api/workentries/{id}` - Delete entry

### Summaries
- `GET /api/summaries/daily?date=YYYY-MM-DD` - Daily summary
- `GET /api/summaries/weekly?weekStart=YYYY-MM-DD` - Weekly summary

## Database
SQLite database (`workplanner.db`) is created automatically on first API run.

## Backlog and Kanban
- Backlog shows tasks without a sprint (`SprintId = null`, `Status = Backlog`).
- Kanban shows tasks for the selected sprint in TODO / InProgress / Done columns.
- Priority is calculated per column (`Order` field).

## Migrations
```bash
# Add a new migration
dotnet ef migrations add MigrationName --project WorkPlanner.Api

# Update database
dotnet ef database update --project WorkPlanner.Api
```
