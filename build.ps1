$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot

try
{
    $Version = ./get-version.ps1

    Write-Host -ForegroundColor Yellow "`r`n#### Build ####`r`n"

    dotnet build -c Release -p:Version=$Version
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host -ForegroundColor Yellow "`r`n#### Test ####`r`n"

    dotnet test --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host -ForegroundColor Yellow "`r`n#### Publish ####"

    dir "src/XnaFontTextureGenerator.Desktop/Properties/PublishProfiles/*.pubxml" | %{

        $Profile = $_.Basename
        Write-Host -ForegroundColor Cyan "`r`n## $Profile ##`r`n"

        dotnet publish "src/XnaFontTextureGenerator.Desktop/XnaFontTextureGenerator.Desktop.csproj" `
            -p:PublishProfile=$Profile -p:Version=$Version -c Release -o "build/$Profile" `
            -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    dir "build" -Include "*.tgz","*.zip" -Recurse | move -Destination "build"
}
finally
{
    Pop-Location
}
