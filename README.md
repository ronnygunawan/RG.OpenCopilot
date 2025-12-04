# RG.OpenCopilot (GitHub Enterprise Coding Agent)

RG.OpenCopilot is a C#/.NET 10 solution that aims to provide a GitHub Enterprise–hosted coding agent similar to the GitHub Copilot Coding Agent (GCCA).

The agent is triggered by labeling issues with `copilot-assisted`. It analyzes the issue, optional instructions markdown, and the repository, creates a detailed plan using a premium model, then hands execution to cheaper models that modify code, run tests, and open pull requests.

## Current Status

✅ **POC Implemented** - The proof of concept is complete with:
- GitHub webhook handler for `issues` events
- Webhook signature validation (HMAC-SHA256)
- AgentTask creation and management
- Simple planner that generates structured plans
- WIP PR creation with initial issue prompt
- PR description updates with detailed plans
- Comprehensive test coverage

See **[POC-SETUP.md](POC-SETUP.md)** for setup and testing instructions.

## Solution Structure

- `RG.OpenCopilot.slnx` – root solution
- `RG.OpenCopilot.App` – ASP.NET Core minimal API for GitHub App webhooks and health checks
- `RG.OpenCopilot.Agent` – shared domain models and planner/executor abstractions
- `RG.OpenCopilot.Runner` – console app to run the agent locally for testing
- `RG.OpenCopilot.Tests` – xUnit tests using Shouldly assertions

All projects target `.NET 10.0` with nullable reference types and implicit usings enabled.

## High-Level Architecture

1. **GitHub App (on GitHub Enterprise)**
   - Installed on your GHE organization.
   - Listens for `issues` events.
   - When an issue receives the `copilot-assisted` label, the app creates an agent task.

2. **Planner (Premium Model)**
   - Reads the issue, optional instructions markdown, and repository metadata.
   - Produces a structured `AgentPlan` (problem summary, steps, checklist, file targets).

3. **Executor (Cheaper Model)**
   - Takes the plan and interacts with the repository (via Git + filesystem and commands) to apply changes and run tests.
   - Creates or updates a pull request with the changes.

## Quick Start

Build the solution:

```bash
dotnet build
```

Run tests:

```bash
dotnet test
```

Run the web app:

```bash
dotnet run --project RG.OpenCopilot.App
```

Then open `http://localhost:5272/health` (or the port shown in the console) to verify it responds with `ok`.

For detailed setup and testing instructions, see **[POC-SETUP.md](POC-SETUP.md)**.

## Next Steps

- Implement a real `IPlannerService` that calls your chosen premium AI model.
- Implement a real `IExecutorService` that:
  - Clones the repository associated with the issue.
  - Applies file edits and commits on a new branch.
  - Opens/updates pull requests and posts comments via the GitHub API.
- Add background job processing for long-running tasks.
- Add persistent storage for agent tasks.
- Create and configure a GitHub App on your GitHub Enterprise instance that points its webhook URL to `RG.OpenCopilot.App`.
