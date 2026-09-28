# Using and Maintaining the Template

## Start a project from GitHub

1. On the template repository, click **Use this template** → create your repository.
2. Clone it and run `./scripts/init-project.ps1 -Name Acme.Orders`. The script runs the same template engine in place: it renames everything, generates fresh ids, and removes the template-only files (`.template.config`, `template-pack`, the release and template workflows, and the script itself).
3. `dotnet build Acme.Orders.slnx`, `dotnet test --solution Acme.Orders.slnx`, commit, push.

## Install as a `dotnet new` template

From a clone of this repository:

```bash
dotnet new install ./
```

Or pack it and share the package (for example through an internal NuGet feed):

```bash
dotnet pack template-pack -o artifacts
dotnet new install artifacts/CleanArchitecture.Template.1.0.0.nupkg
```

## Create a project

```bash
dotnet new ca-api -n Acme.Orders
cd Acme.Orders
dotnet tool restore
dotnet test --solution Acme.Orders.slnx
```

What the template replaces:

- `CleanArchitecture` → the project name, in namespaces, project and folder names, the solution, and settings.
- `clean-architecture` → the kebab-case name (database name, compose project, JWT issuer).
- The user-secrets id and the Docker Compose project GUID get fresh values.

After creating a project:

1. Put a real `Jwt:Secret` in user secrets: `dotnet user-secrets set "Jwt:Secret" "<32+ random characters>" --project src/Acme.Orders.Api`.
2. Review `PermissionProvider` and `Permissions` for the product's roles.
3. Decide whether the sample `Todos` feature stays (see below).
4. Initialize git and push; CI (`.github/workflows/build.yml`) builds and runs all tests.

## Removing the sample `Todos` feature

Delete these, then regenerate the initial migration and keep its hand-written audit trigger block:

- `src/*.Domain/Todos`, `src/*.Application/Todos`, `src/*.Infrastructure/Todos`, `src/*.Api/Endpoints/Todos`
- the `TodoItems` `DbSet`, the `ITodoItemStore` registration, `Tags.Todos`, and `todos:*` permissions
- `tests/*.UnitTests/Todos`, `Fakes/InMemoryTodoItemStore.cs`, `TestData.NewTodo`, and the todo-based integration tests (`Todos/`, `Tenancy/`) — replace the tenancy tests with ones for your first tenant-owned entity

## Releasing the template

Push a tag such as `v3.2.0`. The `Release template` workflow tests the repository, packs the template with that version, verifies that a generated project builds, publishes the package to GitHub Packages, and attaches it to a GitHub release.

To install from GitHub Packages, add the feed once (a personal access token with `read:packages`):

```bash
dotnet nuget add source https://nuget.pkg.github.com/<owner>/index.json --name github --username <user> --password <token>
dotnet new install CleanArchitecture.Template
```

## Repository workflows

| Workflow | Where it runs | Purpose |
|---|---|---|
| `build.yml` | template and generated projects | build, test, publish |
| `template.yml` | template repository only | pack, generate a project, build and test it |
| `release.yml` | template repository only | publish the package on `v*` tags |

## Updating the template

- Change the template repository, run `dotnet test --solution CleanArchitecture.slnx`, bump `PackageVersion` in `template-pack/CleanArchitecture.Template.csproj`, and republish.
- Existing projects do not update automatically; port changes deliberately.
- Keep `.claude/skills`, `CLAUDE.md`, and `docs/` in step with the code — the skills are how new code keeps the same shape.
