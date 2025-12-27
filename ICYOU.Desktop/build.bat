@echo off
chcp 65001 >nul
setlocal

echo ========================================
echo    ICYOU Messenger Build Script
echo ========================================
echo.

set OUTPUT=build
set CONFIG=Release

:: Очистка
if exist "%OUTPUT%" rd /s /q "%OUTPUT%"

:: Сборка
echo [1/6] Сборка проекта...
dotnet build -c %CONFIG% >nul 2>&1
if errorlevel 1 (
    echo ОШИБКА: Сборка не удалась
    dotnet build -c %CONFIG%
    pause
    exit /b 1
)
echo       OK

:: Создание папок
echo [2/6] Создание структуры папок...
mkdir "%OUTPUT%"
mkdir "%OUTPUT%\Server"
mkdir "%OUTPUT%\Client"
mkdir "%OUTPUT%\Client\modules"
mkdir "%OUTPUT%\Client\Downloads"
mkdir "%OUTPUT%\SDK"
mkdir "%OUTPUT%\Modules"
mkdir "%OUTPUT%\Emotes"
mkdir "%OUTPUT%\Emotes\default"
echo       OK

:: Копирование сервера
echo [3/6] Копирование сервера...
xcopy "ICYOU.Server\bin\%CONFIG%\net8.0\*.*" "%OUTPUT%\Server\" /s /q /y >nul
echo       OK

:: Копирование клиента
echo [4/6] Копирование клиента...
xcopy "ICYOU.Client\bin\%CONFIG%\net8.0-windows\*.*" "%OUTPUT%\Client\" /s /q /y >nul
echo       OK

:: Копирование SDK
echo [5/6] Копирование SDK...
copy "ICYOU.SDK\bin\%CONFIG%\net8.0\ICYOU.SDK.dll" "%OUTPUT%\SDK\" >nul
copy "ICYOU.SDK\bin\%CONFIG%\net8.0\ICYOU.SDK.xml" "%OUTPUT%\SDK\" >nul 2>nul
echo       OK

:: Копирование модулей
echo [6/6] Копирование модулей...

:: Пример модуля
if exist "ICYOU.SDK.Example\bin\%CONFIG%\net8.0\ICYOU.SDK.Example.dll" (
    copy "ICYOU.SDK.Example\bin\%CONFIG%\net8.0\ICYOU.SDK.Example.dll" "%OUTPUT%\Modules\" >nul
)

:: Модуль цитирования
if exist "ICYOU.Modules.Quote\bin\%CONFIG%\net8.0\ICYOU.Modules.Quote.dll" (
    copy "ICYOU.Modules.Quote\bin\%CONFIG%\net8.0\ICYOU.Modules.Quote.dll" "%OUTPUT%\Modules\" >nul
    copy "ICYOU.Modules.Quote\bin\%CONFIG%\net8.0\ICYOU.Modules.Quote.dll" "%OUTPUT%\Client\modules\" >nul
)

:: Модуль превью ссылок
if exist "ICYOU.Modules.LinkPreview\bin\%CONFIG%\net8.0\ICYOU.Modules.LinkPreview.dll" (
    copy "ICYOU.Modules.LinkPreview\bin\%CONFIG%\net8.0\*.dll" "%OUTPUT%\Modules\" >nul
    copy "ICYOU.Modules.LinkPreview\bin\%CONFIG%\net8.0\ICYOU.Modules.LinkPreview.dll" "%OUTPUT%\Client\modules\" >nul
    if exist "ICYOU.Modules.LinkPreview\bin\%CONFIG%\net8.0\HtmlAgilityPack.dll" (
        copy "ICYOU.Modules.LinkPreview\bin\%CONFIG%\net8.0\HtmlAgilityPack.dll" "%OUTPUT%\Client\modules\" >nul
    )
)

:: Модуль E2E шифрования
if exist "ICYOU.Modules.E2E\bin\%CONFIG%\net8.0\ICYOU.Modules.E2E.dll" (
    copy "ICYOU.Modules.E2E\bin\%CONFIG%\net8.0\ICYOU.Modules.E2E.dll" "%OUTPUT%\Modules\" >nul
    copy "ICYOU.Modules.E2E\bin\%CONFIG%\net8.0\ICYOU.Modules.E2E.dll" "%OUTPUT%\Client\modules\" >nul
)

echo       OK

:: Копирование смайлов
if exist "emotes\*.*" (
    xcopy "emotes\*.*" "%OUTPUT%\Emotes\" /s /q /y >nul 2>nul
    xcopy "emotes\*.*" "%OUTPUT%\Client\emotes\" /s /q /y >nul 2>nul
)

:: Создание стартовых скриптов
echo @echo off > "%OUTPUT%\Server\start_server.bat"
echo echo Starting ICYOU Server... >> "%OUTPUT%\Server\start_server.bat"
echo ICYOU.Server.exe --port 7777 >> "%OUTPUT%\Server\start_server.bat"
echo pause >> "%OUTPUT%\Server\start_server.bat"

echo @echo off > "%OUTPUT%\Client\start_client.bat"
echo start ICYOU.Client.exe >> "%OUTPUT%\Client\start_client.bat"

:: Linux скрипт для сервера
echo #!/bin/bash > "%OUTPUT%\Server\start_server.sh"
echo echo "Starting ICYOU Server..." >> "%OUTPUT%\Server\start_server.sh"
echo dotnet ICYOU.Server.dll --port 7777 >> "%OUTPUT%\Server\start_server.sh"

echo.
echo ========================================
echo    Сборка завершена!
echo ========================================
echo.
echo Результат в папке: %OUTPUT%
echo.
echo Структура:
echo   build\
echo   +-- Server\          - Сервер (Win/Linux)
echo   +-- Client\          - WPF клиент
echo   ¦   +-- modules\     - Активные модули
echo   ¦   +-- emotes\      - Смайлы
echo   ¦   +-- Downloads\   - Загрузки
echo   ¦   +-- userdata\    - Данные пользователей
echo   +-- SDK\             - SDK для разработки
echo   +-- Modules\         - Все модули (для копирования)
echo   +-- Emotes\          - Паки смайлов
echo.
echo Модули:
echo   - Quote         (Цитирование)
echo   - LinkPreview   (Превью ссылок)
echo   - E2E           (Шифрование сообщений)
echo.
echo Данные пользователей:
echo   build\Client\userdata\[username]\messages.db
echo.
pause
