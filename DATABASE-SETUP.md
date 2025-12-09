# Database Setup and EF Core Operations

This document explains how to configure and use the PostgreSQL database backend for persistent task storage in RG.OpenCopilot.

## Overview

RG.OpenCopilot supports two storage backends:
- **In-Memory Storage** (default) - Tasks are stored in memory and lost on application restart
- **PostgreSQL Storage** - Tasks are persisted to a PostgreSQL database

## Configuration

### Using In-Memory Storage (Default)

If no connection string is configured, the application automatically uses in-memory storage. This is suitable for development and testing.

No configuration required - just leave the connection string empty in `appsettings.json`.

### Using PostgreSQL Storage

To enable PostgreSQL storage, configure the connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AgentTaskDatabase": "Host=localhost;Database=opencopilot;Username=postgres;Password=yourpassword"
  }
}
```

Or use environment variables:

```bash
export ConnectionStrings__AgentTaskDatabase="Host=localhost;Database=opencopilot;Username=postgres;Password=yourpassword"
```

For production deployments, use secrets management (Azure Key Vault, AWS Secrets Manager, etc.) instead of storing passwords in configuration files.

## PostgreSQL Database Setup

### 1. Install PostgreSQL

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install postgresql postgresql-contrib
```

**macOS (using Homebrew):**
```bash
brew install postgresql@16
brew services start postgresql@16
```

**Windows:**
Download and install from https://www.postgresql.org/download/windows/

**Docker:**
```bash
docker run --name opencopilot-db \
  -e POSTGRES_PASSWORD=yourpassword \
  -e POSTGRES_DB=opencopilot \
  -p 5432:5432 \
  -d postgres:16
```

### 2. Create Database

```bash
# Connect to PostgreSQL
psql -U postgres

# Create database
CREATE DATABASE opencopilot;

# Create user (optional)
CREATE USER opencopilot_user WITH PASSWORD 'yourpassword';
GRANT ALL PRIVILEGES ON DATABASE opencopilot TO opencopilot_user;
```

## EF Core Migrations

### Initial Setup

The initial migration has already been created. To apply it to your database:

```bash
cd RG.OpenCopilot.PRGenerationAgent.Services
dotnet ef database update --context AgentTaskDbContext
```

This creates the following table:
- `agent_tasks` - Stores agent tasks with their plans, status, and timestamps

### Creating New Migrations

When you modify the domain models (AgentTask, AgentPlan, etc.), create a new migration:

```bash
cd RG.OpenCopilot.PRGenerationAgent.Services

# Create a new migration
dotnet ef migrations add YourMigrationName --context AgentTaskDbContext --output-dir Infrastructure/Persistence/Migrations

# Review the generated migration in Infrastructure/Persistence/Migrations/

# Apply the migration to the database
dotnet ef database update --context AgentTaskDbContext
```

### Listing Migrations

```bash
cd RG.OpenCopilot.PRGenerationAgent.Services
dotnet ef migrations list --context AgentTaskDbContext
```

### Removing the Last Migration

If you made a mistake and haven't applied the migration yet:

```bash
cd RG.OpenCopilot.PRGenerationAgent.Services
dotnet ef migrations remove --context AgentTaskDbContext
```

### Reverting a Migration

To revert to a specific migration:

```bash
cd RG.OpenCopilot.PRGenerationAgent.Services

# Revert to a specific migration
dotnet ef database update PreviousMigrationName --context AgentTaskDbContext

# Revert all migrations (back to empty database)
dotnet ef database update 0 --context AgentTaskDbContext
```

### Generating SQL Scripts

To generate SQL scripts without applying them (useful for production deployments):

```bash
cd RG.OpenCopilot.PRGenerationAgent.Services

# Generate script for all migrations
dotnet ef migrations script --context AgentTaskDbContext --output migration.sql

# Generate script from specific migration to latest
dotnet ef migrations script FromMigration --context AgentTaskDbContext --output migration.sql

# Generate script between two migrations
dotnet ef migrations script FromMigration ToMigration --context AgentTaskDbContext --output migration.sql

# Generate idempotent script (safe to run multiple times)
dotnet ef migrations script --context AgentTaskDbContext --idempotent --output migration.sql
```

## Database Schema

### agent_tasks Table

| Column | Type | Description |
|--------|------|-------------|
| id | varchar(500) | Primary key, format: `{owner}/{repo}/issues/{number}` |
| installation_id | bigint | GitHub App installation ID |
| repository_owner | varchar(255) | Repository owner name |
| repository_name | varchar(255) | Repository name |
| issue_number | integer | Issue number |
| plan | jsonb | AgentPlan stored as JSON (nullable) |
| status | text | Task status (enum stored as string) |
| created_at | timestamp with time zone | Task creation timestamp |
| started_at | timestamp with time zone | Task execution start timestamp (nullable) |
| completed_at | timestamp with time zone | Task completion timestamp (nullable) |

**Indexes:**
- `ix_agent_tasks_installation_id` - For filtering by installation
- `ix_agent_tasks_status` - For filtering by status
- `ix_agent_tasks_created_at` - For sorting by creation time
- `ix_agent_tasks_repository` - Composite index on (repository_owner, repository_name)

### AgentPlan JSON Structure

The `plan` column stores the AgentPlan as JSONB, which includes:
- `ProblemSummary` - Description of the problem
- `Constraints` - List of constraints
- `Steps` - Array of PlanStep objects with Id, Title, Details, Done
- `Checklist` - List of checklist items
- `FileTargets` - List of target files

Example:
```json
{
  "ProblemSummary": "Add login feature",
  "Constraints": ["Use JWT authentication", "Follow OWASP guidelines"],
  "Steps": [
    {
      "Id": "step-1",
      "Title": "Create login endpoint",
      "Details": "Implement POST /api/auth/login",
      "Done": false
    }
  ],
  "Checklist": ["Create API endpoint", "Add tests", "Update documentation"],
  "FileTargets": ["Controllers/AuthController.cs", "Services/AuthService.cs"]
}
```

## Task Querying and Filtering

The PostgreSQL implementation supports:

### Get Task by ID
```csharp
var task = await taskStore.GetTaskAsync("owner/repo/issues/1");
```

### Get Tasks by Installation ID
```csharp
var tasks = await taskStore.GetTasksByInstallationIdAsync(installationId: 123);
```

### Future Query Capabilities

The current implementation provides basic querying. Future enhancements could include:
- Filter by status
- Filter by repository
- Date range queries
- Full-text search on plan summaries
- Pagination support

These can be added by extending the `IAgentTaskStore` interface and implementing additional query methods.

## Task Resumption After Restart

With PostgreSQL storage enabled, tasks are automatically persisted and can be resumed after application restart:

1. Tasks in `PendingPlanning` or `Executing` status remain in the database
2. On restart, the application can query for incomplete tasks
3. Background job processing can resume execution of incomplete tasks

To implement task resumption:
1. Query for tasks with status != `Completed` and != `Failed`
2. Re-enqueue them into the job queue
3. The executor will pick up from the last known state

## Connection Pooling

Npgsql (PostgreSQL provider) uses connection pooling by default. Configure it in the connection string:

```
Host=localhost;Database=opencopilot;Username=postgres;Password=yourpassword;Minimum Pool Size=1;Maximum Pool Size=20
```

## Performance Considerations

1. **JSONB Indexing**: For large deployments, consider adding GIN indexes on the `plan` column for faster JSON queries:
   ```sql
   CREATE INDEX idx_agent_tasks_plan ON agent_tasks USING GIN (plan);
   ```

2. **Connection Limits**: PostgreSQL has a default connection limit (usually 100). Adjust `max_connections` in `postgresql.conf` if needed.

3. **Regular Maintenance**: Run `VACUUM` and `ANALYZE` regularly to maintain performance:
   ```sql
   VACUUM ANALYZE agent_tasks;
   ```

## Backup and Restore

### Backup
```bash
pg_dump -U postgres opencopilot > opencopilot_backup.sql
```

### Restore
```bash
psql -U postgres opencopilot < opencopilot_backup.sql
```

## Troubleshooting

### Cannot connect to database

1. Check PostgreSQL is running:
   ```bash
   sudo systemctl status postgresql  # Linux
   brew services list                # macOS
   ```

2. Verify connection string is correct
3. Check firewall settings
4. Ensure PostgreSQL accepts connections from your host in `pg_hba.conf`

### Migration errors

1. Ensure you're in the correct directory (`RG.OpenCopilot.PRGenerationAgent.Services`)
2. Check that `dotnet ef` tools are installed:
   ```bash
   dotnet tool install --global dotnet-ef --version 10.0.0
   ```
3. Verify the database connection string is valid

### JSON serialization issues

The AgentPlan and related models must be JSON-serializable. If you add new properties:
1. Ensure they have public getters/setters or init accessors
2. Use simple types (string, int, lists) or other serializable types
3. Test serialization/deserialization in unit tests

## Testing

The test suite includes comprehensive tests for the PostgreSQL implementation using SQLite in-memory databases:

```bash
dotnet test --filter "FullyQualifiedName~PostgreSqlAgentTaskStoreTests"
```

These tests verify:
- CRUD operations
- Plan serialization/deserialization
- Timestamp handling
- Query filtering
- Concurrent operations

## Migration from In-Memory to PostgreSQL

To migrate existing tasks from in-memory storage to PostgreSQL:

1. The application doesn't currently support migration as in-memory tasks are lost on restart
2. For production, configure PostgreSQL from the start
3. Future enhancement: Export/import functionality for task migration

## References

- [EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [Npgsql Documentation](https://www.npgsql.org/doc/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
