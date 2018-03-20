FROM microsoft/dotnet:1.1-sdk AS build-env
WORKDIR /app

ENV ACTORSYSTEM "lighthouse"

# should be a comma-delimited list
ENV SEEDS "[]"

COPY src/Lighthouse/*.csproj ./
RUN dotnet restore

COPY src/Lighthouse ./
RUN dotnet publish -c Release --framework netcoreapp1.1 -o out

FROM microsoft/dotnet:1.1-runtime AS runtime
WORKDIR /app
COPY --from=build-env /app/out ./
COPY --from=build-env /app/get-dockerip.sh ./get-dockerip.sh
ENTRYPOINT ["/bin/bash","get-dockerip.sh"]

# 9110 - Petabridge.Cmd
# 4053 - Akka.Cluster
EXPOSE 9110 4053

CMD ["dotnet", "Lighthouse.dll"]