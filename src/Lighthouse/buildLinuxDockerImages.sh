#!/usr/bin/env bash
##########################################################################
# Build local Alpine Linux Docker images using the current project.
# Script is designed to be run inside the root directory of the Akka.Bootstrap.Docker.Sample project.
##########################################################################

IMAGE_VERSION=$1
IMAGE_NAME=$2

if [ -z $IMAGE_VERSION ]; then
	echo `date`" - Missing mandatory argument: Docker image version."
	echo `date`" - Usage: ./buildDockerImagesLinux.sh [imageVersion] [imageName]"
	exit 1
fi

if [ -z $IMAGE_NAME ]; then
	IMAGE_NAME="akka.docker.boostrap"
	echo `date`" - Using default Docker image name [$IMAGE_NAME]"
fi


echo "Building project..."
dotnet publish -c Release
dotnet build-server shutdown

LINUX_IMAGE="$IMAGE_NAME:$IMAGE_VERSION-linux"
LINUX_IMAGE_LATEST="$IMAGE_NAME:latest-linux"

echo "Creating Docker (Linux) image [$LINUX_IMAGE]..."
docker build . -f Dockerfile-linux -t $LINUX_IMAGE  -t $LINUX_IMAGE_LATEST