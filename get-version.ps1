$ErrorActionPreference = "Continue"

Push-Location $PSScriptRoot

try
{
    $Tag = git describe --tags --abbrev=0 --match v[0-9]* HEAD 2>&1
    if ($LASTEXITCODE -ne 0) {
        $Tag = "v0.0.0"
    }

    $Hash = git rev-parse --short HEAD
    $Version = [Version]::Parse($Tag.TrimStart('v'))
    "$Version+$Hash"
}
finally
{
    Pop-Location
}
