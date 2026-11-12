# build. This image has the full .NET SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# this copies the project files first and restore packages. Docker caches this layer,
# so later rebuilds skip the download unless the packages change
COPY TfiBus.Core/TfiBus.Core.csproj TfiBus.Core/
COPY TfiBus.Collector/TfiBus.Collector.csproj TfiBus.Collector/
RUN dotnet restore TfiBus.Collector/TfiBus.Collector.csproj

# copy the source (the .dockerignore keeps junk out) and publish a release build
COPY TfiBus.Core/ TfiBus.Core/
COPY TfiBus.Collector/ TfiBus.Collector/
RUN dotnet publish TfiBus.Collector/TfiBus.Collector.csproj -c Release -o /app --no-restore

# run. Only the .NET runtime, without the SDK, so the image is small.
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .

# Inside the container the database lives in /data, which is mapped to a folder on the server.
ENV Collector__DatabasePath=/data/collector.db
ENTRYPOINT ["dotnet", "TfiBus.Collector.dll"]
