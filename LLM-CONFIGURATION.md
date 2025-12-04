# LLM Configuration Guide

RG.OpenCopilot uses Microsoft Semantic Kernel to integrate with various LLM providers for intelligent plan generation. This guide explains how to configure the LLM provider.

## Supported Providers

Currently implemented:
- **OpenAI** - Direct OpenAI API integration
- **Azure OpenAI** - Azure-hosted OpenAI models

The architecture supports adding additional providers through SemanticKernel connectors.

## Recommended Premium Models for Planning

Based on performance and capabilities, the following premium models are recommended for intelligent plan generation:

### OpenAI Models
- **GPT-4o** - Current flagship model (available now)
- **GPT-5** - Next-generation model (upcoming)
- **GPT-5-Codex** - Specialized for code generation and planning (upcoming)
- **GPT-5.1** - Enhanced version (upcoming)
- **GPT-5.1-Codex** - Latest code-specialized model (upcoming)

### Anthropic Claude Models  
- **Claude Opus 4.5** - Most capable for complex planning tasks (upcoming)
- **Claude Sonnet 4.5** - Balanced performance and cost (upcoming)
- **Claude 3.5 Sonnet** - Current stable version (available via API)

### Google Gemini Models
- **Gemini 2.5 Pro** - Next-generation Gemini (upcoming)
- **Gemini 3 Pro** - Advanced Gemini model (upcoming)
- **Gemini 1.5 Pro** - Current stable version (available via API)

## Configuration

LLM settings are configured in `appsettings.json` or via environment variables.

### OpenAI Configuration

```json
{
  "LLM": {
    "Provider": "OpenAI",
    "ApiKey": "sk-...",
    "ModelId": "gpt-4o"
  }
}
```

**Configuration Options:**
- `Provider`: Set to `"OpenAI"`
- `ApiKey`: Your OpenAI API key
- `ModelId`: Model to use

**Supported Model IDs:**
- `gpt-4o` (recommended for current use)
- `gpt-4-turbo`
- `gpt-3.5-turbo`
- `gpt-5` (when available)
- `gpt-5-codex` (when available)
- `gpt-5.1` (when available)
- `gpt-5.1-codex` (when available)

### Azure OpenAI Configuration

```json
{
  "LLM": {
    "Provider": "AzureOpenAI",
    "ApiKey": "your-azure-api-key",
    "AzureEndpoint": "https://your-resource.openai.azure.com",
    "AzureDeployment": "your-deployment-name"
  }
}
```

**Configuration Options:**
- `Provider`: Set to `"AzureOpenAI"`
- `ApiKey`: Your Azure OpenAI API key
- `AzureEndpoint`: Your Azure OpenAI endpoint URL
- `AzureDeployment`: The deployment name you configured in Azure

Azure OpenAI supports the same models as OpenAI once deployed to your Azure resource.

## Adding Support for Claude and Gemini

While not currently implemented, support for Anthropic Claude and Google Gemini can be added in the future through:

1. **OpenAI-Compatible Endpoints**: Some providers offer OpenAI-compatible APIs
2. **SemanticKernel Connectors**: Custom connectors implementing `IChatCompletionService`
3. **Microsoft.Extensions.AI**: Using the AI abstractions layer for multi-provider support

### Planned Claude Integration

For Claude models (Opus 4.5, Sonnet 4.5), future implementation would use:
```json
{
  "LLM": {
    "Provider": "Claude",
    "ApiKey": "sk-ant-...",
    "ModelId": "claude-opus-4-5"
  }
}
```

### Planned Gemini Integration

For Gemini models (2.5 Pro, 3 Pro), future implementation would use:
```json
{
  "LLM": {
    "Provider": "Gemini",
    "ApiKey": "your-google-api-key",
    "ModelId": "gemini-2.5-pro"
  }
}
```

## Environment Variables

You can also configure the LLM using environment variables, which is recommended for production deployments:

```bash
# OpenAI
export LLM__Provider="OpenAI"
export LLM__ApiKey="sk-..."
export LLM__ModelId="gpt-4o"

# Azure OpenAI
export LLM__Provider="AzureOpenAI"
export LLM__ApiKey="your-azure-api-key"
export LLM__AzureEndpoint="https://your-resource.openai.azure.com"
export LLM__AzureDeployment="your-deployment-name"
```

Note: Environment variables use double underscores (`__`) as section separators in .NET.

## Development Mode (No LLM)

If no `LLM:ApiKey` is configured, the application will fall back to `SimplePlannerService`, which generates a basic static plan. This is useful for development and testing without incurring LLM API costs.

## Features

### Intelligent Plan Generation

The LLM-powered planner analyzes:
- **Issue context**: Title and description from the GitHub issue
- **Repository analysis**: Detected languages, build tools, test frameworks, and key files
- **Custom instructions**: Optional markdown files from `.github/open-copilot/` directory

### JSON Schema Enforcement

The planner uses OpenAI's JSON mode (or equivalent) to enforce structured responses that match the `AgentPlan` schema:

```json
{
  "problemSummary": "Concise task summary",
  "constraints": ["Constraint 1", "Constraint 2"],
  "steps": [
    {
      "id": "step-1",
      "title": "Step title",
      "details": "Detailed description",
      "done": false
    }
  ],
  "checklist": ["Verification item 1", "Verification item 2"],
  "fileTargets": ["path/to/file1.cs", "path/to/file2.cs"]
}
```

### Custom Instructions

You can provide issue-specific or repository-wide instructions by creating markdown files in `.github/open-copilot/`:

1. **Issue-specific**: `.github/open-copilot/{issueNumber}.md`
2. **General instructions**: `.github/open-copilot/instructions.md`
3. **Fallback**: `.github/open-copilot/README.md`

Example `.github/open-copilot/instructions.md`:

```markdown
# Coding Guidelines

- Use async/await for all I/O operations
- Follow the repository's existing naming conventions
- Add XML documentation comments for public APIs
- Ensure all new code has test coverage
- Use dependency injection for services
```

## Repository Analysis

The `RepositoryAnalyzer` automatically detects:
- **Programming languages** and their usage (via GitHub API)
- **Build tools** (npm, dotnet, Maven, Gradle, Cargo, etc.)
- **Test frameworks** (xUnit, NUnit, Jest, pytest, etc.)
- **Key configuration files** (package.json, .csproj, Cargo.toml, etc.)

This context is included in the planner prompt to help the LLM generate more accurate and context-aware implementation plans.

## Model Recommendations

### For Planning (Premium Models)

- **OpenAI GPT-4o**: Best balance of cost and quality
- **OpenAI GPT-4 Turbo**: Excellent for complex planning
- **Azure OpenAI GPT-4**: Same quality as OpenAI with Azure integration

Planning uses relatively low token counts (typically 1000-4000 tokens per plan), so the cost impact is minimal.

### Temperature Setting

The planner uses `Temperature = 0.3` for more deterministic and consistent planning. This ensures:
- Structured, logical step-by-step plans
- Reduced hallucination
- More consistent output quality

## Security Considerations

- **Never commit API keys** to source control
- Use environment variables or secure secret management in production
- Rotate API keys regularly
- Monitor API usage and set spending limits
- Consider using Azure Managed Identity for Azure OpenAI in production

## Troubleshooting

### Plan generation fails or returns fallback plan

1. Check that `LLM:ApiKey` is configured correctly
2. Verify your API key has sufficient credits/quota
3. Check application logs for detailed error messages
4. Ensure the model ID is valid and accessible with your API key

### Azure OpenAI connection issues

1. Verify `AzureEndpoint` URL is correct
2. Ensure `AzureDeployment` matches your deployment name
3. Check that your API key has access to the deployment
4. Verify network connectivity to Azure OpenAI endpoint

## Cost Optimization

- Use the fallback `SimplePlannerService` for development/testing
- Configure appropriate rate limits in your LLM provider dashboard
- Monitor token usage via application logging
- Consider caching plans for similar issues (future enhancement)
