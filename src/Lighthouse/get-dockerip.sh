#!/bin/sh
host=$(hostname -i)
echo "Docker container bound on $host"
export CONTAINER_IP="$host"

exec "$@"