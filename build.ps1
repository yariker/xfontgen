$ErrorActionPreference = "Stop"

pushd $PSScriptRoot

try
{
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT)
    {
        $Cygwin = "$env:SystemDrive\cygwin64\bin";

        if (-not (Test-Path $Cygwin))
        {
            throw "Cygwin can not be found or not installed."
        }

        if (($env:Path -split ';') -notcontains $Cygwin)
        {
            $env:Path = $Cygwin + ';' + $env:Path
        }
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
            tar -cvzf "../xfontgen.$Profile.tgz" *
        } elseif ($Profile -like "osx-*") {
            tar -acf "../xfontgen.$Profile.zip" *.app
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
