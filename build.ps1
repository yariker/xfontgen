$ErrorActionPreference = "Stop"

pushd $PSScriptRoot

try
{
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $Tar = "$env:SystemDrive\cygwin64\bin\tar.exe"
        if (-not (Test-Path $Tar)) {
            Write-Error "Cygwin can not be found or not installed."
            exit -1
        }
    } else {
        $Tar = "tar"
    }

    $Version = ./get-version.ps1

    Write-Host -ForegroundColor Yellow "`r`n#### Test ####`r`n"
    dotnet test --verbosity normal

    Write-Host -ForegroundColor Yellow "`r`n#### Publish ####"

    dir "src/XnaFontTextureGenerator.Desktop/Properties/PublishProfiles/*.pubxml" | %{

        $Profile = $_.Basename
        Write-Host -ForegroundColor Cyan "`r`n## $Profile ##`r`n"

        dotnet publish "src/XnaFontTextureGenerator.Desktop/XnaFontTextureGenerator.Desktop.csproj" `
            -p:PublishProfile=$Profile -p:VersionPrefix="$($Version.Prefix)" -p:VersionSuffix="$($Version.Suffix)" -c Release `
            -o "build/$Profile" -p:DebugType=None -p:DebugSymbols=false

        pushd "build/$Profile"

        if ($Profile -like "linux-*") {
            & $Tar -cvzf "../xfontgen.$Profile.tgz" *
        } elseif ($Profile -like "osx-*") {
            & $Tar -acf "../xfontgen.$Profile.zip" *.app
        } elseif ($Profile -like "win-*") {
            Compress-Archive -Path "*" -DestinationPath "../xfontgen.$Profile.zip" -Force
        }

        popd
    }
}
finally
{
    popd
}
