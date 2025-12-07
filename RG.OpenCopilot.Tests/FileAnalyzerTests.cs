using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class FileAnalyzerTests {
    [Fact]
    public async Task AnalyzeFileAsync_CSharpFile_ParsesNamespaces() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("Test.cs", """
            using System;
            using System.Collections.Generic;

            namespace MyApp.Services {
                public class TestService {
                    public void DoSomething() { }
                }
            }
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "Test.cs");

        // Assert
        result.FilePath.ShouldBe("Test.cs");
        result.Language.ShouldBe("csharp");
        result.Namespaces.ShouldContain("MyApp.Services");
    }

    [Fact]
    public async Task AnalyzeFileAsync_CSharpFile_ParsesImports() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("Test.cs", """
            using System;
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            namespace MyApp {
                public class Test { }
            }
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "Test.cs");

        // Assert
        result.Imports.ShouldContain("System");
        result.Imports.ShouldContain("System.Collections.Generic");
        result.Imports.ShouldContain("Microsoft.Extensions.Logging");
    }

    [Fact]
    public async Task AnalyzeFileAsync_CSharpFile_ParsesClasses() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("Test.cs", """
            namespace MyApp {
                public class MyClass { }
                public interface IMyInterface { }
                public sealed class SealedClass { }
                public abstract class AbstractClass { }
                public record MyRecord { }
                public struct MyStruct { }
            }
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "Test.cs");

        // Assert
        result.Classes.ShouldContain("class MyClass");
        result.Classes.ShouldContain("interface IMyInterface");
        result.Classes.ShouldContain("class SealedClass");
        result.Classes.ShouldContain("class AbstractClass");
        result.Classes.ShouldContain("record MyRecord");
        result.Classes.ShouldContain("struct MyStruct");
    }

    [Fact]
    public async Task AnalyzeFileAsync_CSharpFile_ParsesMethods() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("Test.cs", """
            public class MyClass {
                public void PublicMethod() { }
                private void PrivateMethod() { }
                protected void ProtectedMethod() { }
                internal void InternalMethod() { }
                public async Task AsyncMethod() { }
                public static void StaticMethod() { }
            }
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "Test.cs");

        // Assert
        result.Functions.ShouldContain("PublicMethod");
        result.Functions.ShouldContain("PrivateMethod");
        result.Functions.ShouldContain("ProtectedMethod");
        result.Functions.ShouldContain("InternalMethod");
        result.Functions.ShouldContain("AsyncMethod");
        result.Functions.ShouldContain("StaticMethod");
    }

    [Fact]
    public async Task AnalyzeFileAsync_JavaScriptFile_ParsesImports() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("test.js", """
            import React from 'react';
            import { useState, useEffect } from 'react';
            const lodash = require('lodash');
            const express = require('express');
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "test.js");

        // Assert
        result.Language.ShouldBe("javascript");
        result.Imports.ShouldContain("react");
        result.Imports.ShouldContain("lodash");
        result.Imports.ShouldContain("express");
    }

    [Fact]
    public async Task AnalyzeFileAsync_JavaScriptFile_ParsesClassesAndFunctions() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("test.js", """
            class MyClass {
                constructor() {}
                myMethod() {}
            }

            function myFunction() {}

            const myArrowFunc = () => {};
            const asyncFunc = async () => {};
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "test.js");

        // Assert
        result.Classes.ShouldContain("MyClass");
        result.Functions.ShouldContain("myFunction");
        result.Functions.ShouldContain("myArrowFunc");
        result.Functions.ShouldContain("asyncFunc");
    }

    [Fact]
    public async Task AnalyzeFileAsync_PythonFile_ParsesImports() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("test.py", """
            import os
            import sys
            from datetime import datetime
            from collections import OrderedDict, Counter
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "test.py");

        // Assert
        result.Language.ShouldBe("python");
        result.Imports.ShouldContain("os");
        result.Imports.ShouldContain("sys");
        result.Imports.ShouldContain("datetime");
        result.Imports.ShouldContain("OrderedDict");
        result.Imports.ShouldContain("Counter");
    }

    [Fact]
    public async Task AnalyzeFileAsync_PythonFile_ParsesClassesAndFunctions() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("test.py", """
            class MyClass:
                def __init__(self):
                    pass
                
                def my_method(self):
                    pass

            def my_function():
                pass

            async def async_function():
                pass
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "test.py");

        // Assert
        result.Classes.ShouldContain("MyClass");
        result.Functions.ShouldContain("__init__");
        result.Functions.ShouldContain("my_method");
        result.Functions.ShouldContain("my_function");
        result.Functions.ShouldContain("async_function");
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsMatchingFiles() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFindResult("*.cs", """
            ./File1.cs
            ./Services/File2.cs
            ./Models/File3.cs
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.ListFilesAsync("test-container", "*.cs");

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldContain("File1.cs");
        result.ShouldContain("Services/File2.cs");
        result.ShouldContain("Models/File3.cs");
    }

    [Fact]
    public async Task ListFilesAsync_HandlesNoMatches() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFindResult("*.xyz", "");
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.ListFilesAsync("test-container", "*.xyz");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task BuildFileTreeAsync_CreatesCorrectStructure() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFindResult(".", """
            ./README.md
            ./src/Program.cs
            ./src/Services/MyService.cs
            ./tests/MyTests.cs
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.BuildFileTreeAsync("test-container", ".");

        // Assert
        result.AllFiles.Count.ShouldBe(4);
        result.AllFiles.ShouldContain("README.md");
        result.AllFiles.ShouldContain("src/Program.cs");
        result.AllFiles.ShouldContain("src/Services/MyService.cs");
        result.AllFiles.ShouldContain("tests/MyTests.cs");
        
        result.Root.ShouldNotBeNull();
        result.Root.IsDirectory.ShouldBeTrue();
    }

    [Fact]
    public async Task BuildFileTreeAsync_HandlesNestedDirectories() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFindResult(".", """
            ./a/b/c/file1.txt
            ./a/b/file2.txt
            ./a/file3.txt
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.BuildFileTreeAsync("test-container", ".");

        // Assert
        result.AllFiles.Count.ShouldBe(3);
        result.Root.Children.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task BuildFileTreeAsync_HandlesEmptyDirectory() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFindResult(".", "");
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.BuildFileTreeAsync("test-container", ".");

        // Assert
        result.AllFiles.ShouldBeEmpty();
        result.Root.ShouldNotBeNull();
        result.Root.IsDirectory.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeFileAsync_TypeScriptFile_DetectsCorrectLanguage() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("test.ts", """
            interface MyInterface {
                name: string;
            }

            class MyClass implements MyInterface {
                name: string = '';
            }
            """);
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "test.ts");

        // Assert
        result.Language.ShouldBe("typescript");
    }

    [Fact]
    public async Task AnalyzeFileAsync_UnknownExtension_ReturnsUnknownLanguage() {
        // Arrange
        var containerManager = new TestContainerManagerForFileAnalyzer();
        containerManager.SetFileContent("test.xyz", "some content");
        var analyzer = new FileAnalyzer(containerManager, new TestLogger<FileAnalyzer>());

        // Act
        var result = await analyzer.AnalyzeFileAsync("test-container", "test.xyz");

        // Assert
        result.Language.ShouldBe("unknown");
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

        public Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default) {
            if (command == "find") {
                // Handle both "find . -type f -name pattern" and "find . -type f"
                string key;
                if (args.Length > 3 && args[2] == "f" && args[3] == "-name") {
                    // find . -type f -name pattern
                    key = args[4];
                } else if (args.Length > 2 && args[1] == "-type" && args[2] == "f") {
                    // find . -type f
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
    }
}
