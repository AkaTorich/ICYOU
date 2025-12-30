#!/bin/bash
# Скрипт установки systemd сервиса для ICYOU Server

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_NAME="icyou-server"
SERVICE_FILE="$SCRIPT_DIR/$SERVICE_NAME.service"
SYSTEMD_DIR="/etc/systemd/system"

# Использование существующего пути или опционально копирование
INSTALL_DIR="${1:-/home/psyshout/publish_linux}"
SERVICE_USER="${2:-psyshout}"

echo "=================================="
echo "Установка ICYOU Server как systemd сервиса"
echo "=================================="
echo ""
echo "Путь к серверу: $INSTALL_DIR"
echo "Пользователь: $SERVICE_USER"
echo ""

# Проверка прав root
if [ "$EUID" -ne 0 ]; then 
    echo "ОШИБКА: Этот скрипт должен быть запущен с правами root (sudo)"
    exit 1
fi

# Проверка существования директории
if [ ! -d "$INSTALL_DIR" ]; then
    echo "ОШИБКА: Директория $INSTALL_DIR не существует!"
    echo ""
    echo "Использование:"
    echo "  $0 [путь_к_серверу] [пользователь]"
    echo ""
    echo "Пример:"
    echo "  sudo $0 /home/psyshout/publish_linux psyshout"
    exit 1
fi

# Проверка наличия файлов сервера
if [ ! -f "$INSTALL_DIR/ICYOU.Server.Linux.dll" ] && [ ! -f "$INSTALL_DIR/ICYOU.Server.Linux" ]; then
    echo "ПРЕДУПРЕЖДЕНИЕ: Не найдены файлы сервера в $INSTALL_DIR"
    echo "Проверьте, что директория содержит ICYOU.Server.Linux.dll или ICYOU.Server.Linux"
    read -p "Продолжить установку? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Проверка наличия run_server.sh
if [ ! -f "$INSTALL_DIR/run_server.sh" ]; then
    echo "Копирование run_server.sh в $INSTALL_DIR..."
    if [ -f "$SCRIPT_DIR/run_server.sh" ]; then
        cp "$SCRIPT_DIR/run_server.sh" "$INSTALL_DIR/"
        chmod +x "$INSTALL_DIR/run_server.sh"
        echo "   run_server.sh скопирован"
    else
        echo "ОШИБКА: run_server.sh не найден в $SCRIPT_DIR"
        exit 1
    fi
fi

echo "1. Проверка пользователя $SERVICE_USER..."
if ! id "$SERVICE_USER" &>/dev/null; then
    echo "   ПРЕДУПРЕЖДЕНИЕ: Пользователь $SERVICE_USER не существует!"
    echo "   Создайте пользователя или укажите существующего:"
    echo "   sudo $0 $INSTALL_DIR [имя_пользователя]"
    exit 1
else
    echo "   Пользователь $SERVICE_USER существует"
fi

# Установка прав на директорию
echo ""
echo "2. Установка прав доступа..."
chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/run_server.sh"
chmod +x "$INSTALL_DIR/ICYOU.Server.Linux" 2>/dev/null || true
echo "   Права установлены"

echo ""
echo "3. Установка systemd сервиса..."
# Обновление пути в service файле
sed "s|WorkingDirectory=.*|WorkingDirectory=$INSTALL_DIR|g" "$SERVICE_FILE" > "/tmp/$SERVICE_NAME.service"
sed -i "s|ExecStart=.*|ExecStart=$INSTALL_DIR/run_server.sh|g" "/tmp/$SERVICE_NAME.service"
sed -i "s|^User=.*|User=$SERVICE_USER|g" "/tmp/$SERVICE_NAME.service"
sed -i "s|^Group=.*|Group=$SERVICE_USER|g" "/tmp/$SERVICE_NAME.service"
cp "/tmp/$SERVICE_NAME.service" "$SYSTEMD_DIR/$SERVICE_NAME.service"
chmod 644 "$SYSTEMD_DIR/$SERVICE_NAME.service"
echo "   Сервис установлен"

echo ""
echo "4. Перезагрузка systemd..."
systemctl daemon-reload
echo "   Systemd перезагружен"

echo ""
echo "=================================="
echo "Установка завершена!"
echo "=================================="
echo ""
echo "Управление сервисом:"
echo "  Запуск:        sudo systemctl start $SERVICE_NAME"
echo "  Остановка:     sudo systemctl stop $SERVICE_NAME"
echo "  Перезапуск:    sudo systemctl restart $SERVICE_NAME"
echo "  Статус:        sudo systemctl status $SERVICE_NAME"
echo "  Логи:          sudo journalctl -u $SERVICE_NAME -f"
echo "  Автозапуск:    sudo systemctl enable $SERVICE_NAME"
echo "  Отключить:     sudo systemctl disable $SERVICE_NAME"
echo ""
echo "Для запуска и включения автозапуска выполните:"
echo "  sudo systemctl enable --now $SERVICE_NAME"
echo ""

