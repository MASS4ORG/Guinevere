# Contributing to Guinevere

Thank you for your interest in contributing to Guinevere! We welcome contributions from the community and are pleased to have you join us.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Documentation](#documentation)
- [Pull Request Process](#pull-request-process)
- [Release Process](#release-process)

## Code of Conduct

This project adheres to a code of conduct that we expect all contributors to follow. Please be respectful and constructive in all interactions.

### Our Standards

- **Be respectful**: Treat everyone with respect and kindness
- **Be inclusive**: Welcome newcomers and help them learn
- **Be constructive**: Provide helpful feedback and suggestions
- **Be patient**: Remember that everyone has different experience levels
- **Be professional**: Keep discussions focused on the project

## Getting Started

### Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 10.0 SDK** or later
- **Git** for version control

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/guinevere.git
   cd guinevere
   ```
3. Add the upstream repository:
   ```bash
   git remote add upstream https://github.com/brmassa/guinevere.git
   ```

## Development Setup

### Initial Setup

1. **Restore packages**:
   ```bash
   dotnet restore
   ```

2. **Build the solution**:
   ```bash
   ./build.sh Compile
   ```

3. **Run tests**:
   ```bash
   ./build.sh Test
   ```

### Project Structure

```
Guinevere/
├── Guinevere/                 # Core library
├── Integrations/              # Graphics API integrations
│   ├── Guinevere.OpenGL.OpenTK/
│   ├── Guinevere.OpenGL.Raylib/
│   ├── Guinevere.OpenGL.SilkNET/
│   └── Guinevere.Vulkan.SilkNET/
├── Samples/                   # Example applications
├── Guinevere.Tests/           # Unit tests
├── .nuke/                     # Build automation
└── .github/                   # GitHub workflows
```

## How to Contribute

### Types of Contributions

We welcome several types of contributions:

- **Bug fixes**: Fix issues in existing code
- **New features**: Add new functionality to the core library or integrations
- **Documentation**: Improve or add documentation
- **Examples**: Create new sample applications
- **Tests**: Add or improve test coverage
- **Performance improvements**: Optimize existing code

### Before You Start

1. **Check existing issues**: Look for related issues or feature requests
2. **Create an issue**: If none exists, create one to discuss your proposal
3. **Get feedback**: Wait for maintainer feedback before starting work
4. **Assign yourself**: Once approved, assign the issue to yourself

### Development Workflow

1. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes**:
   - Write clean, readable code
   - Follow the coding standards
   - Add tests for new functionality
   - Update documentation as needed

3. **Test your changes**:
   ```bash
   ./build.sh Test
   ./build.sh BuildSamples
   ```

4. **Commit your changes**:
   ```bash
   git add .
   git commit -m "feat: add new feature description"
   ```

5. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```

6. **Create a Pull Request**: Open a PR against the `main` branch

## Coding Standards

### C# Conventions

- **Naming**: Follow standard C# naming conventions
  - PascalCase for classes, methods, properties
  - camelCase for fields, parameters, local variables
  - Use descriptive names

- **Documentation**: Add XML documentation for public APIs
  ```csharp
  /// <summary>
  /// Renders a button with the specified text.
  /// </summary>
  /// <param name="text">The button text to display</param>
  /// <returns>True if the button was clicked</returns>
  public bool Button(string text)
  ```

- **Null handling**: Use nullable reference types appropriately
- **Async/await**: Use async patterns for I/O operations
- **Resource disposal**: Properly dispose of resources using `using` statements

### Code Style

- **Formatting**: Use consistent indentation (4 spaces)
- **Line length**: Keep lines under 120 characters when reasonable
- **Braces**: Use opening braces on the same line for control structures
- **Spacing**: Add spaces around operators and after commas

### Example

```csharp
namespace Guinevere.Core
{
    /// <summary>
    /// Represents a GUI context for rendering immediate mode interfaces.
    /// </summary>
    public class GuiContext : IDisposable
    {
        private readonly IRenderer renderer;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the GuiContext class.
        /// </summary>
        /// <param name="renderer">The renderer to use for drawing</param>
        public GuiContext(IRenderer renderer)
        {
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        }

        /// <summary>
        /// Renders a button with the specified text.
        /// </summary>
        /// <param name="text">The button text to display</param>
        /// <returns>True if the button was clicked</returns>
        public bool Button(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Button text cannot be null or empty", nameof(text));

            return renderer.DrawButton(text);
        }

        /// <summary>
        /// Disposes of the GUI context and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                renderer?.Dispose();
                disposed = true;
            }
        }
    }
}
```

## Testing

### Test Requirements

- **Unit tests**: Write unit tests for all new functionality
- **Integration tests**: Add integration tests for new integrations
- **Coverage**: Maintain test coverage above 80%
- **Test naming**: Use descriptive test method names

### Test Structure

```csharp
[TestClass]
public class GuiContextTests
{
    [TestMethod]
    public void Button_WithValidText_ReturnsExpectedResult()
    {
        // Arrange
        var mockRenderer = new Mock<IRenderer>();
        mockRenderer.Setup(r => r.DrawButton("Test")).Returns(true);
        var context = new GuiContext(mockRenderer.Object);

        // Act
        var result = context.Button("Test");

        // Assert
        Assert.IsTrue(result);
        mockRenderer.Verify(r => r.DrawButton("Test"), Times.Once);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Button_WithNullText_ThrowsArgumentException()
    {
        // Arrange
        var context = new GuiContext(Mock.Of<IRenderer>());

        // Act & Assert
        context.Button(null);
    }
}
```

### Running Tests

```bash
# Run all tests
./build.sh Test

# Run tests with coverage report
./build.sh TestReport

# Run specific test project
dotnet test Guinevere.Tests/
```

## Documentation

### Documentation Requirements

- **API documentation**: Add XML comments to all public APIs
- **README updates**: Update README.md for new features
- **Sample code**: Provide working examples for new features
- **Architecture docs**: Document significant architectural changes

### Writing Guidelines

- **Clear and concise**: Write in clear, simple language
- **Examples**: Include code examples where helpful
- **Up-to-date**: Keep documentation current with code changes
- **Accessible**: Write for developers of all experience levels

## Pull Request Process

### PR Checklist

Before submitting a pull request, ensure:

- [ ] Code follows the coding standards
- [ ] All tests pass
- [ ] New functionality has tests
- [ ] Documentation is updated
- [ ] No merge conflicts with main branch
- [ ] Commit messages follow conventional format

### Conventional Commits

Use conventional commit format for commit messages:

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

Examples:
```
feat(core): add button click animation support
fix(opengl): resolve texture binding issue
docs(readme): update installation instructions
```

### PR Review Process

1. **Automated checks**: Ensure all CI checks pass
2. **Code review**: Wait for maintainer review
3. **Address feedback**: Make requested changes
4. **Final approval**: Maintainer approves and merges

## Release Process

### Versioning

We use [Semantic Versioning](https://semver.org/):

- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Release Schedule

- **Automatic releases**: Every Thursday if there are new commits
- **Manual releases**: For urgent fixes or major features
- **Pre-releases**: For testing significant changes

### Release Assets

Each release includes:
- NuGet packages for all libraries
- Sample application packages (Windows/Linux)
- Release notes with changelog
- Source code archives

## Getting Help

### Communication Channels

- **GitHub Issues**: For bug reports and feature requests
- **GitHub Discussions**: For questions and general discussion
- **Pull Requests**: For code review and collaboration

### Questions?

If you have questions about contributing:

1. Check existing issues and discussions
2. Create a new issue with the "question" label
3. Provide context and specific details

### Resources

- [.NET Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- [Semantic Versioning](https://semver.org/)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [GitHub Flow](https://guides.github.com/introduction/flow/)

## Recognition

Contributors will be recognized in:
- Project README.md
- Release notes
- Project documentation

Thank you for contributing to Guinevere! 🎨⚡
