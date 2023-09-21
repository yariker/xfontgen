$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot

try
{
    $Version = ./get-version.ps1

    Write-Host -ForegroundColor Yellow "`r`n#### Test ####`r`n"

    dotnet test --verbosity normal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host -ForegroundColor Yellow "`r`n#### Publish ####"

    dir "src/XnaFontTextureGenerator.Desktop/Properties/PublishProfiles/*.pubxml" | %{

        $Profile = $_.Basename
        Write-Host -ForegroundColor Cyan "`r`n## $Profile ##`r`n"

        dotnet publish "src/XnaFontTextureGenerator.Desktop/XnaFontTextureGenerator.Desktop.csproj" `
            -p:PublishProfile=$Profile -p:VersionPrefix="$($Version.Prefix)" -p:VersionSuffix="$($Version.Suffix)" -c Release `
            -o "build/$Profile" -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    dir "build" -Include *.zip, *.tgz -Recurse | move -Destination "build" -Force
}
finally
{
    Pop-Location
}
