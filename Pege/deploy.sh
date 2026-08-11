
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

echo "=== Старт развертывания проекта на VPS ($VPS_IP) ==="

# Упаковка исходного кода локально
echo "Упаковка проекта..."
tar --exclude='bin' \
    --exclude='obj' \
    --exclude='.git' \
    --exclude='.vs' \
    --exclude='mp3' \
    --exclude='storage' \
    -czf /tmp/project.tar.gz .

# Создание временной папки и копирование архива через SSH-ключ
echo "Копирование архива на VPS..."
ssh -o StrictHostKeyChecking=no "$VPS_USER@$VPS_IP" "mkdir -p /root/pege_build"
scp /tmp/project.tar.gz "$VPS_USER@$VPS_IP:/root/pege_build/project.tar.gz"

# Удаляем локальный архив
rm /tmp/project.tar.gz

# Удаленное выполнение команд на VPS
echo "Подключение к VPS для настройки и сборки..."
ssh "$VPS_USER@$VPS_IP" << EOF
    set -e

    # --- БЛОК ПРОВЕРКИ И УСТАНОВКИ DOCKER ---
    if ! command -v docker &> /dev/null; then
        echo "Docker не найден на сервере. Начинаем автоматическую установку через официальный скрипт..."
        
        # Обновляем систему и ставим curl, если его вдруг нет
        apt-get update && apt-get install -y curl
        
        # Скачиваем и запускаем официальный установщик Docker, который сделает всё за нас
        curl -fsSL https://get.docker.com -o get-docker.sh
        sh get-docker.sh
        
        # Удаляем временный скрипт установщика
        rm get-docker.sh
        
        # Включаем и запускаем службу
        systemctl enable docker
        systemctl start docker
        
        echo "Docker успешно установлен!"
    else
        echo "Docker уже установлен на сервере. Пропускаем шаг установки."
    fi
    # ----------------------------------------

    # Переходим в рабочую папку и распаковываем проект
    cd /root/pege_build
    tar -xzf project.tar.gz
    rm project.tar.gz
    
    # Подготовка папок на хосте
    mkdir -p /root/storage
    mkdir -p /root/mp3
    chown -R 1654:1654 /root/storage
    chown -R 1654:1654 /root/mp3
    
    # Создаем пустой .env файл, если его нет
    if [ ! -f /root/storage/pege.env ]; then
        echo "# Создано автоматически" > /root/storage/pege.env
        chown 1654:1654 /root/storage/pege.env
        echo "Файл /root/storage/pege.env создан на сервере. Заполните его переменными."
    fi

    # Остановка и удаление старого контейнера, если он существует
    if [ \$(docker ps -a -q -f name=^/${CONTAINER_NAME}\$) ]; then
        echo "Остановка и удаление старого контейнера..."
        docker stop ${CONTAINER_NAME}
        docker rm ${CONTAINER_NAME}
    fi

    # Сборка нового Docker-образа
    echo "Сборка Docker-образа..."
    docker build -t ${IMAGE_NAME}:latest .

    # Запуск контейнера со всеми флагами
    echo "Запуск нового контейнера..."
    docker run -d \
        --name ${CONTAINER_NAME} \
        --restart unless-stopped \
        -p 8080:8080 \
        -v /root/storage:/app/storage \
        -v /root/mp3:/app/mp3 \
        --env-file /root/storage/pege.env \
        ${IMAGE_NAME}:latest

    # Очистка временных файлов сборки
    cd /root
    rm -rf /root/pege_build

    # Очистка старых и неиспользуемых Docker-образов
    echo "Очистка неиспользуемых Docker-образов..."
    docker image prune -f

    echo "=== Развертывание успешно завершено! ==="
    echo "Текущий статус контейнера:"
    docker ps -f name=${CONTAINER_NAME}
EOF

echo ""
read -n 1 -s -r -p "Нажмите любую клавишу для выхода..."
echo ""
