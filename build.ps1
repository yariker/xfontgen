$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot

try
{
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {

        $Cygwin = "$env:SystemDrive\cygwin64\bin";

        if (-not (Test-Path $Cygwin)) {
            Write-Error "Cygwin can not be found or not installed."
            exit -1
        }

        if (($env:Path -split ';') -notcontains $Cygwin) {
            $env:Path = $Cygwin + ";" + $env:Path
        }
    }

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
