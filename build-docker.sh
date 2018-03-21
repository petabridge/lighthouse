#!/bin/sh

if [ -z ${1} ]; then
  # no version number passed in, get last release tag from github
  release_tag=`git describe --tags $(git rev-list --tags --max-count=1)`
else
  # can pass a target release tag e.g. bash docker-build.sh v1.0.2
  # must include the "v" at the front of the version number e.g. "v1.0.2" NOT "1.0.2"
  release_tag=$1
fi

docker build -t petabridge/lighthouse:netcore1.1 -t petabridge/lighthouse:${release_tag} -t petabridge/lighthouse:latest .

if [ -z "$DOCKER_PUSH"]; then
	echo "not pushing"
else
	docker push petabridge/lighthouse
fi