
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

# Проверка наличия домена
if [ -z "$DOMAIN" ]; then
    echo "Предупреждение: Переменная DOMAIN не задана в deploy.conf, используем o0o0.online"
    DOMAIN="o0o0.online"
fi

# Проверка наличия сети
if [ -z "$DOCKER_NETWORK" ]; then
    echo "Предупреждение: Переменная DOCKER_NETWORK не задана, используем 'web'"
    DOCKER_NETWORK="web"
fi

echo "=== Старт развертывания проекта на VPS ($PROD_VPS_IP) для домена $DOMAIN ==="

# Упаковка исходного кода локально (исключаем лишний мусор)
echo "Упаковка исходного кода проекта..."
PROJECT_DIR=$(pwd)
cd ..
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

cd "$PROJECT_DIR"

# Создание временной папки на VPS и копирование архива исходников
echo "Копирование исходного кода на VPS..."
ssh -o StrictHostKeyChecking=no "$PROD_VPS_USER@$PROD_VPS_IP" "mkdir -p /root/pege_build"
scp /tmp/project.tar.gz "$PROD_VPS_USER@$PROD_VPS_IP:/root/pege_build/project.tar.gz"

rm /tmp/project.tar.gz

# Удаленное выполнение команд на VPS
ssh "$PROD_VPS_USER@$PROD_VPS_IP" TG_BOT_TOKEN="$TG_BOT_TOKEN" CONTAINER_NAME="$CONTAINER_NAME" IMAGE_NAME="$IMAGE_NAME" DOMAIN="$DOMAIN" DOCKER_NETWORK="$DOCKER_NETWORK" 'bash -s' << 'EOF'
    set -e

    # --- БЛОК ПРОВЕРКИ И УСТАНОВКИ DOCKER ---
    if ! command -v docker &> /dev/null; then
        echo "Docker не найден на сервере. Начинаем автоматическую установку..."
        apt-get update && apt-get install -y curl
        curl -fsSL https://docker.com -o get-docker.sh
        sh get-docker.sh
        rm get-docker.sh
        systemctl enable docker
        systemctl start docker
        echo "Docker успешно установлен!"
    else
        echo "Docker уже установлен на сервере."
    fi
    # ----------------------------------------

    # --- СОЗДАНИЕ DOCKER СЕТИ ---
    echo "Проверка/создание Docker сети ${DOCKER_NETWORK}..."
    if ! docker network ls | grep -q "${DOCKER_NETWORK}"; then
        echo "Создаем Docker сеть ${DOCKER_NETWORK}..."
        docker network create ${DOCKER_NETWORK}
    else
        echo "Docker сеть ${DOCKER_NETWORK} уже существует"
    fi
    # ----------------------------------------

    # Переходим в рабочую папку и распаковываем исходный код
    cd /root/pege_build
    tar -xzf project.tar.gz
    rm project.tar.gz

    cp $(which ffmpeg) ./ffmpeg
    cp $(which ffprobe) ./ffprobe
    
    # Подготовка папок
    mkdir -p /root/storage
    mkdir -p /root/audio
    mkdir -p /root/caddy_data
    mkdir -p /root/caddy_config
    mkdir -p /root/caddy_logs
    
    # Создаем .env файл
    cat << ENV_EOF > /root/storage/pege.env
# Создано автоматически при развертывании
Telegram__BotToken=${TG_BOT_TOKEN}
DelayMeasurementMode=false
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

    echo "Файл /root/storage/pege.env успешно обновлен."

    # --- НАСТРОЙКА CADDY ---
    echo "Настройка Caddy reverse proxy..."
    
    cat << CADDY_EOF > /root/caddy_config/Caddyfile
${DOMAIN} {
    reverse_proxy ${CONTAINER_NAME}:8080
    
    # WebSocket поддержка
    handle_path /ws/* {
        reverse_proxy ${CONTAINER_NAME}:8080
    }
    
    # Заголовки безопасности
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains"
        X-Content-Type-Options "nosniff"
        X-XSS-Protection "1; mode=block"
        X-Frame-Options "SAMEORIGIN"
    }
    
    log {
        output file /var/log/caddy/access.log
    }
}
CADDY_EOF

    echo "Caddyfile создан для домена ${DOMAIN}"

    # Останавливаем старый Caddy если есть
    if [ $(docker ps -a -q -f name=^/caddy$) ]; then
        echo "Остановка старого Caddy..."
        docker stop caddy || true
        docker rm caddy || true
    fi

    # Запускаем Caddy
    echo "Запуск Caddy контейнера..."
    docker run -d \
        --name caddy \
        --restart unless-stopped \
        --network ${DOCKER_NETWORK} \
        -p 80:80 \
        -p 443:443 \
        -v /root/caddy_data:/data \
        -v /root/caddy_config:/config \
        -v /root/caddy_config/Caddyfile:/etc/caddy/Caddyfile \
        -v /root/caddy_logs:/var/log/caddy \
        caddy:latest

    echo "Caddy контейнер запущен"
    # ----------------------------------------

    # Получаем ID старого образа
    OLD_IMAGE_ID=$(docker images -q ${IMAGE_NAME}:latest 2>/dev/null || true)

    # Сборка Docker-образа
    echo "Сборка Docker-образа..."
    docker build -t ${IMAGE_NAME}:latest -f Pege/Dockerfile .

    # Остановка старого контейнера приложения
    if [ $(docker ps -a -q -f name=^/${CONTAINER_NAME}$) ]; then
        echo "Остановка старого контейнера приложения..."
        # Пытаемся остановить стрим
        curl -X PUT "https://${DOMAIN}/api/stream/stop"
        sleep 5
        docker stop ${CONTAINER_NAME} || true
        docker rm ${CONTAINER_NAME} || true
    fi

    # Запуск нового контейнера приложения
    echo "Запуск нового контейнера приложения..."
    docker run -d \
        --name ${CONTAINER_NAME} \
        --restart unless-stopped \
        --network ${DOCKER_NETWORK} \
        -u 0 \
        --log-opt max-size=10m \
        --log-opt max-file=3 \
        --mount "type=bind,source=/root/storage,target=/app/storage,bind-propagation=shared" \
        --mount "type=bind,source=/root/audio,target=/app/audio,bind-propagation=shared" \
        --env-file /root/storage/pege.env \
        ${IMAGE_NAME}:latest

    echo "Контейнер приложения запущен"

    # Очистка
    cd /root
    rm -rf /root/pege_build

    # Удаляем старый образ
    if [ -n "$OLD_IMAGE_ID" ]; then
        NEW_IMAGE_ID=$(docker images -q ${IMAGE_NAME}:latest)
        if [ "$OLD_IMAGE_ID" != "$NEW_IMAGE_ID" ]; then
            echo "Удаление старого образа ($OLD_IMAGE_ID)..."
            docker rmi "$OLD_IMAGE_ID" 2>/dev/null || true
        fi
    fi

    # Проверка статуса
    echo ""
    echo "=== Статус контейнеров ==="
    docker ps -f name=caddy -f name=${CONTAINER_NAME}
    
    echo ""
    echo "=== Проверка сети ==="
    docker network inspect ${DOCKER_NETWORK} | grep -A 5 "Containers" || true
    
    echo ""
    echo "=== Развертывание успешно завершено! ==="
    echo "Приложение доступно по адресу: https://${DOMAIN}"
    
    echo ""
    echo "Ожидание получения SSL сертификата (30 секунд)..."
    sleep 30
    
    echo "Проверка SSL сертификата:"
    docker exec caddy caddy cert list || echo "Сертификат еще не получен, проверьте позже"
    
EOF

echo ""
read -n 1 -s -r -p "Нажмите любую клавишу для выхода..."
echo ""