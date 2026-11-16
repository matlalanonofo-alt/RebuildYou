# Build and run the ASP.NET Core MVC application in a small production image.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["RebuildYou.Web.csproj", "./"]
RUN dotnet restore "RebuildYou.Web.csproj"
COPY . .
RUN dotnet publish "RebuildYou.Web.csproj" --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RebuildYou.Web.dll"]
