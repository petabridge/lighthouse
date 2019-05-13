# Build local Windows nanoserver Docker images using the current project.
# Script is designed to be run inside the root directory of the Akka.Bootstrap.Docker.Sample project.
param (
    [string]$imageName = "akka.docker.boostrap",
    [Parameter(Mandatory=$true)][string]$tagVersion
)

Write-Host "Building project..."
dotnet publish -c Release
dotnet build-server shutdown

$windowsImage = "{0}:{1}-windows" -f $imageName,$tagVersion
$windowsImageLatest = "{0}:latest-windows" -f $imageName

Write-Host ("Creating Docker (Windows) image [{0}]..." -f $windowsImage)
docker build . -f Dockerfile-windows -t $windowsImage -t $windowsImageLatest