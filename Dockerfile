FROM microsoft/aspnetcore-build:1.1 AS build-env
WORKDIR /app

COPY *.sln .
COPY src/Lighthouse/*.csproj ./src/Lighthouse
RUN dotnet restore

COPY . ./
WORKDIR /app/src/Lighthouse
RUN dotnet publish -c Release --framework netcoreapp1.1 -o out

FROM microsoft/aspnetcore:1.1
WORKDIR /app/src/Lighthouse
COPY --from=build-env /app/src/Lighthouse/out .
ENTRYPOINT ["dotnet", "lighthouse.dll"]