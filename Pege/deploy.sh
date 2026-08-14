#!/bin/bash

# Останавливать скрипт при любых ошибках
set -e

# Загрузка конфигурации
if [ -f "deploy.conf" ]; then
    source deploy.conf
else
    echo "Ошибка: Файл конфигурации deploy.conf не найден!"
    exit 1
fi

# Проверка наличия токена в конфиге
if [ -z "$TG_BOT_TOKEN" ]; then
    echo "Предупреждение: Переменная TG_BOT_TOKEN пуста или отсутствует в deploy.conf"
fi

echo "=== Старт развертывания проекта на VPS ($VPS_IP) ==="

# Упаковка исходного кода локально (исключаем лишний мусор)
echo "Упаковка исходного кода проекта..."
tar --exclude='bin' \
    --exclude='obj' \
    --exclude='.git' \
    --exclude='.vs' \
    --exclude='mp3' \
    --exclude='storage' \
    -czf /tmp/project.tar.gz .

# Создание временной папки на VPS и копирование легкого архива исходников
echo "Копирование исходного кода на VPS..."
ssh -o StrictHostKeyChecking=no "$VPS_USER@$VPS_IP" "mkdir -p /root/pege_build"
scp /tmp/project.tar.gz "$VPS_USER@$VPS_IP:/root/pege_build/project.tar.gz"

# Удаляем локальный архив исходников, он больше не нужен
rm /tmp/project.tar.gz

# Удаленное выполнение команд на VPS с передачей нужных переменных
echo "Подключение к VPS для сборки и настройки..."
ssh "$VPS_USER@$VPS_IP" TG_BOT_TOKEN="$TG_BOT_TOKEN" CONTAINER_NAME="$CONTAINER_NAME" IMAGE_NAME="$IMAGE_NAME" 'bash -s' << 'EOF'
    set -e

    # --- БЛОК ПРОВЕРКИ И УСТАНОВКИ DOCKER ---
    if ! command -v docker &> /dev/null; then
        echo "Docker не найден на сервере. Начинаем автоматическую установку через официальный скрипт..."
        apt-get update && apt-get install -y curl
        curl -fsSL https://docker.com -o get-docker.sh
        sh get-docker.sh
        rm get-docker.sh
        systemctl enable docker
        systemctl start docker
        echo "Docker успешно установлен!"
    else
        echo "Docker уже установлен на сервере. Пропускаем шаг установки."
    fi
    # ----------------------------------------

    # Переходим в рабочую папку и распаковываем исходный код
    cd /root/pege_build
    tar -xzf project.tar.gz
    rm project.tar.gz
    
    # Подготовка папок в каталоге /root
    mkdir -p /root/storage
    mkdir -p /root/audio
    
    # Создаем и заполняем .env файл, если его нет
    if [ ! -f /root/storage/pege.env ]; then
        echo "# Создано автоматически при развертывании" > /root/storage/pege.env
        echo "Telegram__BotToken=${TG_BOT_TOKEN}" >> /root/storage/pege.env
        echo "ASPNETCORE_URLS=http://+:8080" >> /root/storage/pege.env
        echo "Файл /root/storage/pege.env создан на сервере, и в него записан токен."
    fi

    # Перед сборкой получаем ID текущего (старого) образа приложения, чтобы потом его удалить
    OLD_IMAGE_ID=$(docker images -q ${IMAGE_NAME}:latest 2>/dev/null || true)

    # Сборка нового Docker-образа прямо на сервере
    echo "Сборка Docker-образа на сервере (используется локальный кэш)..."
    docker build -t ${IMAGE_NAME}:latest .

    # Остановка и удаление старого контейнера, если он существует
    if [ $(docker ps -a -q -f name=^/${CONTAINER_NAME}$) ]; then
        echo "Остановка и удаление старого контейнера..."
        docker stop ${CONTAINER_NAME}
        docker rm ${CONTAINER_NAME}
    fi

    # Запуск нового контейнера с умным монтированием папок
    echo "Запуск нового контейнера..."
    docker run -d \
        --name ${CONTAINER_NAME} \
        --restart unless-stopped \
        -u 0 \
        --net=host \
        --log-opt max-size=10m \
        --log-opt max-file=3 \
        --mount "type=bind,source=/root/storage,target=/app/storage,bind-propagation=shared" \
        --mount "type=bind,source=/root/audio,target=/app/mp3,bind-propagation=shared" \
        --env-file /root/storage/pege.env \
        ${IMAGE_NAME}:latest

    # Очистка временных файлов сборки на VPS
    cd /root
    rm -rf /root/pege_build

    # БЕЗОПАСНАЯ ОЧИСТКА: Удаляем строго предыдущий образ приложения, если он существовал и изменился
    if [ -n "$OLD_IMAGE_ID" ]; then
        NEW_IMAGE_ID=$(docker images -q ${IMAGE_NAME}:latest)
        if [ "$OLD_IMAGE_ID" != "$NEW_IMAGE_ID" ]; then
            echo "Удаление старой версии образа приложения ($OLD_IMAGE_ID)..."
            docker rmi "$OLD_IMAGE_ID" 2>/dev/null || true
        fi
    fi

    echo "=== Развертывание успешно завершено! ==="
    echo "Текущий статус контейнера:"
    docker ps -f name=${CONTAINER_NAME}
EOF

echo ""
read -n 1 -s -r -p "Нажмите любую клавишу для выхода..."
echo ""
