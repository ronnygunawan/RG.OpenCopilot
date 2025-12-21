# RG.OpenCopilot Demo - Quick Start Guide

This demo provides a **minimal "first successful run" experience** that showcases RG.OpenCopilot's core capabilities without requiring GitHub App setup, webhooks, or Docker.

## What You'll See

The demo demonstrates the complete AI-powered planning flow:

1. 📋 **Sample Issue** - Loads a realistic GitHub issue
2. 🤖 **AI Planning** - Generates a detailed implementation plan
3. 📝 **Structured Output** - Creates actionable steps and checklists
4. 💾 **Artifacts** - Saves the generated plan for inspection

**Time to complete:** ~1-2 minutes (planning phase only)

## Prerequisites

- **.NET 10.0 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **(Optional)** OpenAI API key for AI-powered planning
  - Without an API key, the demo uses a simple rule-based planner

## Quick Start

### One-Command Demo (Linux/macOS)

```bash
./demo/run-demo.sh
```

### One-Command Demo (Windows)

```cmd
demo\run-demo.bat
```

### Manual Run

```bash
# Build
dotnet build RG.OpenCopilot.Demo/RG.OpenCopilot.Demo.csproj

# Run
dotnet run --project RG.OpenCopilot.Demo/RG.OpenCopilot.Demo.csproj
```

## Optional: Configure AI Planning

For AI-powered planning with detailed, intelligent plans:

1. Create `RG.OpenCopilot.Demo/appsettings.Development.json`:

```json
{
  "LLM": {
    "Planner": {
      "Provider": "OpenAI",
      "ApiKey": "sk-your-api-key-here",
      "ModelId": "gpt-4o"
    }
  }
}
```

2. Run the demo again to see AI-generated plans

**Supported providers:**
- OpenAI (gpt-4o, gpt-4-turbo, gpt-3.5-turbo)
- Azure OpenAI (requires `AzureEndpoint` and `AzureDeployment`)
- Anthropic Claude
- Google Gemini

See [LLM-CONFIGURATION.md](../LLM-CONFIGURATION.md) for detailed configuration.

## What Happens During the Demo

### 1. Issue Loading
```
📋 Sample Issue Loaded
   Title: Add multiplication and division methods to Calculator
   Number: #1
```

The demo loads a sample issue that requests adding new methods to a calculator class.

### 2. Planning Phase
```
🤖 Stage 1: Planning
   Analyzing issue and generating implementation plan...

✅ Plan Generated Successfully!
```

The planner analyzes the issue and generates a structured plan with:
- Problem summary
- Constraints (coding conventions, requirements)
- Implementation steps (detailed, ordered)
- File targets (files to modify)
- Completion checklist

### 3. Plan Output
```
📝 Problem Summary:
   Add multiplication and division methods to the Calculator class...

⚠️  Constraints:
   • Use XML documentation comments
   • Handle division by zero appropriately

📋 Implementation Steps (3 steps):
   1. Add Multiply method
      Add a Multiply(int a, int b) method that returns the product...
   2. Add Divide method with error handling
      Add a Divide(int a, int b) method with division by zero check...
   3. Update documentation
      Ensure XML comments are present for both new methods...

📁 Files to Modify:
   • src/Calculator.cs

✓ Completion Checklist:
   □ Both methods implemented
   □ XML documentation added
   □ Division by zero handled correctly
   □ Code follows existing style
```

### 4. Artifact Generation
```
💾 Plan saved to: demo/generated-plan.json
```

The complete plan is saved as a JSON file for inspection.

## Understanding the Output

### Generated Plan Structure

The plan includes:

- **Problem Summary** - Concise description of what needs to be done
- **Constraints** - Requirements and guidelines to follow
- **Steps** - Ordered, detailed implementation steps
- **File Targets** - Specific files that need changes
- **Checklist** - Verification items before completion

### Sample Repository

The demo includes a simple C# calculator project:

```
demo/sample-repo/
├── README.md
├── Sample.Calculator.sln
└── src/
    ├── Calculator.csproj
    └── Calculator.cs          # Main calculator class
```

This provides a realistic but minimal codebase for demonstration.

## What's NOT in This Demo

This demo focuses on the **planning phase only** to provide quick verification. It does NOT include:

- ❌ Docker container execution
- ❌ Actual code generation/modification
- ❌ Test generation
- ❌ Build/test execution
- ❌ Git operations
- ❌ GitHub PR creation

These features are available in the full system but require additional setup.

## Full System Setup

For the complete experience including code execution, Docker, and GitHub integration:

1. See [POC-SETUP.md](../POC-SETUP.md) for detailed setup
2. See [README.md](../README.md) for architecture overview
3. See [LLM-CONFIGURATION.md](../LLM-CONFIGURATION.md) for AI configuration

## Troubleshooting

### "Build failed"

Ensure .NET 10.0 SDK is installed:
```bash
dotnet --version  # Should show 10.0.x or later
```

### "Sample issue file not found"

Make sure you're running from the RG.OpenCopilot root directory:
```bash
cd /path/to/RG.OpenCopilot
./demo/run-demo.sh
```

### Using Simple Planner

If you see "Using SimplePlannerService", the demo is running without AI. This works but produces basic plans. Add an OpenAI API key for better results.

### Permission Denied (Linux/macOS)

Make the script executable:
```bash
chmod +x demo/run-demo.sh
```

## What's Next

After running the demo:

1. ✅ Inspect `demo/generated-plan.json` to see the detailed plan
2. 📖 Read [PLAN.md](../PLAN.md) to understand the architecture
3. 🔧 Try [POC-SETUP.md](../POC-SETUP.md) for the full setup
4. 🚀 Label a real GitHub issue with `copilot-assisted` to see it in action

## Feedback

Found an issue or have suggestions? [Open an issue](https://github.com/ronnygunawan/RG.OpenCopilot/issues) on GitHub!
