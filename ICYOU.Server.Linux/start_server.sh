#!/bin/bash

# Проверка аргументов для фонового запуска
BACKGROUND=false
if [ "$1" == "--background" ] || [ "$1" == "-b" ]; then
    BACKGROUND=true
fi

echo "Starting ICYOU Server (Linux)..."
echo "=================================="

# Добавление .NET в PATH если установлен, но не в PATH
if [ -d "/usr/share/dotnet" ] && ! echo "$PATH" | grep -q "/usr/share/dotnet"; then
    export PATH=$PATH:/usr/share/dotnet
fi

# Проверка наличия .NET
if ! command -v dotnet &> /dev/null; then
    if [ -f "/usr/share/dotnet/dotnet" ]; then
        export PATH=$PATH:/usr/share/dotnet
    else
        echo "ОШИБКА: .NET SDK не установлен!"
        echo "Установите .NET 8.0 SDK: https://dotnet.microsoft.com/download"
        exit 1
    fi
fi

# Сборка проекта
echo "Сборка проекта..."
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "ОШИБКА: Сборка не удалась!"
    exit 1
fi

# Запуск сервера
if [ "$BACKGROUND" = true ]; then
    echo "Запуск сервера в фоновом режиме..."
    echo "Логи сохраняются в server.log"
    nohup dotnet run -c Release -- --port 7777 > server.log 2>&1 &
    SERVER_PID=$!
    echo "Сервер запущен в фоне (PID: $SERVER_PID)"
    echo "Для остановки используйте: kill $SERVER_PID"
    echo "Для просмотра логов: tail -f server.log"
else
    echo "Запуск сервера..."
    echo ""
    dotnet run -c Release -- --port 7777
fi

