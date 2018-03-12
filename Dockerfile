FROM microsoft/dotnet:1.1-sdk AS build-env
WORKDIR /app

COPY src/Lighthouse/*.csproj ./
RUN dotnet restore

COPY src/Lighthouse ./
RUN dotnet publish -c Release --framework netcoreapp1.1 -o out

FROM microsoft/dotnet:1.1-runtime AS runtime
WORKDIR /app
COPY --from=build-env /app/out ./
RUN ls
ENTRYPOINT ["dotnet", "Lighthouse.dll"]