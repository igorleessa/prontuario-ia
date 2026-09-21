FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Prontuario.EmrDemo/Prontuario.EmrDemo.csproj src/Prontuario.EmrDemo/
RUN dotnet restore src/Prontuario.EmrDemo/Prontuario.EmrDemo.csproj
COPY src/Prontuario.EmrDemo/ src/Prontuario.EmrDemo/
RUN dotnet publish src/Prontuario.EmrDemo/Prontuario.EmrDemo.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Prontuario.EmrDemo.dll"]
