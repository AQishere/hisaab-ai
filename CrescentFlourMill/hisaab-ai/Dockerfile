# The ERP API for a Hugging Face Space (scripts/deploy-space.sh). Build context: this folder.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY hisaab-ai.csproj nuget.config ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Dates are the mill's local time (Pakistan); "today" on the dashboard follows this.
ENV TZ=Asia/Karachi \
    ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "Hisaab.ERP.dll"]
