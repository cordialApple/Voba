param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Integration', 'Smoke', 'App')]
    [string]$Task
)

$ErrorActionPreference = 'Stop'
$credentialPath = Join-Path $env:LOCALAPPDATA 'Voba\demo-credentials.dpapi'
if (-not (Test-Path -LiteralPath $credentialPath)) {
    throw "Protected demo credential missing: $credentialPath. Configure Atlas access first."
}
$encrypted = [IO.File]::ReadAllBytes($credentialPath)
$plain = [Security.Cryptography.ProtectedData]::Unprotect(
    $encrypted, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
try {
    $credential = [Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json
}
finally {
    [Array]::Clear($plain, 0, $plain.Length)
}

$username = [Uri]::EscapeDataString($credential.username)
$password = [Uri]::EscapeDataString($credential.password)
$uri = "mongodb+srv://${username}:${password}@$($credential.host)/?retryWrites=true&w=majority"
$names = @('VOBA_MONGO_CONNECTION_STRING', 'VOBA_MONGO_DATABASE',
    'VOBA_TEST_MONGO_URI', 'VOBA_TEST_MONGO_DATABASE')
$previous = @{}
foreach ($name in $names) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    $env:VOBA_MONGO_CONNECTION_STRING = $uri
    $env:VOBA_MONGO_DATABASE = 'VobaDemo'
    $env:VOBA_TEST_MONGO_URI = $uri
    $env:VOBA_TEST_MONGO_DATABASE = 'VobaDemoTests'

    switch ($Task) {
        'Integration' {
            dotnet test (Join-Path $PSScriptRoot '..\Voba.Persistence.IntegrationTests\Voba.Persistence.IntegrationTests.csproj')
        }
        'Smoke' {
            dotnet run --project (Join-Path $PSScriptRoot '..\Voba.ModelSmoke\Voba.ModelSmoke.csproj')
        }
        'App' {
            dotnet build (Join-Path $PSScriptRoot '..\Voba\Voba.csproj') -t:Run -f net9.0-windows10.0.19041.0
        }
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Voba $Task failed with exit code $LASTEXITCODE."
    }
}
finally {
    foreach ($name in $names) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
}
