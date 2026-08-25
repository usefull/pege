
# Этот этап используется при запуске из VS в быстром режиме (по умолчанию для конфигурации отладки)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Включаем Server GC
ENV DOTNET_GCServer=1
ENV DOTNET_GCHeapCount=2
ENV DOTNET_GCAllowVeryLargeObjects=1
ENV DOTNET_GCHeapHardLimitPercent=80
ENV DOTNET_GCHeapHardLimit=0x80000000

# Устанавливаем FFmpeg
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    libfdk-aac2 \
    && rm -rf /var/lib/apt/lists/*

# Подменяем собранным с поддержкой libfdk
COPY ffmpeg /usr/bin/ffmpeg
COPY ffprobe /usr/bin/ffprobe
RUN chmod +x /usr/bin/ffmpeg
RUN chmod +x /usr/bin/ffprobe
USER $APP_UID

# Этот этап используется для сборки проекта службы
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Pege/Pege.csproj", "Pege/"]
COPY ["Pege.Test.Core/Pege.Test.Core.csproj", "Pege.Test.Core/"]
RUN dotnet restore "Pege/Pege.csproj"
COPY . .
WORKDIR "/src/Pege"
RUN dotnet build "Pege.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Pege.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pege.dll"]
