FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Prontuario.sln .
COPY src/Prontuario.Domain/Prontuario.Domain.csproj src/Prontuario.Domain/
COPY src/Prontuario.Application/Prontuario.Application.csproj src/Prontuario.Application/
COPY src/Prontuario.Infrastructure/Prontuario.Infrastructure.csproj src/Prontuario.Infrastructure/
COPY src/Prontuario.Api/Prontuario.Api.csproj src/Prontuario.Api/
COPY tests/Prontuario.Application.Tests/Prontuario.Application.Tests.csproj tests/Prontuario.Application.Tests/
RUN dotnet restore src/Prontuario.Api/Prontuario.Api.csproj

COPY . .
RUN dotnet publish src/Prontuario.Api/Prontuario.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Prontuario.Api.dll"]
