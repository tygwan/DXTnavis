# Contributing to BIM-DXPlatform

First off, thank you for considering contributing to BIM-DXPlatform! 🎉

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Commit Guidelines](#commit-guidelines)
- [Pull Request Process](#pull-request-process)

---

## Code of Conduct

This project adheres to a Code of Conduct. By participating, you are expected to uphold this code.

### Our Standards

**Examples of behavior that contributes to creating a positive environment:**
- ✅ Using welcoming and inclusive language
- ✅ Being respectful of differing viewpoints
- ✅ Gracefully accepting constructive criticism
- ✅ Focusing on what is best for the community

**Examples of unacceptable behavior:**
- ❌ Trolling, insulting/derogatory comments, and personal attacks
- ❌ Public or private harassment
- ❌ Publishing others' private information without permission

---

## How Can I Contribute?

### 🐛 Reporting Bugs

Before creating bug reports, please check existing issues. When creating a bug report, include:

- **Clear title and description**
- **Steps to reproduce**
- **Expected vs. actual behavior**
- **Screenshots** (if applicable)
- **Environment details** (Revit version, OS, .NET version)

**Bug Report Template:**
```markdown
**Describe the bug**
A clear and concise description of what the bug is.

**To Reproduce**
1. Open Revit 2025
2. Click on 'Snapshot Capture'
3. See error

**Expected behavior**
What you expected to happen.

**Screenshots**
If applicable, add screenshots.

**Environment:**
 - OS: [e.g. Windows 11]
 - Revit Version: [e.g. 2025.1]
 - DXrevit Version: [e.g. 0.1.0]

**Additional context**
Add any other context about the problem.
```

### 💡 Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear title**
- **Provide a detailed description** of the suggested enhancement
- **Explain why this enhancement would be useful**
- **List potential use cases**

### 🔧 Code Contributions

#### Areas We Need Help

- **Documentation**: Improve user guides and API docs
- **Testing**: Write unit tests and integration tests
- **Localization**: Translate UI to other languages
- **Bug Fixes**: Tackle issues labeled `good-first-issue`
- **Features**: Implement features from the roadmap

---

## Development Setup

### Prerequisites

- Visual Studio 2022 (17.8+)
- .NET 8.0 SDK
- Autodesk Revit 2025
- Git

### Setup Steps

1. **Fork and clone the repository**
   ```bash
   git clone https://github.com/YOUR-USERNAME/BIM-DXPlatform.git
   cd BIM-DXPlatform
   ```

2. **Create a branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Build the solution**
   ```bash
   dotnet restore
   dotnet build
   ```

4. **Make your changes**

5. **Test your changes**
   - Build the project
   - Test in Revit 2025
   - Check logs for errors

6. **Commit and push**
   ```bash
   git add .
   git commit -m "feat: Add your feature"
   git push origin feature/your-feature-name
   ```

For detailed setup instructions, see [Development Setup Guide](docs/dev/setup-guide.md).

---

## Coding Standards

### C# Conventions

Follow the [.NET Runtime Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md):

```csharp
// ✅ Good
public class SnapshotService
{
    private readonly ILogger _logger;

    public async Task<bool> SaveSnapshotAsync(SnapshotDTO snapshot)
    {
        try
        {
            // Implementation
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save snapshot");
            return false;
        }
    }
}

// ❌ Bad
public class snapshotservice
{
    public bool SaveSnapshot(SnapshotDTO snapshot)
    {
        // Missing error handling
        // Blocking call instead of async
    }
}
```

### Naming Conventions

- **Classes/Methods**: PascalCase (`SnapshotService`, `SaveSnapshotAsync`)
- **Private fields**: _camelCase (`_logger`, `_httpClient`)
- **Local variables**: camelCase (`snapshot`, `elementCount`)
- **Constants**: PascalCase (`MaxRetryCount`, `DefaultTimeout`)

### Code Quality

- ✅ Write XML documentation for public APIs
- ✅ Add error handling and logging
- ✅ Follow async/await pattern for I/O operations
- ✅ Keep methods under 50 lines
- ✅ Use meaningful variable names

```csharp
/// <summary>
/// Extracts BIM data from the current Revit document.
/// </summary>
/// <param name="document">The Revit document to extract data from.</param>
/// <returns>A collection of extracted elements as DTOs.</returns>
public async Task<List<ElementDTO>> ExtractDataAsync(Document document)
{
    // Implementation
}
```

---

## Commit Guidelines

We follow [Conventional Commits](https://www.conventionalcommits.org/) specification.

### Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- **feat**: New feature
- **fix**: Bug fix
- **docs**: Documentation changes
- **style**: Code style changes (formatting, no logic change)
- **refactor**: Code refactoring
- **perf**: Performance improvements
- **test**: Adding or updating tests
- **chore**: Maintenance tasks

### Examples

```bash
# Feature
feat(dxrevit): Add progress tracking to snapshot capture

# Bug fix
fix(dxbase): Resolve circular dependency in ConfigurationService

# Documentation
docs(readme): Update installation instructions

# Refactor
refactor(dxrevit): Extract data extraction logic into separate service
```

### Commit Message Rules

- ✅ Use imperative mood ("Add feature" not "Added feature")
- ✅ Don't capitalize first letter
- ✅ No period at the end
- ✅ Keep subject line under 72 characters
- ✅ Provide context in body if needed

---

## Pull Request Process

### Before Submitting

- [ ] Code builds without errors
- [ ] All tests pass
- [ ] Code follows style guidelines
- [ ] Documentation is updated
- [ ] Commit messages follow conventions

### PR Title Format

Use the same format as commit messages:

```
feat(dxrevit): Add real-time synchronization
```

### PR Description Template

```markdown
## Description
Brief description of changes.

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## How Has This Been Tested?
Describe the tests you ran.

## Checklist
- [ ] Code builds successfully
- [ ] Tests pass
- [ ] Documentation updated
- [ ] No breaking changes (or documented)

## Screenshots (if applicable)
Add screenshots to demonstrate the changes.
```

### Review Process

1. **Automated checks** run (build, tests)
2. **Code review** by maintainers
3. **Feedback** addressed
4. **Approved** and merged

### After Your PR is Merged

- Your contribution will be listed in CHANGELOG.md
- You'll be added to the contributors list
- Consider joining our discussions for future features!

---

## Questions?

Feel free to ask questions in:
- [GitHub Discussions](https://github.com/tygwan/BIM-DXPlatform/discussions)
- [GitHub Issues](https://github.com/tygwan/BIM-DXPlatform/issues)

---

## Thank You!

Your contributions make BIM-DXPlatform better for everyone! 🙏
