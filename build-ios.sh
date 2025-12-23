#!/bin/bash

echo "========================================"
echo "ICYOU.iOS Build Script (macOS)"
echo "========================================"
echo ""

# Установка переменных
CONFIG="Release"
FRAMEWORK="net10.0-ios"
ARCH="ios-arm64"
PROJECT_FILE="ICYOU.Mobile/ICYOU.Mobile.iOS.csproj"
IPA_PATH="ICYOU.Mobile/bin/$CONFIG/$FRAMEWORK/$ARCH/publish/ICYOU.Mobile.ipa"

# Проверка наличия Xcode
if ! command -v xcodebuild &> /dev/null; then
    echo "ОШИБКА: Xcode не установлен или не найден в PATH"
    echo "Установите Xcode из App Store"
    exit 1
fi

echo "✓ Xcode найден: $(xcodebuild -version | head -1)"
echo ""

# Вопрос 1: Очистить сборку?
echo "[Шаг 1] Очистить предыдущие сборки?"
read -p "(y/n, по умолчанию n): " CLEAN_BUILD
if [[ "$CLEAN_BUILD" =~ ^[Yy]$ ]]; then
    echo "Очистка проектов..."
    dotnet clean -c "$CONFIG"
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось очистить проекты"
        exit 1
    fi
    echo "Очистка завершена успешно"
else
    echo "Очистка пропущена"
fi
echo ""

# Вопрос 2: Собрать проекты?
echo "[Шаг 2] Собрать проекты?"
read -p "(y/n, по умолчанию y): " BUILD_PROJECTS
if [[ "$BUILD_PROJECTS" =~ ^[Nn]$ ]]; then
    echo "Сборка пропущена"
else
    echo ""
    echo "Восстановление NuGet пакетов..."
    dotnet restore
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось восстановить пакеты"
        exit 1
    fi
    echo "Пакеты восстановлены успешно"
    echo ""

    echo "[1/6] Сборка ICYOU.SDK (базовая библиотека) для iOS..."
    dotnet build ICYOU.SDK/ICYOU.SDK.ios.csproj -c "$CONFIG" -f "$FRAMEWORK" -r "$ARCH"
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось собрать ICYOU.SDK"
        exit 1
    fi
    echo ""

    echo "[2/6] Сборка ICYOU.Core для iOS..."
    dotnet build ICYOU.Core/ICYOU.Core.ios.csproj -c "$CONFIG" -f "$FRAMEWORK" -r "$ARCH"
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось собрать ICYOU.Core"
        exit 1
    fi
    echo ""

    echo "[3/6] Сборка ICYOU.Modules.E2E для iOS..."
    dotnet build ICYOU.Modules.E2E/ICYOU.Modules.E2E.ios.csproj -c "$CONFIG" -f "$FRAMEWORK" -r "$ARCH"
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось собрать ICYOU.Modules.E2E"
        exit 1
    fi
    echo ""

    echo "[4/6] Сборка ICYOU.Modules.Quote для iOS..."
    dotnet build ICYOU.Modules.Quote/ICYOU.Modules.Quote.ios.csproj -c "$CONFIG" -f "$FRAMEWORK" -r "$ARCH"
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось собрать ICYOU.Modules.Quote"
        exit 1
    fi
    echo ""

    echo "[5/6] Сборка ICYOU.Modules.LinkPreview для iOS..."
    dotnet build ICYOU.Modules.LinkPreview/ICYOU.Modules.LinkPreview.ios.csproj -c "$CONFIG" -f "$FRAMEWORK" -r "$ARCH"
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось собрать ICYOU.Modules.LinkPreview"
        exit 1
    fi
    echo ""

    echo "[6/6] Сборка ICYOU.Mobile (iOS) для $ARCH..."
    dotnet build "$PROJECT_FILE" -c "$CONFIG" -f "$FRAMEWORK" -r "$ARCH"
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось собрать ICYOU.Mobile iOS"
        exit 1
    fi
    echo "Все проекты собраны успешно"
    echo ""
fi

# Вопрос 3: Опубликовать IPA?
echo "[Шаг 3] Опубликовать IPA (publish)?"
echo "ПРИМЕЧАНИЕ: Для публикации iOS требуется:"
echo "  - Сертификаты разработчика Apple"
echo "  - Provisioning Profile"
read -p "(y/n, по умолчанию n): " PUBLISH_IPA
if [[ ! "$PUBLISH_IPA" =~ ^[Yy]$ ]]; then
    echo "Публикация пропущена"
else
    echo ""
    echo "Публикация IPA для iOS..."
    dotnet publish "$PROJECT_FILE" -c "$CONFIG" -f "$FRAMEWORK" -r "$ARCH"
    if [ $? -ne 0 ]; then
        echo "ОШИБКА: Не удалось опубликовать IPA"
        echo ""
        echo "Возможные причины:"
        echo "  1. Не настроены сертификаты Apple Developer"
        echo "  2. Не установлен Provisioning Profile"
        echo "  3. Проблемы с Xcode"
        echo ""
        exit 1
    fi

    if [ -f "$IPA_PATH" ]; then
        echo "========================================"
        echo "IPA СОЗДАН УСПЕШНО!"
        echo "========================================"
        echo "Путь: $(pwd)/$IPA_PATH"
        echo "Размер: $(stat -f%z "$IPA_PATH" 2>/dev/null || stat -c%s "$IPA_PATH" 2>/dev/null) байт"
        echo "========================================"
        echo ""

        # Копирование IPA в папку build
        mkdir -p build
        cp -f "$IPA_PATH" "build/ICYOU.Mobile-ios.ipa"
        echo "IPA скопирован в: $(pwd)/build/ICYOU.Mobile-ios.ipa"
        echo ""
    else
        echo "ПРЕДУПРЕЖДЕНИЕ: IPA файл не найден по пути: $IPA_PATH"
        echo ""
    fi
fi

echo ""
echo "========================================"
echo "СКРИПТ ЗАВЕРШЕН"
echo "========================================"
echo ""
echo "Для установки IPA на устройство используйте:"
echo "  - Xcode (Window → Devices and Simulators)"
echo "  - Apple Configurator"
echo "  - TestFlight (для распространения)"
echo ""
