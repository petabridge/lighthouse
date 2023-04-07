# Build local Windows nanoserver Docker images using the current project.
# Script is designed to be run inside the root directory of the Akka.Bootstrap.Docker.Sample project.
param (
    [string]$imageName = "lighthouse",
    [Parameter(Mandatory=$true)][string]$tagVersion
)

Write-Host "Building project..."
dotnet publish -c Release --framework net7.0
dotnet build-server shutdown

$windowsImage = "{0}:windows-{1}" -f $imageName,$tagVersion
$windowsImageLatest = "{0}:windows-latest" -f $imageName

Write-Host ("Creating Docker (Windows) image [{0}]..." -f $windowsImage)
docker build . --no-cache -f Dockerfile-windows -t $windowsImage -t $windowsImageLatest