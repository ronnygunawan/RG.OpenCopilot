# RG.OpenCopilot Setup Guide

This guide explains how to set up and test RG.OpenCopilot.

## What's Implemented

The implementation includes the following key components:

1. **Webhook Handler** - Receives GitHub webhook events for issues
2. **GitHub Integration** - Creates branches and pull requests via Octokit
3. **LLM Planner** - Generates structured plans using Semantic Kernel (with fallback to simple planner)
4. **Executor Service** - Clones repositories, executes commands, commits, and posts PR comments
5. **Repository Analyzer** - Detects languages, build tools, and test frameworks
6. **Task Store** - In-memory storage for agent tasks
7. **Webhook Validation** - HMAC-SHA256 signature verification for security
8. **Comprehensive Tests** - 39 tests covering all major components

## Architecture

When an issue is labeled with `copilot-assisted`:

1. The webhook handler receives the event
2. Validates the webhook signature (if configured)
3. Creates an `AgentTask` for the issue
4. Creates a working branch (e.g., `open-copilot/issue-123`)
5. Creates a WIP PR with the initial issue prompt
6. Analyzes the repository to understand its structure
7. Loads custom instructions (if available)
8. Calls the planner to generate a structured plan
9. Updates the PR description with the plan and checklist
10. (Optional) Executor service clones repo, makes changes, commits, and posts progress

## Configuration

Configure the application using `appsettings.json` or environment variables:

```json
{
  "GitHub": {
    "Token": "your-github-personal-access-token",
    "WebhookSecret": "your-webhook-secret",
    "AppId": "123456",
    "AppPrivateKey": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----"
  },
  "LLM": {
    "Provider": "OpenAI",
    "ApiKey": "your-openai-api-key",
    "ModelId": "gpt-4o"
  }
}
```

### GitHub Personal Access Token (for Development)

For development/testing, use a personal access token with the following permissions:

- `repo` - Full control of private repositories
- `workflow` - Update GitHub Action workflows

Create a token at: https://github.com/settings/tokens

### GitHub App Credentials (for Production)

For production use with a GitHub App:

1. Create a GitHub App with the required permissions (see below)
2. Generate a private key for the app
3. Configure `GitHub:AppId` and `GitHub:AppPrivateKey` in your settings

The executor service will use installation tokens for repository access when App credentials are configured.

### LLM Configuration

For intelligent planning, configure an LLM provider:

- **Provider**: `OpenAI` or `AzureOpenAI`
- **ApiKey**: Your API key
- **ModelId**: Model to use (e.g., `gpt-4o`, `gpt-5`)

See [LLM-CONFIGURATION.md](LLM-CONFIGURATION.md) for details.

### Webhook Secret

This is optional but recommended for production. Generate a random secret and configure it both in the app settings and in your GitHub webhook configuration.

## Running the POC

### Build and Run

```bash
cd /path/to/RG.OpenCopilot
dotnet build
dotnet run --project RG.OpenCopilot.App
```

The app will start on `http://localhost:5272` (or the port shown in console).

### Test the Health Endpoint

```bash
curl http://localhost:5272/health
```

Should return: `ok`

### Test the Webhook Endpoint Locally

Create a test webhook payload file (`test-webhook.json`):

```json
{
  "action": "labeled",
  "label": {
    "name": "copilot-assisted"
  },
  "issue": {
    "number": 1,
    "title": "Test Issue",
    "body": "This is a test issue to verify the POC works correctly."
  },
  "repository": {
    "name": "test-repo",
    "full_name": "owner/test-repo",
    "owner": {
      "login": "owner"
    }
  },
  "installation": {
    "id": 12345
  }
}
```

Send the webhook (without signature validation):

```bash
curl -X POST http://localhost:5272/github/webhook \
  -H "Content-Type: application/json" \
  -H "X-GitHub-Event: issues" \
  -d @test-webhook.json
```

**Note:** To test with signature validation, you need to:
1. Set the `GitHub:WebhookSecret` in your configuration
2. Generate a valid HMAC-SHA256 signature for the payload
3. Include it in the `X-Hub-Signature-256` header

## Running Tests

```bash
dotnet test
```

All tests should pass. Current test coverage includes:
- `AgentPlanTests` - Tests for domain models
- `WebhookHandlerTests` - Tests for webhook processing
- `WebhookValidatorTests` - Tests for signature validation

## Using with GitHub

### Setting Up a GitHub App

1. Go to your organization settings → Developer settings → GitHub Apps
2. Create a new GitHub App with:
   - **Webhook URL**: `https://your-domain.com/github/webhook`
   - **Webhook secret**: Your configured secret
   - **Permissions**:
     - Repository contents: Read & write
     - Pull requests: Read & write
     - Issues: Read & write
   - **Subscribe to events**:
     - Issues
3. Install the app on repositories where you want to use the agent

### Testing with a Real Issue

1. Create an issue in a repository where the app is installed
2. Add the `copilot-assisted` label to the issue
3. The app will:
   - Create a branch `open-copilot/issue-<number>`
   - Create a WIP PR
   - Generate a plan
   - Update the PR description

## Current Implementation Status

The following features are now implemented:

- ✅ **Webhook Handler** - Receives GitHub webhook events for issues
- ✅ **GitHub Integration** - Creates branches and pull requests via Octokit
- ✅ **Planner Service** - Generates structured plans (with LLM support via Semantic Kernel)
- ✅ **Executor Service** - Clones repositories, executes commands, commits, and posts PR comments
- ✅ **Repository Analysis** - Analyzes repos to detect languages, build tools, and test frameworks
- ✅ **Task Store** - In-memory storage for agent tasks
- ✅ **Webhook Validation** - HMAC-SHA256 signature verification for security
- ✅ **Comprehensive Tests** - 39 tests covering all major components

## Next Steps (Future Enhancements)

The following features are planned for future releases:

- **LLM-Driven Code Changes** - Integrate executor with LLM to actually modify code based on plan steps
- **Background Jobs** - Process webhooks asynchronously for better scalability
- **Persistent Storage** - Store tasks in a database instead of in-memory
- **PR Updates** - Update PR descriptions as work progresses
- **Completion Logic** - Remove [WIP] tag and finalize PR when done
- **Docker Sandboxing** - Run commands in isolated Docker containers for enhanced security

For detailed information about the executor service, see [EXECUTOR-SERVICE.md](EXECUTOR-SERVICE.md).

## Troubleshooting

### The app doesn't create a branch or PR

- Check that your GitHub token has the correct permissions
- Verify the repository owner and name in the webhook payload are correct
- Check the app logs for error messages

### Webhook validation fails

- Ensure the webhook secret matches between your configuration and GitHub
- Verify the signature is being sent in the `X-Hub-Signature-256` header
- Check that the payload exactly matches what was signed (no modifications)

### Tests fail

Run with verbose output to see details:

```bash
dotnet test --verbosity normal
```

## License

See LICENSE file in the repository root.
