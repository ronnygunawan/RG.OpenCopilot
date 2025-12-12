using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for FileAnalyzer that test realistic code samples
/// </summary>
public class FileAnalyzerIntegrationTests {
    [Fact]
    public async Task AnalyzeFileAsync_ComplexCSharpClass_ExtractsAllElements() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("ComplexService.cs", """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Microsoft.Extensions.Logging;

            namespace MyApp.Services {
                /// <summary>
                /// A complex service demonstrating various C# features
                /// </summary>
                public sealed class ComplexService : IComplexService {
                    private readonly ILogger<ComplexService> _logger;
                    private readonly IDependency _dependency;

                    public ComplexService(ILogger<ComplexService> logger, IDependency dependency) {
                        _logger = logger;
                        _dependency = dependency;
                    }

                    public async Task<Result> ProcessAsync(Request request) {
                        _logger.LogInformation("Processing request");
                        return await Task.FromResult(new Result());
                    }

                    public void SyncMethod() {
                        _logger.LogDebug("Synchronous method called");
                    }

                    protected internal virtual void ProtectedMethod() { }

                    private static string PrivateStaticMethod() => "";
                }

                public interface IComplexService {
                    Task<Result> ProcessAsync(Request request);
                    void SyncMethod();
                }

                public record Request {
                    public string Name { get; init; } = "";
                }

                public record Result;
            }
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "ComplexService.cs");

        // Assert
        result.Language.ShouldBe("csharp");
        result.Namespaces.ShouldContain("MyApp.Services");
        
        result.Imports.Count.ShouldBeGreaterThan(0);
        result.Imports.ShouldContain("System");
        result.Imports.ShouldContain("System.Collections.Generic");
        
        result.Classes.Count.ShouldBeGreaterThan(0);
        result.Classes.ShouldContain(c => c.Contains("ComplexService"));
        result.Classes.ShouldContain(c => c.Contains("IComplexService"));
        result.Classes.ShouldContain(c => c.Contains("Request"));
        result.Classes.ShouldContain(c => c.Contains("Result"));
        
        result.Functions.Count.ShouldBeGreaterThan(0);
        result.Functions.ShouldContain("ProcessAsync");
        result.Functions.ShouldContain("SyncMethod");
    }

    [Fact]
    public async Task AnalyzeFileAsync_ReactComponent_ParsesCorrectly() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("UserProfile.tsx", """
            import React, { useState, useEffect } from 'react';
            import { User } from '../types';
            import { fetchUser } from '../api';
            import './UserProfile.css';

            interface UserProfileProps {
                userId: string;
            }

            export const UserProfile: React.FC<UserProfileProps> = ({ userId }) => {
                const [user, setUser] = useState<User | null>(null);
                const [loading, setLoading] = useState(true);

                useEffect(() => {
                    loadUser();
                }, [userId]);

                const loadUser = async () => {
                    setLoading(true);
                    const userData = await fetchUser(userId);
                    setUser(userData);
                    setLoading(false);
                };

                const handleRefresh = () => {
                    loadUser();
                };

                if (loading) return <div>Loading...</div>;
                if (!user) return <div>User not found</div>;

                return (
                    <div className="user-profile">
                        <h1>{user.name}</h1>
                        <button onClick={handleRefresh}>Refresh</button>
                    </div>
                );
            };

            export default UserProfile;
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "UserProfile.tsx");

        // Assert
        result.Language.ShouldBe("typescript");
        result.Imports.ShouldContain("react");
        result.Imports.ShouldContain("../types");
        result.Imports.ShouldContain("../api");
        result.Functions.ShouldContain("loadUser");
        result.Functions.ShouldContain("handleRefresh");
    }

    [Fact]
    public async Task AnalyzeFileAsync_PythonDjangoModel_ParsesCorrectly() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("models.py", """
            from django.db import models
            from django.contrib.auth.models import User
            from datetime import datetime

            class BlogPost(models.Model):
                title = models.CharField(max_length=200)
                content = models.TextField()
                author = models.ForeignKey(User, on_delete=models.CASCADE)
                created_at = models.DateTimeField(auto_now_add=True)
                updated_at = models.DateTimeField(auto_now=True)
                published = models.BooleanField(default=False)

                class Meta:
                    ordering = ['-created_at']
                    verbose_name = 'Blog Post'
                    verbose_name_plural = 'Blog Posts'

                def __str__(self):
                    return self.title

                def publish(self):
                    self.published = True
                    self.save()

                async def async_publish(self):
                    await self.async_save()

            class Comment(models.Model):
                post = models.ForeignKey(BlogPost, on_delete=models.CASCADE, related_name='comments')
                author = models.CharField(max_length=100)
                content = models.TextField()
                created_at = models.DateTimeField(auto_now_add=True)

                def __str__(self):
                    return f'Comment by {self.author} on {self.post.title}'
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "models.py");

        // Assert
        result.Language.ShouldBe("python");
        result.Imports.ShouldContain("django.db");
        result.Imports.ShouldContain("django.contrib.auth.models");
        result.Imports.ShouldContain("datetime");
        
        result.Classes.ShouldContain("BlogPost");
        result.Classes.ShouldContain("Comment");
        // Note: Nested classes like Meta are not currently parsed by the simple regex-based parser
        
        result.Functions.ShouldContain("__str__");
        result.Functions.ShouldContain("publish");
        result.Functions.ShouldContain("async_publish");
    }

    [Fact]
    public async Task BuildFileTreeAsync_RealProjectStructure_BuildsCorrectTree() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFindResult(".", """
            ./README.md
            ./LICENSE
            ./.gitignore
            ./src/Program.cs
            ./src/Services/UserService.cs
            ./src/Services/EmailService.cs
            ./src/Models/User.cs
            ./src/Models/Email.cs
            ./tests/UserServiceTests.cs
            ./tests/EmailServiceTests.cs
            ./docs/architecture.md
            ./docs/api.md
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.BuildFileTreeAsync("test-container", ".");

        // Assert
        result.AllFiles.Count.ShouldBe(12);
        result.AllFiles.ShouldContain("README.md");
        result.AllFiles.ShouldContain("src/Program.cs");
        result.AllFiles.ShouldContain("src/Services/UserService.cs");
        result.AllFiles.ShouldContain("tests/UserServiceTests.cs");
        result.AllFiles.ShouldContain("docs/architecture.md");
        
        result.Root.ShouldNotBeNull();
        result.Root.IsDirectory.ShouldBeTrue();
        result.Root.Children.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ListFilesAsync_WithWildcardPatterns_FindsAllMatches() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFindResult("*.cs", """
            ./Program.cs
            ./Startup.cs
            ./Services/UserService.cs
            ./Services/EmailService.cs
            ./Models/User.cs
            ./Controllers/UserController.cs
            ./Tests/UserServiceTests.cs
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.ListFilesAsync("test-container", "*.cs");

        // Assert
        result.Count.ShouldBe(7);
        result.ShouldContain("Program.cs");
        result.ShouldContain("Services/UserService.cs");
        result.ShouldContain("Controllers/UserController.cs");
        result.ShouldContain("Tests/UserServiceTests.cs");
    }

    [Fact]
    public async Task ListFilesAsync_TestFilesPattern_FindsOnlyTests() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFindResult("*Tests.cs", """
            ./Tests/UserServiceTests.cs
            ./Tests/EmailServiceTests.cs
            ./Tests/Integration/ApiTests.cs
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.ListFilesAsync("test-container", "*Tests.cs");

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldAllBe(f => f.Contains("Tests"));
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class TestContainerManagerForFileAnalyzer : IContainerManager {
        private readonly Dictionary<string, string> _fileContents = new();
        private readonly Dictionary<string, string> _findResults = new();

        public void SetFileContent(string path, string content) {
            _fileContents[path] = content;
        }

        public void SetFindResult(string pattern, string output) {
            _findResults[pattern] = output;
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-container");
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, ContainerImageType imageType, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-container");
        }

        public Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default) {
            if (command == "find") {
                string key;
                if (args.Length > 3 && args[2] == "f" && args[3] == "-name") {
                    key = args[4];
                } else if (args.Length > 2 && args[1] == "-type" && args[2] == "f") {
                    key = args[0];
                } else {
                    key = args.Length > 3 ? args[3] : args[0];
                }
                
                if (_findResults.TryGetValue(key, out var result)) {
                    return Task.FromResult(new CommandResult {
                        ExitCode = 0,
                        Output = result,
                        Error = ""
                    });
                }
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = "",
                    Error = ""
                });
            }

            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "success",
                Error = ""
            });
        }

        public Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
            if (_fileContents.TryGetValue(filePath, out var content)) {
                return Task.FromResult(content);
            }
            return Task.FromResult("");
        }

        public Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CommitAndPushAsync(string containerId, string commitMessage, string owner, string repo, string branch, string token, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<bool> DirectoryExistsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(true);
        }

        public Task MoveAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CopyAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string containerId, string path, bool recursive = false, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<List<string>> ListContentsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<string>());
        }

        public Task<BuildToolsStatus> VerifyBuildToolsAsync(string containerId, CancellationToken cancellationToken = default) {
            return Task.FromResult(new BuildToolsStatus {
                DotnetAvailable = true,
                NpmAvailable = true,
                GradleAvailable = true,
                MavenAvailable = true,
                GoAvailable = true,
                CargoAvailable = true,
                MissingTools = []
            });
        }
    }
}
