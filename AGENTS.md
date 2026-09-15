# Kernel Repository AGENTS.md

## Context

This is the **open-source Shinboku Polochon kernel library** repository.

- **Repository Type**: Open source (public)
- **Purpose**: Shared .NET technical kernel for domain, application, and infrastructure contracts
- **Hosting**: Planned for `https://github.com/Shinboku-io/polochon.git`
- **Package Publishing**: `Shinboku.Polochon.*` NuGet packages

## Repository Structure

```
kernel/
├── src/
│   ├── Polochon.Abstractions/    # Shared interfaces and DTOs
│   └── Polochon/                 # Core kernel library
├── tests/
│   ├── Polochon.Tests/UnitTests/                      # Unit tests for kernel components
│   ├── Polochon.Tests/IntegrationTests/              # Integration tests
│   └── Polochon.ContractTests/                  # Contract validation tests
├── samples/
│   ├── ConsoleApp/                     # Basic usage samples
│   ├── WebApi/                         # Web API integration sample
│   └── BlazorApp/                      # Blazor integration sample
├── docs/
│   ├── api/                           # Public API documentation
│   ├── architecture/                  # Architecture overview
│   ├── getting-started/               # Onboarding guides
│   └── patterns/                      # Design patterns used
└── .github/
    ├── workflows/                     # CI/CD pipelines
    └── ISSUE_TEMPLATE/                # Issue templates
```

## Purpose & Design Principles

### What This Library Provides

- **Domain contracts**: Shared domain primitives, base entities, value objects
- **Application contracts**: CQRS patterns, command/query interfaces, result types
- **Infrastructure contracts**: Repository interfaces, persistence abstractions, service registrations
- **Cross-cutting concerns**: Logging, validation, error handling patterns

### Design Philosophy

- **Framework-agnostic**: Works with any .NET application
- **Minimal dependencies**: Only essential dependencies
- **Clear boundaries**: Each package has a single responsibility
- **Backward compatible**: Semantic versioning, careful breaking changes
- **Well-documented**: Comprehensive XML docs and public documentation

## Dependencies

### External Dependencies

- **.NET**: 10.0+
- **NuGet**: Standard package manager
- **Testing**: xUnit/NUnit for tests

### Internal Dependencies

- **Package hierarchy**: 
  - `Polochon.Abstractions` (no dependencies)
  - `Polochon` (meta-package referencing all above)

## Build & Test Directives

### Solution Files

- **Primary solution**: `Shinboku.Polochon.slnx`

### Commands

- **Restore**: `dotnet restore Shinboku.Polochon.slnx`
- **Build**: `dotnet build Shinboku.Polochon.slnx --no-restore`
- **Test**: `dotnet test Shinboku.Polochon.slnx --no-build`
- **Pack**: `dotnet pack Shinboku.Polochon.slnx --no-build --output nupkgs`

### Task Integration

This repository is part of a multi-repo workspace. Use workspace-level Taskfile.yml for cross-repo operations:

- `task kernel:restore` - Restore this solution only
- `task kernel:build` - Build this solution only
- `task kernel:test` - Test this solution only
- `task restore` - Restore both solutions (workspace)
- `task build` - Build both solutions (workspace)

### Versioning & Packaging

- **Version scheme**: Semantic Versioning 2.0.0
- **Package ID pattern**: `Polochon[.Submodule]`
- **CI/CD**: Automated package publishing on release tags
- **Tag pattern**: `v<major>.<minor>.<patch>` (e.g., `v1.0.0`)

## Development Environment

### Dev Container

- **Container name**: BabelCorp School Inventory (shared with app repo)
- **Service**: Available via workspace docker-compose
- **SQL Server**: Accessible at `sqlserver:1433` for integration tests

### Tools

- **.NET SDK**: Required for build/test/pack
- **NuGet CLI**: For package management
- **Signing**: Strong-named assemblies for production

## Agent Behavior Directives

### Code Style

- **Language**: C# 12+
- **Framework**: .NET 8+
- **Formatting**: Follow .editorconfig rules (inherited from workspace)
- **Naming**: 
  - PascalCase for public types and members
  - camelCase for parameters and local variables
  - _camelCase for private fields
  - I prefix for interfaces
- **Async**: Use async/await pattern throughout
- **Nullability**: Enable nullable reference types

### File Operations

- **Read before edit**: Always read a file completely before making edits
- **Minimal changes**: Make the smallest possible change to achieve the goal
- **Style matching**: Match existing code style and patterns
- **API design**: All public APIs must have XML documentation
- **Breaking changes**: Document in CHANGELOG.md and use appropriate version bump

### Git Operations

- **Repository path**: Use `git -C /workspaces/babelcorp_school_inventory/kernel` or `cd kernel && git`
- **Protected branches**: `main`, `master`, `release/*`
- **Push policy**: 
  - Never push to protected branches without PR
  - Feature branches: `feat/<name>`, `fix/<name>`, `docs/<name>`
- **Force push**: Require explicit confirmation; prefer `--force-with-lease`
- **Blast radius**: Changes here affect all consumers (including app repo)

### Testing Strategy

- **Unit tests**: 100% coverage for public APIs
- **Integration tests**: Validate package interactions
- **Contract tests**: Ensure backward compatibility
- **Sample validation**: Samples must build and run successfully

### Public API Standards

- **Documentation**: All public types and members must have XML documentation
- **Examples**: Include `<example>` tags in XML docs where helpful
- **Deprecation**: Use `[Obsolete]` attribute with clear migration path
- **Exceptions**: Document all exceptions that can be thrown

## Package Structure

### Package Naming

```
Polochon                    # Meta-package (depends on all)
Polochon.Abstractions          # Interfaces and DTOs
```

### Package Contents

Each package should include:
- **Source code**: Compiled assemblies
- **XML documentation**: Generated docs file
- **Source link**: For debugging support
- **Symbol package**: PDB files for debugging
- **README**: Package-specific documentation

## Samples

### Sample Projects

- **ConsoleApp**: Minimal console application using kernel
- **WebApi**: ASP.NET Core Web API with kernel integration
- **BlazorApp**: Blazor application demonstrating UI patterns

### Sample Maintenance

- **Always working**: All samples must build and run
- **Documented**: Each sample has a README explaining its purpose
- **Tested**: Samples are validated in CI

## Documentation Standards

### Public Documentation (docs/)

- **API Reference**: Auto-generated from XML docs
- **Getting Started**: Step-by-step onboarding guide
- **Architecture**: High-level design decisions
- **Patterns**: Common patterns and best practices
- **Migration Guides**: For breaking changes

### Code Documentation

- **XML Documentation**: Required for all public APIs
- **Code comments**: Explain "why", not "what"
- **TODO comments**: Use `// TODO: <description>` format
- **HACK comments**: Use `// HACK: <description>` for temporary solutions

## Workspace Context

This repository is part of a **two-repository workspace** at `/workspaces/babelcorp_school_inventory/`.

- **Sibling repository**: `../app` (BabelCorp School Inventory)
- **Dependency direction**: The app depends on this kernel, not vice versa
- **Development mode**: App can reference this via source reference at `../kernel`
- **Production mode**: App consumes this via NuGet packages
- **Workspace AGENTS.md**: See `/workspaces/babelcorp_school_inventory/AGENTS.md` for workspace-wide directives

## Open Source Considerations

- **Public repository**: All code and documentation can be public
- **License**: Include appropriate OSS license (MIT, Apache 2.0, etc.)
- **Contributing**: CONTRIBUTING.md with contribution guidelines
- **Code of Conduct**: CODE_OF_CONDUCT.md for community standards
- **Issue tracking**: Use GitHub issues for bug reports and feature requests
- **Pull requests**: All changes via PR, code review required

## Quality Gates

- **Build**: All projects must build without warnings
- **Tests**: All tests must pass
- **Code analysis**: Run `dotnet format` and address warnings
- **Documentation**: All public APIs must be documented
- **Versioning**: Follow semantic versioning strictly
- **Changelog**: Maintain CHANGELOG.md for all releases

## Version Management

### Release Process

1. Update CHANGELOG.md with new version
2. Update version in all .csproj files
3. Create and push git tag: `v<major>.<minor>.<patch>`
4. CI/CD automatically builds and publishes packages
5. Create GitHub release with release notes

### Version Bumping Rules

- **Major**: Breaking changes to public APIs
- **Minor**: New features, backward compatible
- **Patch**: Bug fixes, backward compatible
