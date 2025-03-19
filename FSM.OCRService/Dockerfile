#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
#EXPOSE 81
#EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["OCR/FSM.OCRService/FSM.OCRService.csproj", "OCR/FSM.OCRService/"]
COPY ["Environment/EnvironmentLib/EnvironmentLib.csproj", "Environment/EnvironmentLib/"]
RUN dotnet restore "OCR/FSM.OCRService/FSM.OCRService.csproj"
COPY . .
WORKDIR "/src/OCR/FSM.OCRService"
RUN dotnet build "FSM.OCRService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FSM.OCRService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FSM.OCRService.dll"]