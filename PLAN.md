# RG.OpenCopilot – Architecture and Implementation Plan

## 1. Goal

Build a GitHub Enterprise–hosted coding agent similar to GitHub Copilot Coding Agent (GCCA):

- Triggered by labeling issues with `copilot-assisted`.
- Uses a **premium model** to analyze the issue, instructions markdown, and repo to create a structured plan and checklist.
- Uses a **cheaper model** to execute the plan: edit code, run tests, and update a long-lived PR.
- Manages a **WIP PR lifecycle** where the PR description and title evolve as the task progresses.

---

## 2. High-Level Architecture

1. **GitHub Enterprise (GHE)**
   - Hosts repositories and issues.
   - Workflow: label an issue with `copilot-assisted` to request agent help.

2. **GitHub App (RG.OpenCopilot App)**
   - Installed on orgs/repos in GHE.
   - Permissions:
     - Contents: Read & write.
     - Pull requests: Read & write.
     - Issues: Read & write.
     - Checks: Read & write (optional).
   - Webhooks:
     - `issues` – detect `copilot-assisted` label.
     - (Later) `issue_comment`, `pull_request` as needed.
   - Webhook endpoint is implemented in `RG.OpenCopilot.GitHubApp`.

3. **RG.OpenCopilot.GitHubApp (ASP.NET Core minimal API)**
   - Receives GitHub webhooks at `/github/webhook`.
   - Validates signatures.
   - On `issues` labeled `copilot-assisted`:
     - Creates an `AgentTask` (repo, issue, installation id).
     - Immediately creates a **WIP PR** from a new branch with the original issue prompt captured in the PR description.
     - Calls `IPlannerService` to generate an `AgentPlan`.
     - Updates the WIP PR description with the detailed plan and checklist, archiving the original prompt in a collapsed section.
     - Persists task/plan and schedules `IExecutorService`.

4. **RG.OpenCopilot.Agent (class library)**
   - Shared domain + abstractions:
     - `AgentPlan`, `PlanStep`, `AgentTaskStatus`, `AgentTask`, `AgentTaskContext`.
     - `IPlannerService` – premium model planner.
     - `IExecutorService` – cheap model executor.
   - Planner:
     - Consumes issue text, instructions markdown, and repo summary.
     - Produces structured plan + checklist + file targets.
   - Executor:
     - Uses repo access + commands to apply plan steps and open PRs.

5. **RG.OpenCopilot.Runner (console app)**
   - Local, GitHub-independent runner for development/testing.
   - Accepts issue-like input and exercises planner/executor against a local repo.

6. **RG.OpenCopilot.Tests (xUnit)**
   - Unit tests for planner/executor orchestration and core domain logic.

---

## 3. Planner–Executor Pipeline

### 3.1 Planner (Premium Model)

- Triggered when an issue is labeled `copilot-assisted` and a WIP branch/PR have been created.
- Gathers context:
  - Issue title, body, and labels.
  - Optional instructions markdown (e.g. `.github/open-copilot/<issue-number>.md` or other conventions).
  - Repository summary (languages, key config files, known test commands, important directories).
- Calls a premium LLM with a strict JSON schema to produce `AgentPlan`:
  - `ProblemSummary` – concise description of the task.
  - `Constraints` – coding guidelines, tech stack, tests to respect.
  - `Steps` – ordered `PlanStep` items with `Id`, `Title`, and `Details`.
  - `Checklist` – items that must be true before calling task “done”.
  - `FileTargets` – likely files/directories to touch.
- Persists the plan (DB or simple storage).
- Updates the **WIP PR description** to contain:
  - A collapsed `<details>` block that archives the **original prompt** (issue title/body/labels).
  - A detailed, checkbox-based task plan derived from `AgentPlan`.

### 3.2 Executor (Cheaper Model)

- Consumes an `AgentTask` with `Plan` attached and a pre-existing **WIP PR**.
- For each unfinished `PlanStep`:
  - Ensures the working branch (e.g. `open-copilot/issue-<n>-<id>`) that backs the WIP PR.
  - Uses tools (implemented in .NET) to:
    - Read/write files in the repo.
    - Run tests/commands.
    - Inspect diffs.
  - Calls the cheaper model iteratively with:
    - Plan JSON.
    - Past tool results.
    - Explicit instruction to follow the plan and checklist.
- After a coherent set of changes:
  - Commits and pushes to the working branch.
  - **Updates the WIP PR description** to reflect current progress (e.g. checked boxes, updated notes).
  - Optionally adjusts the PR title while still keeping a `[WIP]` prefix when work is ongoing.
  - Adds a **PR comment** summarizing the latest changes and test results.
- On full completion:
  - Marks all steps as done, confirms checklist items.
  - **Removes the `[WIP]` tag from the PR title**.
  - Rewrites the PR description into the final human-facing description.
  - Wraps the previous WIP description (detailed plan and history) inside a collapsed `<details>` section for archival.

---

## 4. Core C# Components

### 4.1 Domain Models (`RG.OpenCopilot.Agent`)

- `AgentPlan` – overall plan and checklist.
- `PlanStep` – single step in the plan.
- `AgentTaskStatus` – lifecycle (`PendingPlanning`, `Planned`, `Executing`, `Completed`, `Blocked`, `Failed`).
- `AgentTask` – one unit of work per issue.
- `AgentTaskContext` – planner input (issue text, instructions, repo summary).

### 4.2 Planner Abstractions

- `IPlannerService`:
  - `Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken ct = default);`
  - Implementation will:
    - Build a prompt for the premium model.
    - Enforce JSON schema for `AgentPlan`.

### 4.3 Executor Abstractions

- `IExecutorService`:
  - `Task ExecutePlanAsync(AgentTask task, CancellationToken ct = default);`
  - Implementation will:
    - Clone/fetch repo using GitHub App installation token.
    - Create working branch.
    - Provide file and command tools to the cheaper model.
    - Commit, push, and open/update PR.

### 4.4 GitHub Integration (`RG.OpenCopilot.GitHubApp`)

- Webhook handler:
  - Validates signature.
  - Deserializes `issues` events.
  - When label `copilot-assisted` is added to an open issue:
    - Creates or retrieves `AgentTask`.
    - Calls planner and then executor (possibly via background jobs).
- GitHub client:
  - Uses GitHub App installation tokens to:
    - Read/write issues and comments.
    - Open/update PRs.
    - Optionally query repo metadata (languages, trees).

### 4.5 Local Runner (`RG.OpenCopilot.Runner`)

- Console entrypoint that:
  - Accepts issue title/body (and later repo path, instructions file).
  - Builds `AgentTaskContext`.
  - Calls `IPlannerService` (local or HTTP-based implementation).
  - Optionally calls `IExecutorService` against a local repo clone.

---

## 5. Incremental Implementation Steps

1. **Webhook wiring & WIP PR creation**
  - Replace stub `/github/webhook` in `RG.OpenCopilot.GitHubApp` with real handler:
    - Verify GitHub signature.
    - Parse `issues` events.
    - On `copilot-assisted` label, create `AgentTask`.
    - Create a new branch and a WIP PR with a `[WIP]` prefix in the title, using the original issue prompt as the initial PR description.

2. **Planner implementation & PR description update**
  - Add a real `PlannerService` in `RG.OpenCopilot.Agent` or `RG.OpenCopilot.GitHubApp`:
    - Integrate with chosen premium LLM provider.
    - Define planner prompt and JSON schema.
    - Map response → `AgentPlan`.
  - Update the WIP PR description to include:
    - A collapsed `<details>` section that archives the original issue prompt.
    - A checkbox-based plan and checklist derived from `AgentPlan`.

3. **Executor implementation with PR syncing**
  - Implement `ExecutorService` that:
    - Clones repo using installation token.
    - Uses Git and filesystem APIs to read/write.
    - Runs tests/commands in a sandboxed environment.
    - Pushes commits to the WIP branch.
    - Keeps the PR description in sync with step/checklist progress and posts PR comments summarizing changes and test results.

4. **WIP → Final PR transition**
  - When all plan steps and checklist items are complete:
    - Remove `[WIP]` from the PR title.
    - Rewrite the PR description into its final, reviewer-friendly form.
    - Wrap the previous WIP description (plan + history) in a collapsed `<details>` block.

5. **Storage & background jobs**
  - Add minimal persistence for `AgentTask` + `AgentPlan` (e.g., SQLite or simple file-based store to start).
  - Use background processing (e.g., `IHostedService`, worker queue) so long-running tasks do not block webhook calls.

6. **Refinement**
  - Add richer instructions markdown conventions.
  - Improve planner constraints and checklist generation.
  - Tune prompts, logging, and observability.

---

## 6. Current Status (Scaffold)

- .NET 8 solution and projects created and building successfully.
- Core domain models and planner/executor interfaces defined in `RG.OpenCopilot.Agent`.
- Minimal stubs in `RG.OpenCopilot.GitHubApp` and `RG.OpenCopilot.Runner` to verify wiring.
- Next major work: replace stubs with real planner/executor implementations and wire up the GitHub App webhook logic.
