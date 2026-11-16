# Legacy path; build context MUST be the repository root:
# docker build -f Backend/GameBackend/Dockerfile .
# Railway uses /Dockerfile.railway instead.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Backend/GameBackend/GameBackend.csproj Backend/GameBackend/
RUN dotnet restore Backend/GameBackend/GameBackend.csproj
COPY Backend/GameBackend/ Backend/GameBackend/
COPY Assets/Content/Data/ Assets/Content/Data/
COPY Assets/Scripts/Runtime/DailyEvents/DailyEventCalendar.cs Assets/Scripts/Runtime/DailyEvents/
COPY Assets/Resources/DailyEvents/Calendar.json Assets/Resources/DailyEvents/
RUN dotnet publish Backend/GameBackend/GameBackend.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build --chown=app:app /out/ ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "GameBackend.dll"]
