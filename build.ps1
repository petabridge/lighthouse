<#
.SYNOPSIS
This is a Powershell script to bootstrap a Fake build.
.DESCRIPTION
This Powershell script will download NuGet if missing, restore NuGet tools (including Fake)
and execute your Fake build script with the parameters you provide.
.PARAMETER Target
The build script target to run.
.PARAMETER Configuration
The build configuration to use.
.PARAMETER Verbosity
Specifies the amount of information to be displayed.
.PARAMETER WhatIf
Performs a dry run of the build script.
No tasks will be executed.
.PARAMETER ScriptArgs
Remaining arguments are added here.
#>

[CmdletBinding()]
Param(
    [string]$Target = "Default",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
    [switch]$WhatIf,
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$ScriptArgs
)

$FakeVersion = "4.61.2"
$DotNetChannel = "LTS";
$DotNetVersion = "3.1.100";
$DotNetInstallerUri = "https://dot.net/v1/dotnet-install.ps1";
$NugetVersion = "4.1.0";
$NugetUrl = "https://dist.nuget.org/win-x86-commandline/v$NugetVersion/nuget.exe"
$ProtobufVersion = "3.9.2"
$DocfxVersion = "2.48.1"

# Make sure tools folder exists
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
$ToolPath = Join-Path $PSScriptRoot "tools"
if (!(Test-Path $ToolPath)) {
    Write-Verbose "Creating tools directory..."
    New-Item -Path $ToolPath -Type directory | out-null
}

###########################################################################
# INSTALL .NET CORE CLI
###########################################################################

Function Remove-PathVariable([string]$VariableToRemove)
{
    $path = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($path -ne $null)
    {
        $newItems = $path.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { "$($_)" -inotlike $VariableToRemove }
        [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "User")
    }

    $path = [Environment]::GetEnvironmentVariable("PATH", "Process")
    if ($path -ne $null)
    {
        $newItems = $path.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { "$($_)" -inotlike $VariableToRemove }
        [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "Process")
    }
}

# Get .NET Core CLI path if installed.
$FoundDotNetCliVersion = $null;
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $FoundDotNetCliVersion = dotnet --version;
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    $env:DOTNET_CLI_TELEMETRY_OPTOUT=1
}

if($FoundDotNetCliVersion -ne $DotNetVersion) {
    $InstallPath = Join-Path $PSScriptRoot ".dotnet"
    if (!(Test-Path $InstallPath)) {
        mkdir -Force $InstallPath | Out-Null;
    }
    (New-Object System.Net.WebClient).DownloadFile($DotNetInstallerUri, "$InstallPath\dotnet-install.ps1");
    & $InstallPath\dotnet-install.ps1 -Channel $DotNetChannel -Version $DotNetVersion -InstallDir $InstallPath -Architecture x64;

    Remove-PathVariable "$InstallPath"
    $env:PATH = "$InstallPath;$env:PATH"
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    $env:DOTNET_CLI_TELEMETRY_OPTOUT=1
    $env:DOTNET_ROOT=$InstallPath
}

###########################################################################
# INSTALL NUGET
###########################################################################

# Make sure nuget.exe exists.
$NugetPath = Join-Path $ToolPath "nuget.exe"
if (!(Test-Path $NugetPath)) {
    Write-Host "Downloading NuGet.exe..."
    (New-Object System.Net.WebClient).DownloadFile($NugetUrl, $NugetPath);
}

###########################################################################
# INSTALL FAKE
###########################################################################
# Make sure Fake has been installed.

$FakeExePath = Join-Path $ToolPath "FAKE/tools/FAKE.exe"
if (!(Test-Path $FakeExePath)) {
    Write-Host "Installing Fake..."
    Invoke-Expression "&`"$NugetPath`" install Fake -ExcludeVersion -Version $FakeVersion -OutputDirectory `"$ToolPath`"" | Out-Null;
    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring Fake from NuGet."
    }
}

###########################################################################
# Docfx
###########################################################################

# Make sure Docfx has been installed.
$DocfxExePath = Join-Path $ToolPath "docfx.console/tools/docfx.exe"
if (!(Test-Path $DocfxExePath)) {
    Write-Host "Installing Docfx..."
    Invoke-Expression "&`"$NugetPath`" install docfx.console -ExcludeVersion -Version $DocfxVersion -OutputDirectory `"$ToolPath`"" | Out-Null;
    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring docfx.console from NuGet."
    }
}


###########################################################################
# Google.Protobuf.Tools
###########################################################################

# Make sure Google.Protobuf.Tools has been installed.
$ProtobufExePath = Join-Path $ToolPath "Google.Protobuf.Tools/tools/windows_x64/protoc.exe"
if (!(Test-Path $ProtobufExePath)) {
    Write-Host "Installing Google.Protobuf.Tools..."
    Invoke-Expression "&`"$NugetPath`" install Google.Protobuf.Tools -ExcludeVersion -Version $ProtobufVersion -OutputDirectory `"$ToolPath`"" | Out-Null;
    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring Google.Protobuf.Tools from NuGet."
    }
}


###########################################################################
# SignTool
###########################################################################

# Make sure the SignClient has been installed
if (Get-Command signclient -ErrorAction SilentlyContinue) {
    Write-Host "Found SignClient. Skipping install."
}
else{
    $SignClientFolder = Join-Path $ToolPath "signclient"
    Write-Host "SignClient not found. Installing to ... $SignClientFolder"
    dotnet tool install SignClient --version 1.0.82 --tool-path "$SignClientFolder"
}

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

# Build the argument list.
$Arguments = @{
    target=$Target;
    configuration=$Configuration;
    verbosity=$Verbosity;
    dryrun=$WhatIf;
}.GetEnumerator() | %{"--{0}=`"{1}`"" -f $_.key, $_.value };

# Start Fake
Write-Host "Running build script..."
Invoke-Expression "$FakeExePath `"build.fsx`" $ScriptArgs $Arguments"

exit $LASTEXITCODE
# SIG # Begin signature block
# MIIgRwYJKoZIhvcNAQcCoIIgODCCIDQCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCD1qBLc/OIDk1nK
# rKEr6H5asBg2dB0oEJHmWl5cm99d6aCCDhkwggO3MIICn6ADAgECAhAM5+DlF9hG
# /o/lYPwb8DA5MA0GCSqGSIb3DQEBBQUAMGUxCzAJBgNVBAYTAlVTMRUwEwYDVQQK
# EwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5jb20xJDAiBgNV
# BAMTG0RpZ2lDZXJ0IEFzc3VyZWQgSUQgUm9vdCBDQTAeFw0wNjExMTAwMDAwMDBa
# Fw0zMTExMTAwMDAwMDBaMGUxCzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2Vy
# dCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5jb20xJDAiBgNVBAMTG0RpZ2lD
# ZXJ0IEFzc3VyZWQgSUQgUm9vdCBDQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCC
# AQoCggEBAK0OFc7kQ4BcsYfzt2D5cRKlrtwmlIiq9M71IDkoWGAM+IDaqRWVMmE8
# tbEohIqK3J8KDIMXeo+QrIrneVNcMYQq9g+YMjZ2zN7dPKii72r7IfJSYd+fINcf
# 4rHZ/hhk0hJbX/lYGDW8R82hNvlrf9SwOD7BG8OMM9nYLxj+KA+zp4PWw25EwGE1
# lhb+WZyLdm3X8aJLDSv/C3LanmDQjpA1xnhVhyChz+VtCshJfDGYM2wi6YfQMlqi
# uhOCEe05F52ZOnKh5vqk2dUXMXWuhX0irj8BRob2KHnIsdrkVxfEfhwOsLSSplaz
# vbKX7aqn8LfFqD+VFtD/oZbrCF8Yd08CAwEAAaNjMGEwDgYDVR0PAQH/BAQDAgGG
# MA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFEXroq/0ksuCMS1Ri6enIZ3zbcgP
# MB8GA1UdIwQYMBaAFEXroq/0ksuCMS1Ri6enIZ3zbcgPMA0GCSqGSIb3DQEBBQUA
# A4IBAQCiDrzf4u3w43JzemSUv/dyZtgy5EJ1Yq6H6/LV2d5Ws5/MzhQouQ2XYFwS
# TFjk0z2DSUVYlzVpGqhH6lbGeasS2GeBhN9/CTyU5rgmLCC9PbMoifdf/yLil4Qf
# 6WXvh+DfwWdJs13rsgkq6ybteL59PyvztyY1bV+JAbZJW58BBZurPSXBzLZ/wvFv
# hsb6ZGjrgS2U60K3+owe3WLxvlBnt2y98/Efaww2BxZ/N3ypW2168RJGYIPXJwS+
# S86XvsNnKmgR34DnDDNmvxMNFG7zfx9jEB76jRslbWyPpbdhAbHSoyahEHGdreLD
# +cOZUbcrBwjOLuZQsqf6CkUvovDyMIIFJjCCBA6gAwIBAgIQBcC4m7IhMrtdg5wV
# IYUHmzANBgkqhkiG9w0BAQsFADByMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGln
# aUNlcnQgSW5jMRkwFwYDVQQLExB3d3cuZGlnaWNlcnQuY29tMTEwLwYDVQQDEyhE
# aWdpQ2VydCBTSEEyIEFzc3VyZWQgSUQgQ29kZSBTaWduaW5nIENBMB4XDTIwMDEy
# OTAwMDAwMFoXDTIzMDEzMTEyMDAwMFowYzELMAkGA1UEBhMCVVMxDjAMBgNVBAgT
# BVRleGFzMRAwDgYDVQQHEwdIb3VzdG9uMRgwFgYDVQQKEw9QZXRhYnJpZGdlLCBM
# TEMxGDAWBgNVBAMTD1BldGFicmlkZ2UsIExMQzCCASIwDQYJKoZIhvcNAQEBBQAD
# ggEPADCCAQoCggEBAL0BBOw8B5zHcjHQ/qcF1OrQv7Z1U5NiuEtqikJnkK9OhMzx
# k0Hn9TOReZkHHe1oV2Ouf43E9RpvfufijJIUKpFMezN6tXTyTyMFbgr+sASxaGf3
# RxjyH2caP1ozTtLyycOFR7DKI3tbNhTTAtQkkTvgQ6buDjEhFTBhn5kI8AoXelXb
# n92uFRn7FOGOPno/Lru85Sg5QLz9iqHx4Vje7JeEzQbnB5K6mntZuheSIdnpDCIq
# xPfLtackHqvAKxoYXg8hCP+LTsgRaxNbu8Wi6pDfYjqEeLhBqHZda5T/TF887rHQ
# tgE9XyBOUokrWW2b2+29vCVkTaFTmMauJP5GU7kCAwEAAaOCAcUwggHBMB8GA1Ud
# IwQYMBaAFFrEuXsqCqOl6nEDwGD5LfZldQ5YMB0GA1UdDgQWBBQP0wazew0SBdcA
# 0LguXFA9pT8ZrTAOBgNVHQ8BAf8EBAMCB4AwEwYDVR0lBAwwCgYIKwYBBQUHAwMw
# dwYDVR0fBHAwbjA1oDOgMYYvaHR0cDovL2NybDMuZGlnaWNlcnQuY29tL3NoYTIt
# YXNzdXJlZC1jcy1nMS5jcmwwNaAzoDGGL2h0dHA6Ly9jcmw0LmRpZ2ljZXJ0LmNv
# bS9zaGEyLWFzc3VyZWQtY3MtZzEuY3JsMEwGA1UdIARFMEMwNwYJYIZIAYb9bAMB
# MCowKAYIKwYBBQUHAgEWHGh0dHBzOi8vd3d3LmRpZ2ljZXJ0LmNvbS9DUFMwCAYG
# Z4EMAQQBMIGEBggrBgEFBQcBAQR4MHYwJAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3Nw
# LmRpZ2ljZXJ0LmNvbTBOBggrBgEFBQcwAoZCaHR0cDovL2NhY2VydHMuZGlnaWNl
# cnQuY29tL0RpZ2lDZXJ0U0hBMkFzc3VyZWRJRENvZGVTaWduaW5nQ0EuY3J0MAwG
# A1UdEwEB/wQCMAAwDQYJKoZIhvcNAQELBQADggEBADgE3rY8X9MYn4tEPaGxRkzH
# IzTGb236WdbwqDXIiZfjIC4h3/FMOhvSeC63blYti9ExgEg2en3UmmNbLxUgbQU8
# HK9giaMwqPCWPH+vKkdXtzTFQMkNQPkboOlVU4+2WtLHkrIQwJ3hOEveKAmNuyST
# yWCQD6SNQu0o6GAEKyg4HBVapOf0ioAS0T6ijnT/Sgh98DJSEqzslZ3/RWps51nw
# W3UedbvzCd/xpQx/5IT/hIvK8pJ2IsSW2OKAh450ZWR2LKY9oODZmfFpUvDiZBYs
# RQWRUUvcNDb5mAln3snmVbe1gIcekV7iG7iwzx54K3MFL1OjGKVR5JZcfpNCz4Uw
# ggUwMIIEGKADAgECAhAECRgbX9W7ZnVTQ7VvlVAIMA0GCSqGSIb3DQEBCwUAMGUx
# CzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3
# dy5kaWdpY2VydC5jb20xJDAiBgNVBAMTG0RpZ2lDZXJ0IEFzc3VyZWQgSUQgUm9v
# dCBDQTAeFw0xMzEwMjIxMjAwMDBaFw0yODEwMjIxMjAwMDBaMHIxCzAJBgNVBAYT
# AlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2Vy
# dC5jb20xMTAvBgNVBAMTKERpZ2lDZXJ0IFNIQTIgQXNzdXJlZCBJRCBDb2RlIFNp
# Z25pbmcgQ0EwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQD407Mcfw4R
# r2d3B9MLMUkZz9D7RZmxOttE9X/lqJ3bMtdx6nadBS63j/qSQ8Cl+YnUNxnXtqrw
# nIal2CWsDnkoOn7p0WfTxvspJ8fTeyOU5JEjlpB3gvmhhCNmElQzUHSxKCa7JGnC
# wlLyFGeKiUXULaGj6YgsIJWuHEqHCN8M9eJNYBi+qsSyrnAxZjNxPqxwoqvOf+l8
# y5Kh5TsxHM/q8grkV7tKtel05iv+bMt+dDk2DZDv5LVOpKnqagqrhPOsZ061xPeM
# 0SAlI+sIZD5SlsHyDxL0xY4PwaLoLFH3c7y9hbFig3NBggfkOItqcyDQD2RzPJ6f
# pjOp/RnfJZPRAgMBAAGjggHNMIIByTASBgNVHRMBAf8ECDAGAQH/AgEAMA4GA1Ud
# DwEB/wQEAwIBhjATBgNVHSUEDDAKBggrBgEFBQcDAzB5BggrBgEFBQcBAQRtMGsw
# JAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3NwLmRpZ2ljZXJ0LmNvbTBDBggrBgEFBQcw
# AoY3aHR0cDovL2NhY2VydHMuZGlnaWNlcnQuY29tL0RpZ2lDZXJ0QXNzdXJlZElE
# Um9vdENBLmNydDCBgQYDVR0fBHoweDA6oDigNoY0aHR0cDovL2NybDQuZGlnaWNl
# cnQuY29tL0RpZ2lDZXJ0QXNzdXJlZElEUm9vdENBLmNybDA6oDigNoY0aHR0cDov
# L2NybDMuZGlnaWNlcnQuY29tL0RpZ2lDZXJ0QXNzdXJlZElEUm9vdENBLmNybDBP
# BgNVHSAESDBGMDgGCmCGSAGG/WwAAgQwKjAoBggrBgEFBQcCARYcaHR0cHM6Ly93
# d3cuZGlnaWNlcnQuY29tL0NQUzAKBghghkgBhv1sAzAdBgNVHQ4EFgQUWsS5eyoK
# o6XqcQPAYPkt9mV1DlgwHwYDVR0jBBgwFoAUReuir/SSy4IxLVGLp6chnfNtyA8w
# DQYJKoZIhvcNAQELBQADggEBAD7sDVoks/Mi0RXILHwlKXaoHV0cLToaxO8wYdd+
# C2D9wz0PxK+L/e8q3yBVN7Dh9tGSdQ9RtG6ljlriXiSBThCk7j9xjmMOE0ut119E
# efM2FAaK95xGTlz/kLEbBw6RFfu6r7VRwo0kriTGxycqoSkoGjpxKAI8LpGjwCUR
# 4pwUR6F6aGivm6dcIFzZcbEMj7uo+MUSaJ/PQMtARKUT8OZkDCUIQjKyNookAv4v
# cn4c10lFluhZHen6dGRrsutmQ9qzsIzV6Q3d9gEgzpkxYz0IGhizgZtPxpMQBvwH
# gfqL2vmCSfdibqFT+hKUGIUukpHqaGxEMrJmoecYpJpkUe8xghGEMIIRgAIBATCB
# hjByMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQL
# ExB3d3cuZGlnaWNlcnQuY29tMTEwLwYDVQQDEyhEaWdpQ2VydCBTSEEyIEFzc3Vy
# ZWQgSUQgQ29kZSBTaWduaW5nIENBAhAFwLibsiEyu12DnBUhhQebMA0GCWCGSAFl
# AwQCAQUAoIIBATAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQgUnDR03vOPEHEDe/p
# KB2T1FkZV5jjgmvXLHlxx+mkZ2EwgZQGCisGAQQBgjcCAQwxgYUwgYKgSIBGAFAA
# ZQB0AGEAYgByAGkAZABnAGUAIABTAHQAYQBuAGQAYQByAGQAIABCAHUAaQBsAGQA
# IABUAGUAbQBwAGwAYQB0AGUAc6E2gDRodHRwczovL2dpdGh1Yi5jb20vcGV0YWJy
# aWRnZS9wZXRhYnJpZGdlLWRvdG5ldC1uZXcgMA0GCSqGSIb3DQEBAQUABIIBAFv1
# vD9ZSTJrIH+H7NzH+Y/MHpEof58gecxaZICFzNZeQM9XqFvILDl40nc/ZqCuVxJJ
# d1vW6EF4kmiLIL+HzjdBzwSw6Ekk21E1LIIMw29XYiL+FXGSOSA1B4IOY2feZB7U
# 6KoKRMMo8h5ArGaXLXDm9kmcNg4U6W+wYjnc8RdiQdu1ppivSIarfvCUDfKCq5Lw
# 82LHAWVaHgZ1J5t3XchkTrMgnpBz6hgZ4HBUv9dSXFXxqcKO2xDR5/kuTGGc/HD8
# IQkZVbwSBXkMcuLncU9eMG1hQTIyWmBm/b7ly9oQpKmzmzGNR41Kl1Pc8q8XmrV5
# ruWeG6/eRw7jXdlqB/Shgg7JMIIOxQYKKwYBBAGCNwMDATGCDrUwgg6xBgkqhkiG
# 9w0BBwKggg6iMIIOngIBAzEPMA0GCWCGSAFlAwQCAQUAMHgGCyqGSIb3DQEJEAEE
# oGkEZzBlAgEBBglghkgBhv1sBwEwMTANBglghkgBZQMEAgEFAAQgQdNEo8/2uNAj
# ZQUOlniffokwxrsHfI1hdpmyiHoquaoCEQCbrtN9LCbuS9pPAqmgVxZdGA8yMDIw
# MDEzMDE0MjU1OVqgggu7MIIGgjCCBWqgAwIBAgIQBM0/hWiudsYbsP5xYMynbTAN
# BgkqhkiG9w0BAQsFADByMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQg
# SW5jMRkwFwYDVQQLExB3d3cuZGlnaWNlcnQuY29tMTEwLwYDVQQDEyhEaWdpQ2Vy
# dCBTSEEyIEFzc3VyZWQgSUQgVGltZXN0YW1waW5nIENBMB4XDTE5MTAwMTAwMDAw
# MFoXDTMwMTAxNzAwMDAwMFowTDELMAkGA1UEBhMCVVMxFzAVBgNVBAoTDkRpZ2lD
# ZXJ0LCBJbmMuMSQwIgYDVQQDExtUSU1FU1RBTVAtU0hBMjU2LTIwMTktMTAtMTUw
# ggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDpZDWc+qmYZWQb5BfcuCk2
# zGcJWIVNMODJ/+U7PBEoUK8HMeJdCRjC9omMaQgEI+B3LZ0V5bjooWqO/9Su0noW
# 7/hBtR05dcHPL6esRX6UbawDAZk8Yj5+ev1FlzG0+rfZQj6nVZvfWk9YAqgyaSIT
# vouCLcaYq2ubtMnyZREMdA2y8AiWdMToskiioRSl+PrhiXBEO43v+6T0w7m9FCzr
# DCgnJYCrEEsWEmALaSKMTs3G1bJlWSHgfCwSjXAOj4rK4NPXszl3UNBCLC56zpxn
# ejh3VED/T5UEINTryM6HFAj+HYDd0OcreOq/H3DG7kIWUzZFm1MZSWKdegKblRSj
# AgMBAAGjggM4MIIDNDAOBgNVHQ8BAf8EBAMCB4AwDAYDVR0TAQH/BAIwADAWBgNV
# HSUBAf8EDDAKBggrBgEFBQcDCDCCAb8GA1UdIASCAbYwggGyMIIBoQYJYIZIAYb9
# bAcBMIIBkjAoBggrBgEFBQcCARYcaHR0cHM6Ly93d3cuZGlnaWNlcnQuY29tL0NQ
# UzCCAWQGCCsGAQUFBwICMIIBVh6CAVIAQQBuAHkAIAB1AHMAZQAgAG8AZgAgAHQA
# aABpAHMAIABDAGUAcgB0AGkAZgBpAGMAYQB0AGUAIABjAG8AbgBzAHQAaQB0AHUA
# dABlAHMAIABhAGMAYwBlAHAAdABhAG4AYwBlACAAbwBmACAAdABoAGUAIABEAGkA
# ZwBpAEMAZQByAHQAIABDAFAALwBDAFAAUwAgAGEAbgBkACAAdABoAGUAIABSAGUA
# bAB5AGkAbgBnACAAUABhAHIAdAB5ACAAQQBnAHIAZQBlAG0AZQBuAHQAIAB3AGgA
# aQBjAGgAIABsAGkAbQBpAHQAIABsAGkAYQBiAGkAbABpAHQAeQAgAGEAbgBkACAA
# YQByAGUAIABpAG4AYwBvAHIAcABvAHIAYQB0AGUAZAAgAGgAZQByAGUAaQBuACAA
# YgB5ACAAcgBlAGYAZQByAGUAbgBjAGUALjALBglghkgBhv1sAxUwHwYDVR0jBBgw
# FoAU9LbhIB3+Ka7S5GGlsqIlssgXNW4wHQYDVR0OBBYEFFZTD8HGB6dN19huV3KA
# UEzk7J7BMHEGA1UdHwRqMGgwMqAwoC6GLGh0dHA6Ly9jcmwzLmRpZ2ljZXJ0LmNv
# bS9zaGEyLWFzc3VyZWQtdHMuY3JsMDKgMKAuhixodHRwOi8vY3JsNC5kaWdpY2Vy
# dC5jb20vc2hhMi1hc3N1cmVkLXRzLmNybDCBhQYIKwYBBQUHAQEEeTB3MCQGCCsG
# AQUFBzABhhhodHRwOi8vb2NzcC5kaWdpY2VydC5jb20wTwYIKwYBBQUHMAKGQ2h0
# dHA6Ly9jYWNlcnRzLmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydFNIQTJBc3N1cmVkSURU
# aW1lc3RhbXBpbmdDQS5jcnQwDQYJKoZIhvcNAQELBQADggEBAC6DoUQFSgTjuTJS
# +tmB8Bq7+AmNI7k92JKh5kYcSi9uejxjbjcXoxq/WCOyQ5yUg045CbAs6Mfh4szt
# y3lrzt4jAUftlVSB4IB7ErGvAoapOnNq/vifwY3RIYzkKYLDigtgAAKdH0fEn7QK
# aFN/WhCm+CLm+FOSMV/YgoMtbRNCroPBEE6kJPRHnN4PInJ3XH9P6TmYK1eSRNfv
# bpPZQ8cEM2NRN1aeRwQRw6NYVCHY4o5W10k/V/wKnyNee/SUjd2dGrvfeiqm0kWm
# VQyP9kyK8pbPiUbcMbKRkKNfMzBgVfX8azCsoe3kR04znmdqKLVNwu1bl4L4y6kI
# bFMJtPcwggUxMIIEGaADAgECAhAKoSXW1jIbfkHkBdo2l8IVMA0GCSqGSIb3DQEB
# CwUAMGUxCzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNV
# BAsTEHd3dy5kaWdpY2VydC5jb20xJDAiBgNVBAMTG0RpZ2lDZXJ0IEFzc3VyZWQg
# SUQgUm9vdCBDQTAeFw0xNjAxMDcxMjAwMDBaFw0zMTAxMDcxMjAwMDBaMHIxCzAJ
# BgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5k
# aWdpY2VydC5jb20xMTAvBgNVBAMTKERpZ2lDZXJ0IFNIQTIgQXNzdXJlZCBJRCBU
# aW1lc3RhbXBpbmcgQ0EwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQC9
# 0DLuS82Pf92puoKZxTlUKFe2I0rEDgdFM1EQfdD5fU1ofue2oPSNs4jkl79jIZCY
# vxO8V9PD4X4I1moUADj3Lh477sym9jJZ/l9lP+Cb6+NGRwYaVX4LJ37AovWg4N4i
# Pw7/fpX786O6Ij4YrBHk8JkDbTuFfAnT7l3ImgtU46gJcWvgzyIQD3XPcXJOCq3f
# QDpct1HhoXkUxk0kIzBdvOw8YGqsLwfM/fDqR9mIUF79Zm5WYScpiYRR5oLnRlD9
# lCosp+R1PrqYD4R/nzEU1q3V8mTLex4F0IQZchfxFwbvPc3WTe8GQv2iUypPhR3E
# HTyvz9qsEPXdrKzpVv+TAgMBAAGjggHOMIIByjAdBgNVHQ4EFgQU9LbhIB3+Ka7S
# 5GGlsqIlssgXNW4wHwYDVR0jBBgwFoAUReuir/SSy4IxLVGLp6chnfNtyA8wEgYD
# VR0TAQH/BAgwBgEB/wIBADAOBgNVHQ8BAf8EBAMCAYYwEwYDVR0lBAwwCgYIKwYB
# BQUHAwgweQYIKwYBBQUHAQEEbTBrMCQGCCsGAQUFBzABhhhodHRwOi8vb2NzcC5k
# aWdpY2VydC5jb20wQwYIKwYBBQUHMAKGN2h0dHA6Ly9jYWNlcnRzLmRpZ2ljZXJ0
# LmNvbS9EaWdpQ2VydEFzc3VyZWRJRFJvb3RDQS5jcnQwgYEGA1UdHwR6MHgwOqA4
# oDaGNGh0dHA6Ly9jcmw0LmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydEFzc3VyZWRJRFJv
# b3RDQS5jcmwwOqA4oDaGNGh0dHA6Ly9jcmwzLmRpZ2ljZXJ0LmNvbS9EaWdpQ2Vy
# dEFzc3VyZWRJRFJvb3RDQS5jcmwwUAYDVR0gBEkwRzA4BgpghkgBhv1sAAIEMCow
# KAYIKwYBBQUHAgEWHGh0dHBzOi8vd3d3LmRpZ2ljZXJ0LmNvbS9DUFMwCwYJYIZI
# AYb9bAcBMA0GCSqGSIb3DQEBCwUAA4IBAQBxlRLpUYdWac3v3dp8qmN6s3jPBjdA
# hO9LhL/KzwMC/cWnww4gQiyvd/MrHwwhWiq3BTQdaq6Z+CeiZr8JqmDfdqQ6kw/4
# stHYfBli6F6CJR7Euhx7LCHi1lssFDVDBGiy23UC4HLHmNY8ZOUfSBAYX4k4YU1i
# RiSHY4yRUiyvKYnleB/WCxSlgNcSR3CzddWThZN+tpJn+1Nhiaj1a5bA9FhpDXzI
# AbG5KHW3mWOFIoxhynmUfln8jA/jb7UBJrZspe6HUSHkWGCbugwtK22ixH67xCUr
# RwIIfEmuE7bhfEJCKMYYVs9BNLZmXbZ0e/VWMyIvIjayS6JKldj1po5SMYICTTCC
# AkkCAQEwgYYwcjELMAkGA1UEBhMCVVMxFTATBgNVBAoTDERpZ2lDZXJ0IEluYzEZ
# MBcGA1UECxMQd3d3LmRpZ2ljZXJ0LmNvbTExMC8GA1UEAxMoRGlnaUNlcnQgU0hB
# MiBBc3N1cmVkIElEIFRpbWVzdGFtcGluZyBDQQIQBM0/hWiudsYbsP5xYMynbTAN
# BglghkgBZQMEAgEFAKCBmDAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQwHAYJ
# KoZIhvcNAQkFMQ8XDTIwMDEzMDE0MjU1OVowKwYLKoZIhvcNAQkQAgwxHDAaMBgw
# FgQUAyW9UF7aljAtwi9PoB5MKL4oNMUwLwYJKoZIhvcNAQkEMSIEICGbrMO0FG3M
# c7u+iB7Abp261pqGEnz9oQ08TQ3bxv/yMA0GCSqGSIb3DQEBAQUABIIBAM/d9hAb
# 0ODVDH4SXNrh6IWwiyJ9BpTP9uIbwvHNpPfR7L4O/b6/1dgsrk1T+LhVHKWFzl0l
# na9/HTpaXnVKM9f7El6qvpdnieAvhIrI66l4AywYYa/Pq0tLeiDt4XG8xYuquZnp
# 80K0jvnlre45escl9GBsi1Vys69eUk3hde/nq+wqMUtUd7M4VUkDeP/PbLsZZL8M
# wBfy4g8tL2z/Fi/GGxv/sLCdVJp12kLRODBtgkukzTPlD/gfe4+Q5b1XciUd7DYt
# MeMYbnCesTeuIcDY+HL5WpexLM31sjJdhlEbYhv4DRSDnhLGwfV6DrgT7Hr3H+W0
# 8iDthQYtlekiifk=
# SIG # End signature block
