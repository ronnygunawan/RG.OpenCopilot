# RG.OpenCopilot

> **⚠️ WARNING: This project is under active development, 100% written by LLM, and is NOT ready for production use. Use at your own risk.**

**A GitHub-hosted AI coding agent for automating code changes, test generation, and pull request management.**

RG.OpenCopilot is a C#/.NET 10 solution that aims to provide an intelligent coding agent similar to GitHub Copilot Coding Agent. It is designed to automatically analyze GitHub issues, create implementation plans using LLM models, generate code and tests, and manage the complete pull request lifecycle - all within isolated Docker containers for security.

**This is an experimental project under construction. Many features are incomplete or subject to breaking changes.**

## 🚀 Key Features

- **🤖 Automated Code Generation**: LLM-powered code generation in multiple languages (C#, TypeScript, Python, Java, Go, Rust)
- **🧪 Test Generation**: Automatic unit test creation matching your project's testing patterns and conventions
- **📋 Intelligent Planning**: Creates detailed implementation plans by analyzing issues, repository structure, and custom instructions
- **🔒 Secure Execution**: All code changes run in isolated Docker containers
- **🔄 Full PR Lifecycle**: Manages WIP branches, updates PR descriptions with progress, and finalizes PRs when complete
- **🎯 Multi-Language Support**: Detects and works with .NET, Node.js, Python, Java, Go, Rust, and more
- **📊 Repository Analysis**: Automatically detects languages, build tools, test frameworks, and project structure
- **🎨 Style Preservation**: Maintains your project's coding conventions and patterns

## ⚠️ Project Status

**🚧 UNDER CONSTRUCTION - NOT READY FOR PRODUCTION USE 🚧**

This project is actively under development and should **NOT** be used in production environments. Many features are incomplete, untested in real-world scenarios, or subject to significant changes.

### Implemented Features (In Development)
- ✅ GitHub webhook integration with signature validation
- ✅ GitHub App authentication with installation token caching
- ✅ LLM-powered planning with Semantic Kernel (OpenAI, Azure OpenAI)
- ✅ Docker-based code execution environment
- ✅ Code generation with context awareness
- ✅ Test generation matching project patterns
- ✅ File operations (read, write, analyze, edit)
- ✅ Build and test automation
- ✅ Git operations (commit, push, branch management)
- ✅ PR lifecycle management (create, update, finalize)
- ✅ Repository analysis and custom instructions support
- ✅ Persistent task storage with PostgreSQL and EF Core
- ✅ Background job processing with retry and timeout support
- ✅ Task resumption after application restart (with PostgreSQL)
- ✅ 1050+ comprehensive unit and integration tests

### Known Limitations
- Limited error recovery and retry logic
- Not tested at scale or in production environments
- API and architecture may change significantly

## How It Works

1. **Trigger**: Label a GitHub issue with `copilot-assisted`
2. **Planning**: The agent analyzes the issue, repository structure, and custom instructions to create a detailed implementation plan
3. **Execution**: In an isolated Docker container, the agent:
   - Clones the repository
   - Generates code based on the plan
   - Creates tests to verify the changes
   - Runs builds and tests
   - Commits changes to a WIP branch
4. **PR Management**: Creates and updates a pull request with progress updates and checklist
5. **Completion**: Removes WIP status and finalizes PR description when all tasks are complete

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                    GitHub Enterprise                         │
│              (Issues, PRs, Webhooks, Repository)             │
└───────────────────────────┬──────────────────────────────────┘
                            │ Webhook Event (issue labeled)
                            ▼
┌──────────────────────────────────────────────────────────────┐
│                 RG.OpenCopilot.GitHubApp                     │
│                  (ASP.NET Core Minimal API)                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │
│  │   Webhook    │  │   Planner    │  │   Executor   │        │
│  │   Handler    │─>│   Service    │─>│   Service    │        │
│  └──────────────┘  └──────────────┘  └──────────────┘        │
└───────────┬───────────────────────────────────┬──────────────┘
            │                                   │
            ▼                                   ▼
┌───────────────────────┐       ┌──────────────────────────────┐
│    LLM Provider       │       │      Docker Container        │
│   (OpenAI/Azure)      │       │  ┌────────────────────────┐  │
│  • Plan Generation    │       │  │  Cloned Repository     │  │
│  • Code Generation    │       │  │  • Build & Test        │  │
│  • Test Generation    │       │  │  • File Operations     │  │
│                       │       │  │  • Git Commands        │  │
└───────────────────────┘       │  └────────────────────────┘  │
                                └──────────────────────────────┘
```

### Solution Structure

The solution follows a **feature-based architecture** with clear separation of concerns:

- **RG.OpenCopilot.PRGenerationAgent** - Domain models and service abstractions
  - `Planning/` - Planning domain (AgentPlan, PlanStep, IPlannerService)
  - `Execution/` - Execution domain (AgentTask, AgentTaskStatus, IExecutorService)
  - `FileOperations/` - File operations (FileStructure, FileTree, IFileAnalyzer, IFileEditor)
  - `CodeGeneration/` - Code generation (ICodeGenerator, ITestGenerator)
  - `Infrastructure/` - Infrastructure interfaces (IAgentTaskStore, ICommandExecutor)

- **RG.OpenCopilot.PRGenerationAgent.Services** - Service implementations
  - `Planner/` - LlmPlannerService, SimplePlannerService
  - `Executor/` - ExecutorService, ContainerExecutorService, StepAnalyzer
  - `CodeGeneration/` - CodeGenerator, TestGenerator
  - `Docker/` - ContainerManager, FileAnalyzer, FileEditor
  - `GitHub/` - Git operations, repository analysis, webhook handling, authentication
  - `Infrastructure/` - AgentTaskStore, CommandExecutor, RepositoryCloner

- **RG.OpenCopilot.GitHubApp** - ASP.NET Core minimal API
  - Webhook endpoints (`/github/webhook`, `/health`)
  - Service registration and configuration

- **RG.OpenCopilot.Tests** - Comprehensive test suite
  - Unit tests using xUnit and Shouldly assertions
  - Integration tests for Docker-based operations
  - 112 tests with ~64% code coverage

All projects target **.NET 10.0** with nullable reference types and follow SOLID principles and DDD patterns.

## Quick Start

### Prerequisites

- **.NET 10.0 SDK** or later
- **Docker** (Docker Desktop on Windows, Docker Engine on Linux)
- **Git**
- **GitHub Personal Access Token** or **GitHub App credentials**
- **LLM API Key** (OpenAI or Azure OpenAI) - optional for development

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/ronnygunawan/RG.OpenCopilot.git
   cd RG.OpenCopilot
   ```

2. **Build the solution**
   ```bash
   dotnet build RG.OpenCopilot.slnx --configuration Release
   ```

3. **Run tests** (requires Docker)
   ```bash
   # All tests
   dotnet test
   
   # Unit tests only (no Docker required)
   dotnet test --filter "FullyQualifiedName!~IntegrationTests"
   ```

4. **Configure the application**
   
   Create `appsettings.Development.json` or set environment variables:
   ```json
   {
     "GitHub": {
       "Token": "ghp_your_personal_access_token",
       "WebhookSecret": "your-webhook-secret",
       "AppId": "123456",
       "AppPrivateKey": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----"
     },
     "LLM": {
       "Provider": "OpenAI",
       "ApiKey": "sk-your-openai-api-key",
       "ModelId": "gpt-4o"
     }
   }
   ```

5. **Run the application**
   ```bash
   dotnet run --project RG.OpenCopilot.GitHubApp
   ```
   
   The app starts at `http://localhost:5272`. Verify with:
   ```bash
   curl http://localhost:5272/health
   # Should return: ok
   ```

### Platform Support

- **Windows and Linux** host systems supported
- Executor **always runs in Linux containers** for consistent behavior
- Docker with Linux container support required

## Configuration

### GitHub Authentication

**For Development** - Personal Access Token:
- Create token at https://github.com/settings/tokens
- Required scopes: `repo`, `workflow`
- Set `GitHub:Token` in configuration

**For Production** - GitHub App:
1. Create GitHub App with permissions:
   - Repository contents: Read & write
   - Pull requests: Read & write
   - Issues: Read & write
   - Workflows: Read & write (optional)
2. Generate private key
3. Configure `GitHub:AppId` and `GitHub:AppPrivateKey`
4. Install app on target repositories

**Authentication Features:**
- **Automatic token caching**: Installation tokens are cached and refreshed automatically
- **Per-installation tokens**: Each repository installation gets its own scoped token
- **Fallback support**: Automatically falls back to PAT if GitHub App not configured
- **Permission validation**: Checks installation has required permissions before processing

See [POC-SETUP.md](POC-SETUP.md) for detailed setup instructions.

### LLM Configuration

The planner uses LLM models for intelligent plan generation. Configure your preferred provider:

**OpenAI:**
```json
{
  "LLM": {
    "Provider": "OpenAI",
    "ApiKey": "sk-...",
    "ModelId": "gpt-4o"
  }
}
```

**Azure OpenAI:**
```json
{
  "LLM": {
    "Provider": "AzureOpenAI",
    "ApiKey": "your-azure-key",
    "AzureEndpoint": "https://your-resource.openai.azure.com",
    "AzureDeployment": "your-deployment-name"
  }
}
```

**Fallback Mode**: If no API key is configured, the system uses `SimplePlannerService` for basic planning without LLM costs.

See [LLM-CONFIGURATION.md](LLM-CONFIGURATION.md) for complete configuration details and supported models.

### Custom Instructions

Provide project-specific coding guidelines by creating markdown files in `.github/open-copilot/`:

- **Issue-specific**: `.github/open-copilot/{issueNumber}.md`
- **General**: `.github/open-copilot/instructions.md`
- **Fallback**: `.github/open-copilot/README.md`

Example:
```markdown
# Coding Guidelines

- Use async/await for all I/O operations
- Follow repository naming conventions
- Add XML documentation for public APIs
- Ensure all code has test coverage
- Use dependency injection for services
```

## Core Capabilities

### 🤖 Code Generation

Generate production-ready code in multiple languages using LLM models:

```csharp
var request = new LlmCodeGenerationRequest {
    Description = "Create a User class with Id, Name, Email properties and validation",
    Language = "C#",
    FilePath = "Models/User.cs"
};
var code = await codeGenerator.GenerateCodeAsync(request);
```

**Supported Languages**: C#, JavaScript, TypeScript, Python, Java, Go, Rust

**Features**:
- Context-aware generation with repository analysis
- Dependency management
- Style preservation
- Multi-file generation support
- Framework-specific best practices

See [CODE-GENERATOR.md](CODE-GENERATOR.md) for detailed documentation.

### 🧪 Test Generation

Automatically generate comprehensive unit tests matching your project's patterns:

```csharp
var tests = await testGenerator.GenerateTestsAsync(
    containerId: "my-container",
    codeFilePath: "Calculator.cs",
    codeContent: codeContent,
    testFramework: "xUnit"
);
```

**Supported Frameworks**:
- **.NET**: xUnit, NUnit, MSTest
- **JavaScript/TypeScript**: Jest, Mocha
- **Python**: pytest, unittest

**Features**:
- Automatic framework detection
- Pattern analysis from existing tests
- Assertion style matching (Shouldly, FluentAssertions, etc.)
- Naming convention replication
- Test execution and validation

See [TEST-GENERATOR.md](TEST-GENERATOR.md) for detailed documentation.

### 📋 Intelligent Planning

Creates detailed, actionable implementation plans:

```json
{
  "problemSummary": "Add user authentication feature",
  "constraints": ["Use JWT tokens", "Follow existing patterns"],
  "steps": [
    {
      "id": "step-1",
      "title": "Create authentication models",
      "details": "Define User, Token, and Credentials classes...",
      "done": false
    }
  ],
  "checklist": ["All tests pass", "API documented"],
  "fileTargets": ["Auth/User.cs", "Auth/AuthService.cs"]
}
```

**Features**:
- Repository structure analysis
- Language and framework detection
- Custom instruction integration
- JSON-validated plan output
- Progress tracking with checklists

### 🔒 Secure Execution

All code execution happens in isolated Docker containers:

**Features**:
- Isolated environment per task
- Repository cloning with credentials
- File read/write operations
- Command execution (build, test, lint)
- Git operations (commit, push)
- Automatic cleanup

**Security**:
- Container isolation prevents host contamination
- Path validation prevents directory traversal
- Credential scoping per operation
- No persistent state between tasks

### 🔄 PR Lifecycle Management

Manages the complete pull request workflow:

1. **WIP Phase**:
   - Creates branch: `open-copilot/issue-{number}`
   - Opens PR with `[WIP]` prefix
   - Updates description with plan and progress
   - Posts comments with change summaries

2. **Completion Phase**:
   - Removes `[WIP]` prefix
   - Finalizes PR description
   - Archives WIP details in collapsed section
   - Marks all checklist items complete

### 📊 Repository Analysis

Automatically detects project characteristics:

```csharp
var analysis = await analyzer.AnalyzeAsync(owner, repo, installationId);
// Detects:
// - Primary languages (C# 65%, TypeScript 25%, etc.)
// - Build tools (dotnet, npm, maven, cargo)
// - Test frameworks (xUnit, Jest, pytest)
// - Configuration files (.csproj, package.json)
```

**Detection Capabilities**:
- **Languages**: C#, JavaScript, TypeScript, Python, Java, Go, Rust, Ruby, PHP, Swift
- **Build Tools**: dotnet, npm, yarn, maven, gradle, cargo, go, pip
- **Test Frameworks**: xUnit, NUnit, MSTest, Jest, Mocha, pytest, JUnit
- **Project Files**: Detects solution files, package managers, configuration

## Usage

### Using with GitHub

1. **Set up a GitHub App** (see [POC-SETUP.md](POC-SETUP.md) for details):
   - Create GitHub App with webhook URL: `https://your-domain.com/github/webhook`
   - Configure permissions: Contents (R/W), Pull Requests (R/W), Issues (R/W)
   - Subscribe to "Issues" events
   - Install app on target repositories

2. **Create an issue** in a repository where the app is installed

3. **Add the `copilot-assisted` label** to trigger the agent

4. **The agent will**:
   - Analyze the issue and repository
   - Create a detailed implementation plan
   - Generate and commit code changes
   - Create tests
   - Open a PR with progress updates
   - Finalize the PR when complete

### Local Testing

Test the webhook endpoint locally:

```bash
# Create test payload
cat > test-webhook.json << 'END'
{
  "action": "labeled",
  "label": { "name": "copilot-assisted" },
  "issue": {
    "number": 1,
    "title": "Add user authentication",
    "body": "Implement JWT-based authentication"
  },
  "repository": {
    "name": "test-repo",
    "full_name": "owner/test-repo",
    "owner": { "login": "owner" }
  },
  "installation": { "id": 12345 }
}
END

# Send webhook
curl -X POST http://localhost:5272/github/webhook \
  -H "Content-Type: application/json" \
  -H "X-GitHub-Event: issues" \
  -d @test-webhook.json
```

## Development

### Project Structure

```
RG.OpenCopilot/
├── RG.OpenCopilot.PRGenerationAgent/          # Domain models & interfaces
│   ├── Planning/                              # Planning domain
│   ├── Execution/                             # Execution domain
│   ├── FileOperations/                        # File operations
│   ├── CodeGeneration/                        # Code generation
│   └── Infrastructure/                        # Infrastructure interfaces
├── RG.OpenCopilot.PRGenerationAgent.Services/ # Service implementations
│   ├── Planner/                               # Planning services
│   ├── Executor/                              # Execution services
│   ├── CodeGeneration/                        # Code & test generators
│   ├── Docker/                                # Container management
│   ├── GitHub/                                # GitHub integration
│   └── Infrastructure/                        # Infrastructure implementations
├── RG.OpenCopilot.GitHubApp/                 # ASP.NET Core API
│   └── Program.cs                             # Webhook endpoints
├── RG.OpenCopilot.Tests/                     # Test suite
└── docker/                                    # Docker configurations
```

### Key Technologies

- **.NET 10.0**: Target framework
- **Microsoft Semantic Kernel**: LLM integration
- **Octokit**: GitHub API client
- **Docker.DotNet**: Docker API client
- **xUnit + Shouldly**: Testing framework

### Coding Conventions

- **Architecture**: Feature-based organization, SOLID principles, DDD patterns
- **Style**: 4 spaces, K&R braces, file-scoped namespaces
- **Immutability**: `init` accessors, `sealed` classes where appropriate
- **Async**: All I/O operations use `async`/`await`
- **Nullability**: Nullable reference types enabled
- **Testing**: xUnit with Shouldly assertions, 100% coverage target for new code

See [.github/copilot-instructions.md](.github/copilot-instructions.md) for complete coding guidelines.

### Running Tests

```bash
# All tests (requires Docker)
dotnet test

# Unit tests only
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report
reportgenerator \
  -reports:"TestResults/*/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" \
  -reporttypes:Html
```

Current coverage: **~64%** (112 tests, all passing)

See [COVERAGE.md](COVERAGE.md) for detailed coverage report.

### Building

```bash
# Debug build
dotnet build

# Release build
dotnet build --configuration Release

# Clean and rebuild
dotnet clean && dotnet build
```

## Roadmap

**⚠️ This project is in early development. See [ROADMAP.md](ROADMAP.md) for the complete contributor-facing roadmap including current phase, next steps, and how to help.**

### Current Phase: Core Foundation (In Progress) ✅
- ✅ Core agent architecture and domain models
- ✅ GitHub webhook integration with signature validation
- ✅ LLM-powered planning (OpenAI, Azure OpenAI)
- ✅ Docker-based executor with container isolation
- ✅ Code generation with multi-language support
- ✅ Test generation matching project patterns
- ✅ File operations (analyze, read, write, edit)
- ✅ Repository analysis and custom instructions
- ✅ Build and test automation
- ✅ PR lifecycle management (WIP → final)
- ✅ Comprehensive test coverage
- 🚧 Enhanced error recovery and retry logic
- 🚧 Performance optimizations for large repositories
- 🚧 Production readiness and stability improvements

### Next Phase: Production Readiness 🚀
- 📋 Comprehensive error handling and recovery
- 📋 Security audit and hardening
- 📋 Performance testing and optimization
- 📋 Production deployment documentation and tooling
- 📋 Observability (logging, metrics, tracing)

**→ [View Full Roadmap](ROADMAP.md)** for detailed phases, contribution opportunities, and out-of-scope items.

## Documentation

- **[ROADMAP.md](ROADMAP.md)** - Project roadmap and contribution guide
- **[POC-SETUP.md](POC-SETUP.md)** - Setup and testing instructions
- **[LLM-CONFIGURATION.md](LLM-CONFIGURATION.md)** - LLM provider configuration
- **[CODE-GENERATOR.md](CODE-GENERATOR.md)** - Code generation documentation
- **[TEST-GENERATOR.md](TEST-GENERATOR.md)** - Test generation documentation
- **[EXECUTOR-SERVICE.md](EXECUTOR-SERVICE.md)** - Executor service details
- **[FILE-EDITOR.md](FILE-EDITOR.md)** - File editing capabilities
- **[COVERAGE.md](COVERAGE.md)** - Test coverage report
- **[PLAN.md](PLAN.md)** - Original architecture plan

## Contributing

**⚠️ Important**: This project is in early development and not ready for production use. Contributions are welcome, but be aware that significant architectural changes may occur.

**See [ROADMAP.md](ROADMAP.md) for:**
- Current development phase and priorities
- Areas where we need help
- Contribution guidelines and getting started steps
- Out-of-scope items and future plans

### Quick Start

1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Ensure all tests pass
5. Follow existing coding conventions
6. Submit a pull request

See [.github/copilot-instructions.md](.github/copilot-instructions.md) for coding guidelines.

## License

See [LICENSE](LICENSE) file for details.

## Support

For issues or questions:
- 📝 [Create an issue](https://github.com/ronnygunawan/RG.OpenCopilot/issues)
- 📖 Check the documentation in this repository
- 💬 Review closed issues for solutions

---

## ⚠️ Disclaimer

**THIS SOFTWARE IS PROVIDED "AS IS" AND IS NOT READY FOR PRODUCTION USE.**

This is an experimental project under active development. It is **NOT** suitable for production environments and should only be used for development, testing, and evaluation purposes. The authors make no warranties about the stability, security, or fitness for any particular purpose.

Use at your own risk. The API, architecture, and features are subject to significant changes without notice.
