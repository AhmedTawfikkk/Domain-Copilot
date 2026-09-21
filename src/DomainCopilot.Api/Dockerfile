FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/DomainCopilot.Api/DomainCopilot.Api.csproj", "src/DomainCopilot.Api/"]
COPY ["src/DomainCopilot.Application/DomainCopilot.Application.csproj", "src/DomainCopilot.Application/"]
COPY ["src/DomainCopilot.Infrastructure/DomainCopilot.Infrastructure.csproj", "src/DomainCopilot.Infrastructure/"]
COPY ["src/DomainCopilot.Domain/DomainCopilot.Domain.csproj", "src/DomainCopilot.Domain/"]
RUN dotnet restore "src/DomainCopilot.Api/DomainCopilot.Api.csproj"

COPY . .
RUN dotnet publish "src/DomainCopilot.Api/DomainCopilot.Api.csproj" --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends tesseract-ocr poppler-utils \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DomainCopilot.Api.dll"]
