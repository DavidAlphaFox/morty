# Morty Architecture Design

## System Overview

Morty is an AI-driven development management system that orchestrates Claude CLI to automatically implement user stories, track progress via Kanban/Gantt, and persist all data in SQLite.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Morty.Web                             │
│                  (ASP.NET Core API)                         │
│                                                             │
│  ┌─────────────────┐  ┌─────────────────────────────┐   │
│  │ Background      │  │ Web API + SignalR            │   │
│  │ Service         │──│ (Controllers + Hubs)         │   │
│  │ (MortyLoop)    │  │                              │   │
│  └────────┬────────┘  └──────────────┬──────────────┘   │
│           │                           │                   │
│           │                    ┌──────▼──────┐            │
│           │                    │ Static UI   │            │
│           │                    │ (HTML/JS)   │            │
│           │                    └─────────────┘            │
└───────────┼───────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────┐
│              Morty.Infrastructure                           │
│                  (EF Core + SQLite)                         │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ DbContext, Repositories, Migrations                   │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────┐
│                    SQLite Database                          │
└─────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────┐
│                    claude -p (External Process)             │
└─────────────────────────────────────────────────────────────┘
```

## Component Design

### 1. MortyLoopService (BackgroundService)

The core orchestration engine running as a background service.

**Responsibilities:**
- Load stories from database in priority order
- Call Claude CLI for each story iteration
- Analyze Claude's response to determine next action
- Update story status based on results
- Trigger verification tests
- Broadcast updates via SignalR

**State Machine:**
```
Pending → Planning → InProgress → Verifying → Completed
                       ↓                      ↓
                    Failed ←─────────────────┘
```

### 2. ClaudeClient

Wrapper around `System.Diagnostics.Process` to interact with Claude CLI.

**Features:**
- Start `claude -p` process
- Stream stdin/stdout
- Handle timeout and cancellation
- Parse JSON responses

### 3. ResponseAnalyzer

Analyzes Claude's output to determine:
- Whether implementation is complete
- Whether tests pass
- Whether code compiles
- What files were changed

### 4. CircuitBreaker

Prevents infinite retry loops when Claude repeatedly fails.

**States:**
- Closed: Normal operation
- Open: Recent failures exceed threshold
- Half-Open: Testing if recovery is possible

### 5. RateLimiter

Respects Claude API rate limits.

**Configuration:**
- Requests per minute
- Requests per day
- Token limit (future)

## Database Schema

### Projects
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Auto-increment |
| Name | TEXT | Project name |
| PrdJson | TEXT | Full PRD content |
| CreatedAt | TEXT | ISO8601 timestamp |

### Stories
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Auto-increment |
| ProjectId | INTEGER FK | Reference to Project |
| StoryId | TEXT | User story ID from PRD |
| Title | TEXT | Story title |
| Priority | TEXT | High/Medium/Low |
| Status | TEXT | Current status |
| CreatedAt | TEXT | ISO8601 timestamp |
| CompletedAt | TEXT | When completed |

### Iterations
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Auto-increment |
| StoryId | INTEGER FK | Reference to Story |
| IterationNum | INTEGER | Iteration number |
| StartedAt | TEXT | ISO8601 timestamp |
| CompletedAt | TEXT | ISO8601 timestamp |
| DurationMs | INTEGER | Execution time |
| CostUsd | TEXT | Decimal cost |
| Output | TEXT | Claude's output |

### Plans
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Auto-increment |
| StoryId | INTEGER FK | Reference to Story |
| PlanContent | TEXT | Implementation plan |
| CreatedAt | TEXT | ISO8601 timestamp |

### Verifications
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Auto-increment |
| IterationId | INTEGER FK | Reference to Iteration |
| Type | TEXT | test/build/lint |
| Passed | INTEGER | 0 or 1 |
| Output | TEXT | Verification output |
| CreatedAt | TEXT | ISO8601 timestamp |

### StoryEvents
| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Auto-increment |
| StoryId | INTEGER FK | Reference to Story |
| EventType | TEXT | status_changed, etc. |
| Timestamp | TEXT | ISO8601 timestamp |
| DataJson | TEXT | Event data |

## API Design

### REST Endpoints

#### Projects
- `GET /api/projects` - List all projects
- `GET /api/projects/{id}` - Get project details
- `POST /api/projects` - Create project
- `DELETE /api/projects/{id}` - Delete project

#### Stories
- `GET /api/projects/{projectId}/stories` - List stories
- `GET /api/stories/{id}` - Get story details
- `PATCH /api/stories/{id}` - Update story status

#### Iterations
- `GET /api/stories/{storyId}/iterations` - List iterations

#### Dashboard
- `GET /api/dashboard/stats` - Get project statistics

### SignalR Hub

**Hub: MortyHub**

Methods:
- `JoinProject(int projectId)` - Subscribe to project updates
- `OnStoryUpdated(Story story)` - Broadcast story changes
- `OnIterationComplete(Iteration iteration)` - Broadcast iteration results
- `OnProjectStatsUpdated(Stats stats)` - Broadcast dashboard updates

## Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=morty.db"
  },
  "Morty": {
    "ClaudeCommand": "claude",
    "ClaudeArgs": "-p",
    "WorkingDirectory": "./workspace",
    "MaxIterations": 10,
    "TimeoutMinutes": 15,
    "RateLimit": {
      "RequestsPerMinute": 10
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "ResetMinutes": 5
    }
  }
}
```

## Deployment

### Build
```bash
dotnet publish -c Release --self-contained -r linux-x64
```

### Run
```bash
./Morty.Web --urls "http://localhost:5000"
```

## Future Considerations

### Frontend Options
1. **Current**: Static HTML/JS calling API
2. **Future**: React/Vue SPA
3. **Future**: Blazor WASM

### Scaling
- Multiple project support
- Worker role separation
- Redis for SignalR scaling

### Monitoring
- Health checks
- Metrics (OpenTelemetry)
- Distributed tracing
