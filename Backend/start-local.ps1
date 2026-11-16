$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'GameBackend/GameBackend.csproj'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project $project --urls http://127.0.0.1:5055
