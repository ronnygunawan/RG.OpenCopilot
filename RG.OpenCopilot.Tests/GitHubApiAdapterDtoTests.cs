using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;
using System.Text.Json;

namespace RG.OpenCopilot.Tests;

// Tests for DTOs using JSON serialization/deserialization
public class RepositoryInfoTests {
    [Fact]
    public void CanSerializeAndDeserialize() {
        // Arrange
        var original = new RepositoryInfo { DefaultBranch = "main" };
        
        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<RepositoryInfo>(json);
        
        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.DefaultBranch.ShouldBe("main");
    }
    
    [Fact]
    public void DefaultBranch_CanBeSet() {
        // Arrange & Act
        var info = new RepositoryInfo { DefaultBranch = "develop" };
        
        // Assert
        info.DefaultBranch.ShouldBe("develop");
    }
    
    [Fact]
    public void DefaultBranch_HasDefaultValue() {
        // Arrange & Act
        var info = new RepositoryInfo();
        
        // Assert
        info.DefaultBranch.ShouldBe("");
    }
}

public class ReferenceInfoTests {
    [Fact]
    public void CanSerializeAndDeserialize() {
        // Arrange
        var original = new ReferenceInfo { Sha = "abc123def456" };
        
        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ReferenceInfo>(json);
        
        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Sha.ShouldBe("abc123def456");
    }
    
    [Fact]
    public void Sha_CanBeSet() {
        // Arrange & Act
        var info = new ReferenceInfo { Sha = "sha789" };
        
        // Assert
        info.Sha.ShouldBe("sha789");
    }
    
    [Fact]
    public void Sha_HasDefaultValue() {
        // Arrange & Act
        var info = new ReferenceInfo();
        
        // Assert
        info.Sha.ShouldBe("");
    }
}

public class PullRequestInfoTests {
    [Fact]
    public void CanSerializeAndDeserialize() {
        // Arrange
        var original = new PullRequestInfo { Number = 42, HeadRef = "feature-branch" };
        
        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PullRequestInfo>(json);
        
        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Number.ShouldBe(42);
        deserialized.HeadRef.ShouldBe("feature-branch");
    }
    
    [Fact]
    public void Properties_CanBeSet() {
        // Arrange & Act
        var info = new PullRequestInfo { Number = 100, HeadRef = "my-branch" };
        
        // Assert
        info.Number.ShouldBe(100);
        info.HeadRef.ShouldBe("my-branch");
    }
    
    [Fact]
    public void Number_HasDefaultValue() {
        // Arrange & Act
        var info = new PullRequestInfo();
        
        // Assert
        info.Number.ShouldBe(0);
        info.HeadRef.ShouldBe("");
    }
}

public class LanguageInfoTests {
    [Fact]
    public void CanSerializeAndDeserialize() {
        // Arrange
        var original = new LanguageInfo { Name = "C#", Bytes = 10000 };
        
        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<LanguageInfo>(json);
        
        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Name.ShouldBe("C#");
        deserialized.Bytes.ShouldBe(10000);
    }
    
    [Fact]
    public void Properties_CanBeSet() {
        // Arrange & Act
        var info = new LanguageInfo { Name = "JavaScript", Bytes = 5000 };
        
        // Assert
        info.Name.ShouldBe("JavaScript");
        info.Bytes.ShouldBe(5000);
    }
    
    [Fact]
    public void HasDefaultValues() {
        // Arrange & Act
        var info = new LanguageInfo();
        
        // Assert
        info.Name.ShouldBe("");
        info.Bytes.ShouldBe(0);
    }
}

public class ContentInfoTests {
    [Fact]
    public void CanSerializeAndDeserialize() {
        // Arrange
        var original = new ContentInfo { 
            Name = "README.md", 
            Path = "docs/README.md", 
            IsDirectory = false 
        };
        
        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ContentInfo>(json);
        
        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Name.ShouldBe("README.md");
        deserialized.Path.ShouldBe("docs/README.md");
        deserialized.IsDirectory.ShouldBeFalse();
    }
    
    [Fact]
    public void Properties_CanBeSet() {
        // Arrange & Act
        var info = new ContentInfo { 
            Name = "src", 
            Path = "src", 
            IsDirectory = true 
        };
        
        // Assert
        info.Name.ShouldBe("src");
        info.Path.ShouldBe("src");
        info.IsDirectory.ShouldBeTrue();
    }
    
    [Fact]
    public void HasDefaultValues() {
        // Arrange & Act
        var info = new ContentInfo();
        
        // Assert
        info.Name.ShouldBe("");
        info.Path.ShouldBe("");
        info.IsDirectory.ShouldBeFalse();
    }
}
