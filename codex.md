# Codex Context

## Local LLM Visual Testing

Use the development authorization bypass when visual testing with LLM-driven browsers needs authenticated API and WebApp access without performing Steam or Discord login.

Enable it only for local development runs:

```powershell
$env:Testing__AuthorizationBypass__Enabled = "true"
dotnet run --project sources/AppHost/AppHost.csproj
```

The default impersonated user is the seeded debug administrator:

```text
b6aa28a3-a740-4e0c-b5dc-157c04881872
```

Override the impersonated user only when intentionally testing another local role scenario:

```powershell
$env:Testing__AuthorizationBypass__UserId = "<user-guid>"
```

## EF Migrations

When a new migration is needed, generate it with the EF tool. Do not hand-edit migration files to create schema changes.

```powershell
dotnet ef migrations add <Name> --project sources/Api/Api.csproj --startup-project sources/Api/Api.csproj --context ApiDbContext --output-dir Data/Migrations
```

## Git Commit Discipline

Create unsigned commits regularly after coherent units of work.

Use descriptive commit messages with a clear summary line and enough body detail to explain what changed and why. Capture important implementation decisions, not just file-level edits.

Prefer this command when signing is configured:

```powershell
git commit --no-gpg-sign
```
