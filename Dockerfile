FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY HerProof.sln ./
COPY src/HerProof.Domain/HerProof.Domain.csproj src/HerProof.Domain/
COPY src/HerProof.Application/HerProof.Application.csproj src/HerProof.Application/
COPY src/HerProof.AI/HerProof.AI.csproj src/HerProof.AI/
COPY src/HerProof.Infrastructure/HerProof.Infrastructure.csproj src/HerProof.Infrastructure/
COPY src/HerProof.AssessmentWorker/HerProof.AssessmentWorker.csproj src/HerProof.AssessmentWorker/
COPY src/HerProof.Web/HerProof.Web.csproj src/HerProof.Web/
COPY tests/HerProof.Domain.Tests/HerProof.Domain.Tests.csproj tests/HerProof.Domain.Tests/
COPY tests/HerProof.Application.Tests/HerProof.Application.Tests.csproj tests/HerProof.Application.Tests/
COPY tests/HerProof.AI.Tests/HerProof.AI.Tests.csproj tests/HerProof.AI.Tests/
COPY tests/HerProof.Integration.Tests/HerProof.Integration.Tests.csproj tests/HerProof.Integration.Tests/

RUN dotnet restore src/HerProof.Web/HerProof.Web.csproj

COPY . .
RUN dotnet publish src/HerProof.Web/HerProof.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HerProof.Web.dll"]
