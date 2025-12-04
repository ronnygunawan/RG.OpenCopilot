# RG.OpenCopilot (GitHub Enterprise Coding Agent)

RG.OpenCopilot is a C#/.NET 10 solution that aims to provide a GitHub Enterprise–hosted coding agent similar to the GitHub Copilot Coding Agent (GCCA).

The agent is triggered by labeling issues with `copilot-assisted`. It analyzes the issue, optional instructions markdown, and the repository, creates a detailed plan using a premium model, then hands execution to cheaper models that modify code, run tests, and open pull requests.

## Solution Structure

- `RG.OpenCopilot.slnx` – root solution
- `RG.OpenCopilot.App` – ASP.NET Core minimal API for GitHub App webhooks and health checks
- `RG.OpenCopilot.Agent` – shared domain models and planner/executor abstractions
- `RG.OpenCopilot.Runner` – console app to run the agent locally for testing
- `RG.OpenCopilot.Tests` – xUnit tests using Shouldly assertions (placeholder)

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

## Running Locally

Build the solution:

```pwsh
cd "c:\Users\Ronny\Documents\open-copilot"
dotnet build
```

Run the web app (placeholder webhook + health endpoint):

```pwsh
dotnet run --project .\RG.OpenCopilot.App\RG.OpenCopilot.App.csproj
```

Then open `http://localhost:5000/health` (or the port shown in the console) to verify it responds with `ok`.

Run the local runner stub:

```pwsh
dotnet run --project .\RG.OpenCopilot.Runner\RG.OpenCopilot.Runner.csproj -- "Sample issue title" "Sample issue body"
```

You should see a stub plan printed to the console. This verifies the planner interface and basic wiring.

## Next Steps

- Implement a real `IPlannerService` that calls your chosen premium AI model.
- Implement a real `IExecutorService` that:
  - Clones the repository associated with the issue.
  - Applies file edits and commits on a new branch.
  - Opens/updates pull requests and posts comments via the GitHub API.
- Create and configure a GitHub App on your GitHub Enterprise instance that points its webhook URL to `RG.OpenCopilot.App`.
