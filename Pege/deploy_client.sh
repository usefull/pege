#!/bin/bash

set -e

echo "🚀 Деплой клиентского приложения Pege"

# Загрузка конфигурации
if [ -f "deploy.conf" ]; then
    source deploy.conf
else
    echo "Ошибка: Файл конфигурации deploy.conf не найден!"
    exit 1
fi

# 1. Удаляем старые файлы внутри контейнера
echo "🗑️ Удаление старых файлов в /app/app/..."
ssh $VPS_USER@$VPS_IP "docker exec $CONTAINER_NAME rm -rf /app/app/*"

# 2. Копируем новые файлы на VPS
echo "📤 Копирование файлов на VPS..."
ssh $VPS_USER@$VPS_IP "mkdir -p /tmp/client-deploy"
scp -r ./app/* $VPS_USER@$VPS_IP:/tmp/client-deploy/

# 3. Копируем файлы в контейнер
echo "📦 Копирование файлов в контейнер $CONTAINER_NAME:/app/app/..."
ssh $VPS_USER@$VPS_IP "docker cp /tmp/client-deploy/. $CONTAINER_NAME:/app/app/"

# 4. Очистка временной папки на VPS
ssh $VPS_USER@$VPS_IP "rm -rf /tmp/client-deploy"

# 5. Проверка
echo "📋 Проверка файлов внутри контейнера:"
ssh $VPS_USER@$VPS_IP "docker exec $CONTAINER_NAME ls -la /app/app/ | head -15"

echo ""
echo "✅ Деплой клиента завершен!"
