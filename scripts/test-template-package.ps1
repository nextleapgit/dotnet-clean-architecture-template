param([Parameter(Mandatory = $true)][string] $PackagePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
try {
    $files = @($package.Entries | Where-Object { $_.FullName.StartsWith('content/') -and -not $_.FullName.EndsWith('/') } | ForEach-Object { $_.FullName.Substring(8) })
    if ($files.Count -lt 100) { throw 'The template is missing its source files.' }
    $rootFiles = @('CleanArchitecture.slnx','Directory.Build.props','Directory.Packages.props','global.json','dotnet-tools.json','launchSettings.json','docker-compose.yml','docker-compose.override.yml','docker-compose.dcproj','.editorconfig','.gitattributes','.gitignore','.dockerignore','README.md','CLAUDE.md')
    foreach ($file in $files) {
        if ($file -match '(^|/)(bin|obj|artifacts|nextleapoperations|\.containers|\.git|TestResults)(/|$)' -or $file -match '\.(pfx|p12|pem|nupkg)$|(^|/)\.env') {
            throw "Forbidden package entry: $file"
        }
        if ($file -notmatch '^(src|tests|docs|scripts|\.claude|\.github|\.template.config)/' -and $file -notin $rootFiles) {
            throw "Unexpected package entry: $file"
        }
    }
    foreach ($required in @('CleanArchitecture.slnx','.template.config/template.json','src/CleanArchitecture.Api/Program.cs','src/CleanArchitecture.Api/Dockerfile','tests/CleanArchitecture.IntegrationTests/CleanArchitecture.IntegrationTests.csproj')) {
        if ($required -notin $files) { throw "Missing package entry: $required" }
    }
    Write-Host "Verified $($files.Count) template files; no nested projects, build artifacts or certificate files."
}
finally { $package.Dispose() }
