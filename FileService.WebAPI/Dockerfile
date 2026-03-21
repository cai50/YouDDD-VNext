#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["FileService.WebAPI/FileService.WebAPI.csproj", "FileService.WebAPI/"]
RUN dotnet restore "FileService.WebAPI/FileService.WebAPI.csproj"
COPY . .
WORKDIR "/src/FileService.WebAPI"
RUN dotnet build "FileService.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FileService.WebAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# 关键步骤：创建 wwwroot 文件夹并设置权限
RUN mkdir -p /app/wwwroot && chmod 755 /app/wwwroot
ENTRYPOINT ["dotnet", "FileService.WebAPI.dll"]