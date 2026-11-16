param([Parameter(Mandatory = $true)][string]$BaseUrl)
$ErrorActionPreference = 'Stop'
$uri = $null
if (-not [Uri]::TryCreate($BaseUrl.Trim(), [UriKind]::Absolute, [ref]$uri) -or
    $uri.Scheme -ne 'https' -or $uri.IsLoopback -or $uri.UserInfo -or $uri.Query -or
    $uri.Fragment -or $uri.AbsolutePath -ne '/') {
    throw 'Use only the backend HTTPS origin, for example https://game-api.up.railway.app (without /api).'
}
$url = $uri.GetLeftPart([UriPartial]::Authority)
$repoRoot = Split-Path $PSScriptRoot -Parent
$flowPath = Join-Path $repoRoot 'Assets/Scripts/Runtime/StartupMenuFlow.cs'
$prefabPath = Join-Path $repoRoot 'Assets/Resources/Startup/StartupMenu.prefab'
$flow = [IO.File]::ReadAllText($flowPath)
$prefab = [IO.File]::ReadAllText($prefabPath)
$flowPattern = 'public string apiBaseUrl = "[^"]*";'
$prefabPattern = '(?m)^  apiBaseUrl: [^\r\n]*'
if ([regex]::Matches($flow, $flowPattern).Count -ne 1 -or
    [regex]::Matches($prefab, $prefabPattern).Count -ne 1) {
    throw 'Expected exactly one API URL in each file. No files were changed.'
}
$flow = [regex]::Replace($flow, $flowPattern, ('public string apiBaseUrl = "' + $url + '";'))
$prefab = [regex]::Replace($prefab, $prefabPattern, ('  apiBaseUrl: ' + $url))
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($flowPath, $flow, $utf8)
[IO.File]::WriteAllText($prefabPath, $prefab, $utf8)
Write-Host "Updated StartupMenuFlow.cs and StartupMenu.prefab to $url"
Write-Host 'In Unity, wait for import and build with Development Build OFF.'
