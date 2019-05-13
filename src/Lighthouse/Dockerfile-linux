FROM mcr.microsoft.com/dotnet/core/sdk:2.1 AS base
WORKDIR /app

# should be a comma-delimited list
ENV CLUSTER_SEEDS "[]"
ENV CLUSTER_IP ""
ENV CLUSTER_PORT "4053"

COPY ./bin/Release/netcoreapp2.1/publish/ /app

# 9110 - Petabridge.Cmd
# 4053 - Akka.Cluster
EXPOSE 9110 4053

# Install Petabridge.Cmd client so it can be invoked remotely via
# Docker or K8s 'exec` commands
RUN dotnet tool install --global pbm 

# Needed because https://stackoverflow.com/questions/51977474/install-dotnet-core-tool-dockerfile
ENV PATH="${PATH}:/root/.dotnet/tools"

# RUN pbm help

CMD ["dotnet", "Lighthouse.dll"]