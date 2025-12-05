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

## Features

### Container-Based Executor
- **Docker Integration**: Executes code changes in isolated Docker containers
- **Repository Cloning**: Automatic repository cloning inside containers
- **File Operations**: Read and write files in containers using Docker exec
- **Build & Test**: Automatic detection and execution of .NET, Node.js, and Python projects
- **Git Integration**: Commits and pushes changes from containers

### File Analysis
- **FileAnalyzer Service**: Analyzes repository files to extract structure and metadata
- **Multi-Language Support**: Parses C#, JavaScript/TypeScript, and Python files
- **Structure Extraction**: Identifies classes, functions, imports, and namespaces
- **File Tree Building**: Creates hierarchical file tree representation
- **Pattern Matching**: Supports file discovery with wildcard patterns

The FileAnalyzer enables the executor to understand repository code structure before making modifications, supporting intelligent code changes.

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

For LLM configuration (OpenAI, Azure OpenAI), see **[LLM-CONFIGURATION.md](LLM-CONFIGURATION.md)**.

## LLM Integration

The planner now supports real LLM integration via Microsoft Semantic Kernel:

- **Intelligent plan generation** using GPT-4 or other premium models
- **Repository analysis** that detects languages, build tools, and test frameworks
- **Custom instructions** support via `.github/open-copilot/` markdown files
- **JSON schema enforcement** for structured plan output
- **Fallback mode** for development without an API key

See [LLM-CONFIGURATION.md](LLM-CONFIGURATION.md) for setup instructions.

## Next Steps

- ✅ ~~Implement a real `IPlannerService` that calls your chosen premium AI model.~~ (Completed with SemanticKernel integration)
- ✅ ~~Implement a real `IExecutorService` that:~~ (Completed with Git-based repository cloning and command execution)
  - ✅ ~~Clones the repository associated with the issue using GitHub App installation tokens.~~
  - ✅ ~~Provides file read/write operations and command execution in sandboxed environment.~~
  - ✅ ~~Commits and pushes changes to the WIP branch.~~
  - ✅ ~~Posts progress comments to pull requests via the GitHub API.~~
- Add background job processing for long-running tasks.
- Add persistent storage for agent tasks.
- Create and configure a GitHub App on your GitHub Enterprise instance that points its webhook URL to `RG.OpenCopilot.App`.
