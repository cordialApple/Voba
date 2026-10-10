param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Integration', 'Smoke', 'App', 'Seed', 'Acceptance', 'ModelAcceptance', 'Backend')]
    [string]$Task
)

$ErrorActionPreference = 'Stop'
$secretDirectory = Join-Path $env:LOCALAPPDATA 'Voba'
$names = @('VOBA_MONGO_CONNECTION_STRING', 'VOBA_MONGO_DATABASE',
    'VOBA_TEST_MONGO_URI', 'VOBA_TEST_MONGO_DATABASE', 'VOBA_JWT_SECRET',
    'VOBA_SPOONACULAR_API_KEY', 'VOBA_ENRICHMENT_MODE', 'VOBA_OLLAMA_ENDPOINT',
    'VOBA_OLLAMA_MODEL', 'VOBA_API_BASE_URL', 'ASPNETCORE_URLS',
    'VOBA_TEST_LIVE_MODEL')
$previous = @{}
foreach ($name in $names) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

function Get-MongoUri {
    $credentialPath = Join-Path $secretDirectory 'demo-credentials.dpapi'
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
    return "mongodb+srv://${username}:${password}@$($credential.host)/?retryWrites=true&w=majority"
}

function Get-BackendJwtSecret {
    $secretPath = Join-Path $secretDirectory 'backend-jwt.dpapi'
    if (-not (Test-Path -LiteralPath $secretPath)) {
        $key = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
        $bytes = [Text.Encoding]::UTF8.GetBytes([Convert]::ToBase64String($key))
        try {
            $encrypted = [Security.Cryptography.ProtectedData]::Protect(
                $bytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
            [IO.File]::WriteAllBytes($secretPath, $encrypted)
            $acl = Get-Acl -LiteralPath $secretPath
            $acl.SetAccessRuleProtection($true, $false)
            foreach ($rule in @($acl.Access)) {
                $acl.RemoveAccessRuleSpecific($rule)
            }
            $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
            $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
                $identity, [Security.AccessControl.FileSystemRights]::FullControl,
                [Security.AccessControl.AccessControlType]::Allow))
            Set-Acl -LiteralPath $secretPath -AclObject $acl
        }
        finally {
            [Array]::Clear($key, 0, $key.Length)
            [Array]::Clear($bytes, 0, $bytes.Length)
        }
    }
    $encrypted = [IO.File]::ReadAllBytes($secretPath)
    $plain = [Security.Cryptography.ProtectedData]::Unprotect(
        $encrypted, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    try {
        return [Text.Encoding]::UTF8.GetString($plain)
    }
    finally {
        [Array]::Clear($plain, 0, $plain.Length)
    }
}

try {
    if ($Task -eq 'App') {
        foreach ($name in @('VOBA_MONGO_CONNECTION_STRING', 'VOBA_MONGO_DATABASE',
            'VOBA_TEST_MONGO_URI', 'VOBA_TEST_MONGO_DATABASE', 'VOBA_JWT_SECRET',
            'VOBA_SPOONACULAR_API_KEY', 'VOBA_ENRICHMENT_MODE',
            'VOBA_OLLAMA_ENDPOINT', 'VOBA_OLLAMA_MODEL')) {
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
        if ([string]::IsNullOrWhiteSpace($env:VOBA_API_BASE_URL)) {
            $env:VOBA_API_BASE_URL = 'http://127.0.0.1:5057'
        }
    }
    else {
        $uri = Get-MongoUri
        $env:VOBA_MONGO_CONNECTION_STRING = $uri
        $env:VOBA_MONGO_DATABASE = if ($Task -eq 'Backend') { 'VobaDemoTests' } else { 'VobaDemo' }
        $env:VOBA_TEST_MONGO_URI = $uri
        $env:VOBA_TEST_MONGO_DATABASE = 'VobaDemoTests'
        if ($Task -eq 'Backend') {
            $env:VOBA_JWT_SECRET = Get-BackendJwtSecret
            $env:ASPNETCORE_URLS = 'http://127.0.0.1:5057'
        }
    }

    switch ($Task) {
        'Integration' {
            dotnet test (Join-Path $PSScriptRoot '..\Voba.Persistence.IntegrationTests\Voba.Persistence.IntegrationTests.csproj')
        }
        'Smoke' {
            dotnet run --project (Join-Path $PSScriptRoot '..\Voba.ModelSmoke\Voba.ModelSmoke.csproj')
        }
        'Seed' {
            dotnet run --project (Join-Path $PSScriptRoot '..\Voba.Seed\Voba.Seed.csproj')
        }
        'Acceptance' {
            dotnet test (Join-Path $PSScriptRoot '..\Voba.Backend.AcceptanceTests\Voba.Backend.AcceptanceTests.csproj') --filter FullyQualifiedName~LiveBackendHttpTests
        }
        'ModelAcceptance' {
            $env:VOBA_TEST_LIVE_MODEL = '1'
            dotnet test (Join-Path $PSScriptRoot '..\Voba.Backend.AcceptanceTests\Voba.Backend.AcceptanceTests.csproj') --filter FullyQualifiedName~LiveGemmaHttpTests
        }
        'Backend' {
            dotnet run --no-launch-profile --project (Join-Path $PSScriptRoot '..\Voba.Backend\Voba.Backend.csproj')
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
