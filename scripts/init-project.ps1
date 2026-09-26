<#
.SYNOPSIS
    Turns a repository created with GitHub's "Use this template" into a named product:
    renames CleanArchitecture.* everywhere, generates fresh secrets ids, and removes the
    template-only files. Runs the same `dotnet new ca-api` template the package uses.

.EXAMPLE
    ./scripts/init-project.ps1 -Name Acme.Orders

.NOTES
    Commit or stash your changes first: every file except .git is replaced.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$')]
    [string] $Name
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not (Test-Path (Join-Path $repositoryRoot ".template.config/template.json"))) {
    throw "This repository has already been initialized (no .template.config found)."
}

$workDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("ca-init-" + [guid]::NewGuid().ToString("N"))
$hive = Join-Path $workDirectory "hive"
$output = Join-Path $workDirectory "output"

try {
    Write-Host "Generating $Name from the template ..."
    dotnet new install $repositoryRoot --debug:custom-hive $hive | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Installing the template failed." }

    dotnet new ca-api --name $Name --output $output --debug:custom-hive $hive | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Generating the project failed." }

    Write-Host "Replacing repository contents (keeping .git) ..."
    Get-ChildItem -Path $repositoryRoot -Force |
        Where-Object { $_.Name -ne ".git" } |
        Remove-Item -Recurse -Force

    Get-ChildItem -Path $output -Force | Copy-Item -Destination $repositoryRoot -Recurse -Force

    Push-Location $repositoryRoot
    try {
        dotnet tool restore | Out-Null
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Host "Done. Next steps:" -ForegroundColor Green
    Write-Host "  dotnet build $Name.slnx"
    Write-Host "  dotnet test --solution $Name.slnx"
    Write-Host "  git add -A; git commit -m `"Initialize $Name from template`""
}
finally {
    Remove-Item -Recurse -Force $workDirectory -ErrorAction SilentlyContinue
}
