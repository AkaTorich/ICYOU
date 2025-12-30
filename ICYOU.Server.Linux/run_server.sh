#!/bin/bash
# Скрипт для запуска уже скомпилированного сервера

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Добавление .NET в PATH если установлен, но не в PATH
if [ -d "/usr/share/dotnet" ] && ! echo "$PATH" | grep -q "/usr/share/dotnet"; then
    export PATH=$PATH:/usr/share/dotnet
fi

# Проверка наличия .NET runtime
if ! command -v dotnet &> /dev/null; then
    if [ -f "/usr/share/dotnet/dotnet" ]; then
        export PATH=$PATH:/usr/share/dotnet
    else
        echo "ОШИБКА: .NET Runtime не установлен!"
        echo "Установите .NET 8.0 Runtime: https://dotnet.microsoft.com/download"
        exit 1
    fi
fi

# Запуск скомпилированного сервера
if [ -f "ICYOU.Server.Linux.dll" ]; then
    # Опубликованное приложение в текущей директории
    dotnet ICYOU.Server.Linux.dll --port 7777
elif [ -f "bin/Release/net8.0/linux-x64/ICYOU.Server.Linux" ]; then
    ./bin/Release/net8.0/linux-x64/ICYOU.Server.Linux --port 7777
elif [ -f "bin/Release/net8.0/ICYOU.Server.Linux.dll" ]; then
    dotnet bin/Release/net8.0/ICYOU.Server.Linux.dll --port 7777
else
    echo "ОШИБКА: Скомпилированный сервер не найден!"
    echo "Убедитесь что ICYOU.Server.Linux.dll находится в текущей директории"
    exit 1
fi

