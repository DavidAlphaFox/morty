# Morty Project Tasks

## Project Overview
AI-driven development management system based on Claude API with multi-vendor support, SQLite persistence, Kanban, and Gantt visualization.

## Phase 1: Foundation (Completed)
- [x] Create .NET 10 solution structure
- [x] Set up project layers: Core, Infrastructure, Web
- [x] Configure EF Core + SQLite
- [x] Add Serilog logging

## Phase 2: Data Layer (Completed)
- [x] 2.1 Define complete entity models
- [x] 2.2 Run EF Core migrations
- [x] 2.3 Create repository interfaces and implementations

## Phase 3: Core Loop Engine (Completed)
- [x] 3.1 Implement ClaudeClient (process management)
- [x] 3.2 Implement ResponseAnalyzer
- [x] 3.3 Implement CircuitBreaker
- [x] 3.4 Implement RateLimiter
- [x] 3.5 Implement MortyLoopService (BackgroundService)

## Phase 3.1: Per-Project WorkingDirectory (Completed)
- [x] Add WorkingDirectory field to Project entity
- [x] Update MortyLoopService to use per-project working directory
- [x] Auto-create working directory if not exists
- [x] Create migration for WorkingDirectory

## Phase 4: Web API Layer
- [x] 4.1 Create API controllers for Projects, Stories, Iterations
- [x] 4.2 Add SignalR Hub for real-time updates
- [x] 4.3 Configure CORS

## Phase 5: Frontend (Static + API)
- [x] 5.1 Set up static file serving
- [x] 5.2 Implement Kanban board UI
- [ ] 5.3 Implement Gantt chart
- [x] 5.4 Implement Dashboard

## Phase 6: Multi-Vendor Claude Support (New)
- [ ] 6.1 Add Provider entity and migration
- [ ] 6.2 Add ExecutionOutput entity and migration
- [ ] 6.3 Create IClaudeProvider interface
- [ ] 6.4 Implement AnthropicProvider (API-based)
- [ ] 6.5 Implement AzureOpenAIProvider
- [ ] 6.6 Implement ProviderFactory
- [ ] 6.7 Add configuration loader from environment variables
- [ ] 6.8 Update Iteration to track ProviderId
- [ ] 6.9 Update Plan entity (add ProviderId, Type, Output)
- [ ] 6.10 Add Providers API controller
- [ ] 6.11 Update MortyLoopService to use providers

## Phase 7: CLI Entry Point
- [ ] 7.1 Add System.CommandLine
- [ ] 7.2 Configure CLI commands

## Phase 8: Testing
- [ ] 8.1 Unit tests for core services
- [ ] 8.2 Integration tests

## Phase 9: Deployment
- [ ] 9.1 Configure publish profile
- [ ] 9.2 Self-contained single file build

---

## Technical Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 + ASP.NET Core |
| Database | EF Core + SQLite |
| Logging | Serilog |
| Real-time | SignalR |
| Frontend | Static HTML/JS |
| CLI | System.CommandLine |
| Providers | Anthropic, Azure OpenAI, OpenAI |

## Entity Models (Updated)

```
Project (1) ──→ (*) Story (1) ──→ (*) Iteration ──→ (*) ExecutionOutput
                      │                    │
                      ├──→ (*) Plan        └──→ (*) Verification
                      │    (Type: Planning/Execution)
                      └──→ (*) Event

Provider (1) ──→ (*) Iteration
       │            (ProviderId)
       └──→ (*) Plan
       └──→ (*) ExecutionOutput
```

### Kanban Columns
| Column | Status |
|--------|--------|
| Backlog | Pending |
| Planning | Planning |
| In Progress | InProgress |
| Verifying | Verifying |
| Done | Completed |
| Failed | Failed |

## Project Structure (Updated)

```
morty/
├── Morty.slnx
├── TASK.md
├── design/
│   ├── architecture.md
│   └── multi-vendor-support.md
└── src/
    ├── Morty.Core/
    │   ├── Entities/
    │   │   └── Entities.cs
    │   ├── Interfaces/
    │   │   └── Interfaces.cs
    │   ├── Repositories/
    │   │   └── IRepositories.cs
    │   ├── Services/
    │   │   ├── ClaudeClient.cs
    │   │   ├── ResponseAnalyzer.cs
    │   │   ├── CircuitBreaker.cs
    │   │   └── RateLimiter.cs
    │   └── Morty.Core.csproj
    │
    ├── Morty.Infrastructure/
    │   ├── Data/
    │   │   ├── MortyDbContext.cs
    │   │   └── Migrations/
    │   └── Repositories/
    │       └── Repositories.cs
    │   └── Morty.Infrastructure.csproj
    │
    └── Morty.Web/
        ├── Program.cs
        ├── Services/
        │   └── MortyLoopService.cs
        ├── Controllers/
        ├── Hubs/
        ├── wwwroot/
        │   └── index.html
        ├── appsettings.json
        └── Morty.Web.csproj
```

## Implemented Components

### Core Services
- **ClaudeClient**: Wraps `claude -p` process, handles stdin/stdout streaming
- **ResponseAnalyzer**: Analyzes Claude output for completion, errors, changed files
- **CircuitBreaker**: Prevents infinite retry loops (Closed/Open/HalfOpen states)
- **RateLimiter**: Respects API rate limits (requests per minute)
- **MortyLoopService**: BackgroundService that orchestrates the entire loop

### Repositories
- ProjectRepository, StoryRepository, IterationRepository
- PlanRepository, VerificationRepository, StoryEventRepository

### Database
- SQLite with EF Core migrations applied
- Tables: Projects, Stories, Iterations, Plans, Verifications, StoryEvents
