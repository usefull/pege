#!/bin/bash

# Увеличь системные буферы на хостовой ОС:
# >>> sudo nano /etc/sysctl.conf
# Добавь строки:
# net.core.rmem_max = 16777216
# net.core.wmem_max = 16777216
# net.ipv4.tcp_rmem = 4096 87380 16777216
# net.ipv4.tcp_wmem = 4096 65536 16777216
# Примени изменения:
# >>> sudo sysctl -p

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
PROJECT_DIR=$(pwd) # Запоминаем, где мы сейчас
cd ..              # Поднимаемся в папку решения
tar --exclude='bin' \
    --exclude='obj' \
    --exclude='.git' \
    --exclude='.vs' \
    --exclude='audio' \
    --exclude='storage' \
    --exclude='Client' \
    --exclude='LoadTests' \
    --exclude='SimpleTest' \
    -czf /tmp/project.tar.gz .

cd "$PROJECT_DIR"  # Возвращаемся обратно в папку проекта

# Создание временной папки на VPS и копирование архива исходников
echo "Копирование исходного кода на VPS..."
ssh -o StrictHostKeyChecking=no "$VPS_USER@$VPS_IP" "mkdir -p /root/pege_build"
scp /tmp/project.tar.gz "$VPS_USER@$VPS_IP:/root/pege_build/project.tar.gz"

# Удаляем локальный архив исходников, он больше не нужен
rm /tmp/project.tar.gz

# Удаленное выполнение команд на VPS с передачей нужных переменных
echo "Подключение к VPS для сборки и настройки..."
ssh "$VPS_USER@$VPS_IP" TG_BOT_TOKEN="$TG_BOT_TOKEN" CONTAINER_NAME="$CONTAINER_NAME" IMAGE_NAME="$IMAGE_NAME" VPS_IP="$VPS_IP" 'bash -s' << 'EOF'
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

    cp $(which ffmpeg) ./ffmpeg
    cp $(which ffprobe) ./ffprobe
    
    # Подготовка папок в каталоге /root
    mkdir -p /root/storage
    mkdir -p /root/audio
    
    # Создаем новый или полностью перезаписываем существующий .env файл
    cat << ENV_EOF > /root/storage/pege.env
# Создано автоматически при развертывании
Telegram__BotToken=${TG_BOT_TOKEN}
DelayMeasurementMode=true
ConsumerRate__0=0
ConsumerRate__10=1000
ConsumerRate__11=1100
ConsumerRate__12=1200
ConsumerRate__13=1300
ConsumerRate__14=1400
ConsumerRate__15=1500
ConsumerRate__16=1600
ConsumerRate__17=1700
ConsumerRate__18=1800
ConsumerRate__19=1900
ConsumerRate__20=2000
ConsumerRate__21=2100
ConsumerRate__22=2200
ConsumerRate__23=2300
ConsumerRate__24=2400
ConsumerRate__25=2500
MeasuringPeriods__0=15
ASPNETCORE_URLS=http://+:8080
ENV_EOF

    echo "Файл /root/storage/pege.env успешно обновлен на сервере."

    # Перед сборкой получаем ID текущего (старого) образа приложения, чтобы потом его удалить
    OLD_IMAGE_ID=$(docker images -q ${IMAGE_NAME}:latest 2>/dev/null || true)

    # Сборка нового Docker-образа прямо на сервере
    echo "Сборка Docker-образа на сервере (используется локальный кэш)..."
    docker build -t ${IMAGE_NAME}:latest -f Pege/Dockerfile .

    # Остановка и удаление старого контейнера, если он существует
    if [ $(docker ps -a -q -f name=^/${CONTAINER_NAME}$) ]; then
        echo "Остановка и удаление старого контейнера..."
        curl -X PUT "http://${VPS_IP}:8080/api/stream/stop"
        sleep 10
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
        --mount "type=bind,source=/root/audio,target=/app/audio,bind-propagation=shared" \
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
