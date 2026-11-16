param(
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [Parameter(Mandatory = $true)][ValidateSet('BeforeRestart', 'AfterRestart')][string]$Phase,
    [string]$CheckpointPath = (Join-Path ([IO.Path]::GetTempPath()) 'ValeyGalaxy-Railway/online-checkpoint.json'),
    [Security.SecureString]$Password
)
$ErrorActionPreference = 'Stop'
$uri = $null
if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref]$uri) -or
    ($uri.Scheme -ne 'https' -and -not ($uri.Scheme -eq 'http' -and $uri.IsLoopback)) -or
    $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') {
    throw 'Use the HTTPS backend origin (HTTP is allowed only for local loopback tests).'
}
$BaseUrl = $uri.GetLeftPart([UriPartial]::Authority)
if ($Phase -eq 'BeforeRestart' -and (Test-Path -LiteralPath $CheckpointPath)) {
    throw 'Checkpoint already exists. Use AfterRestart or choose a different CheckpointPath.'
}
if (-not $Password) { $Password = Read-Host 'Test password (8+ characters, uppercase, lowercase, digit; reuse after restart)' -AsSecureString }
$credential = New-Object System.Net.NetworkCredential('', $Password)
$plainPassword = $credential.Password
$headers = @{}
function Invoke-GameApi([string]$Method, [string]$Path, $Body = $null) {
    $parameters = @{ Method = $Method; Uri = $BaseUrl + $Path; Headers = $headers; TimeoutSec = 60 }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ConvertTo-Json -InputObject $Body -Depth 40 -Compress
    }
    Invoke-RestMethod @parameters
}
function Assert-Equal($Expected, $Actual, [string]$Message) {
    $left = ConvertTo-Json -InputObject $Expected -Depth 40 -Compress
    $right = ConvertTo-Json -InputObject $Actual -Depth 40 -Compress
    if ($left -cne $right) { throw $Message }
}
try {
    $ready = Invoke-GameApi GET '/api/health/ready'
    Assert-Equal 'ready' $ready.status 'Backend is not ready.'
    if ($Phase -eq 'BeforeRestart') {
        $username = 'rw_' + [Guid]::NewGuid().ToString('N').Substring(0, 16)
        $registered = Invoke-GameApi POST '/api/auth/register' @{
            username = $username; email = "$username@example.com"
            password = $plainPassword; confirmPassword = $plainPassword
        }
        if (-not $registered.token) { throw 'Registration returned no session token.' }
    }
    else {
        $checkpoint = Get-Content -LiteralPath $CheckpointPath -Raw | ConvertFrom-Json
        Assert-Equal $checkpoint.baseUrl $BaseUrl 'Use the same backend as BeforeRestart.'
        $username = $checkpoint.username
    }
    $login = Invoke-GameApi POST '/api/auth/login' @{ identity = $username; password = $plainPassword }
    if (-not $login.token) { throw 'Login returned no session token.' }
    $headers.Authorization = 'Bearer ' + $login.token
    if ($Phase -eq 'BeforeRestart') {
        $farmer = @{
            name = 'Railway QA'; farmName = 'Restart QA'; favoriteThing = 'Rain'
            gender = 0; hair = 0; skin = 0; shirt = 0; pants = 0; farmType = 1
            pet = 0; hairColor = 0; eyeColor = 0
        }
        $null = Invoke-GameApi PUT '/api/farmer' $farmer
        $null = Invoke-GameApi POST '/api/game/chests' @{
            requestId = [Guid]::NewGuid().ToString(); name = 'Restart QA'
            location = 'FarmHouse'; tileX = 4; tileY = 7
        }
    }
    $account = Invoke-GameApi GET '/api/auth/me'
    $state = Invoke-GameApi GET '/api/game/state'
    if ($Phase -eq 'BeforeRestart') {
        if (@($state.chests).Count -ne 1) { throw 'Expected a persisted chest.' }
        $checkpoint = @{
            baseUrl = $BaseUrl; username = $username; farmer = $account.farmer
            money = $state.money; inventory = $state.inventory; chests = $state.chests
            beforeRestartUtc = [DateTime]::UtcNow.ToString('o')
        }
        $checkpointDirectory = Split-Path ([IO.Path]::GetFullPath($CheckpointPath)) -Parent
        $null = New-Item -ItemType Directory -Path $checkpointDirectory -Force
        $checkpoint | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $CheckpointPath -Encoding UTF8
        Write-Host "PASS: ready, registration, login, farmer and chest write. Test user: $username"
        Write-Host 'Now restart the backend in Railway; then run AfterRestart with the same password.'
    }
    else {
        Assert-Equal $checkpoint.farmer $account.farmer 'Farmer changed or was lost.'
        Assert-Equal $checkpoint.money $state.money 'Money changed or was lost.'
        Assert-Equal $checkpoint.inventory $state.inventory 'Inventory changed or was lost.'
        Assert-Equal $checkpoint.chests $state.chests 'Chest changed or was lost.'
        $checkpoint | Add-Member -NotePropertyName afterRestartVerifiedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
        $checkpoint | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $CheckpointPath -Encoding UTF8
        Write-Host 'PASS: ready, login and persisted farmer/money/inventory/chests after the operator-triggered restart.'
    }
}
finally {
    if ($headers.Authorization) {
        try { $null = Invoke-GameApi POST '/api/auth/logout' }
        catch { Write-Warning 'Could not log out the test session; it will expire automatically.' }
    }
    $headers.Clear()
    $plainPassword = $null
    $credential = $null
}
