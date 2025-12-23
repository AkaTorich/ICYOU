@echo off
chcp 65001 >nul
echo ========================================
echo ICYOU.iOS Build Script
echo ========================================
echo.
echo ВНИМАНИЕ: Для сборки iOS приложений требуется:
echo   - macOS с установленным Xcode
echo   - Или удаленное подключение к Mac
echo   - Или использование облачной сборки
echo.

REM Установка переменных
set CONFIG=Release
set FRAMEWORK=net10.0-ios
set ARCH=ios-arm64
set PROJECT_FILE=ICYOU.Mobile\ICYOU.Mobile.iOS.csproj
set IPA_PATH=ICYOU.Mobile\bin\%CONFIG%\%FRAMEWORK%\%ARCH%\publish\ICYOU.Mobile.ipa

REM Вопрос 1: Очистить сборку?
echo [Шаг 1] Очистить предыдущие сборки?
set /p CLEAN_BUILD="(Y/N, по умолчанию N): "
if /i "%CLEAN_BUILD%"=="Y" (
    echo Очистка проектов...
    dotnet clean -c %CONFIG%
    if %ERRORLEVEL% NEQ 0 (
        echo ОШИБКА: Не удалось очистить проекты
        pause
        exit /b 1
    )
    echo Очистка завершена успешно
) else (
    echo Очистка пропущена
)
echo.

REM Вопрос 2: Собрать проекты?
echo [Шаг 2] Собрать проекты?
set /p BUILD_PROJECTS="(Y/N, по умолчанию Y): "
if /i "%BUILD_PROJECTS%"=="N" (
    echo Сборка пропущена
    goto PUBLISH
)

echo.
echo Восстановление NuGet пакетов...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось восстановить пакеты
    pause
    exit /b 1
)
echo Пакеты восстановлены успешно
echo.

echo [1/6] Сборка ICYOU.SDK (базовая библиотека) для iOS...
dotnet build ICYOU.SDK\ICYOU.SDK.ios.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.SDK
    pause
    exit /b 1
)
echo.

echo [2/6] Сборка ICYOU.Core для iOS...
dotnet build ICYOU.Core\ICYOU.Core.ios.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Core
    pause
    exit /b 1
)
echo.

echo [3/6] Сборка ICYOU.Modules.E2E для iOS...
dotnet build ICYOU.Modules.E2E\ICYOU.Modules.E2E.ios.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Modules.E2E
    pause
    exit /b 1
)
echo.

echo [4/6] Сборка ICYOU.Modules.Quote для iOS...
dotnet build ICYOU.Modules.Quote\ICYOU.Modules.Quote.ios.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Modules.Quote
    pause
    exit /b 1
)
echo.

echo [5/6] Сборка ICYOU.Modules.LinkPreview для iOS...
dotnet build ICYOU.Modules.LinkPreview\ICYOU.Modules.LinkPreview.ios.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Modules.LinkPreview
    pause
    exit /b 1
)
echo.

echo [6/6] Сборка ICYOU.Mobile (iOS) для %ARCH%...
dotnet build %PROJECT_FILE% -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Mobile iOS
    pause
    exit /b 1
)
echo Все проекты собраны успешно
echo.

:PUBLISH
REM Вопрос 3: Опубликовать IPA?
echo [Шаг 3] Опубликовать IPA (publish)?
echo ПРИМЕЧАНИЕ: Для публикации iOS требуется:
echo   - Сертификаты разработчика Apple
echo   - Provisioning Profile
echo   - Подключение к Mac с Xcode
set /p PUBLISH_IPA="(Y/N, по умолчанию N): "
if /i "%PUBLISH_IPA%"=="N" (
    echo Публикация пропущена
    goto END
)

echo.
echo Публикация IPA для iOS...
dotnet publish %PROJECT_FILE% -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось опубликовать IPA
    echo.
    echo Возможные причины:
    echo   1. Отсутствует подключение к Mac
    echo   2. Не настроены сертификаты Apple Developer
    echo   3. Не установлен Xcode на Mac
    echo.
    pause
    exit /b 1
)

if exist "%IPA_PATH%" (
    echo ========================================
    echo IPA СОЗДАН УСПЕШНО!
    echo ========================================
    echo Путь: %CD%\%IPA_PATH%
    for %%A in ("%IPA_PATH%") do echo Размер: %%~zA байт
    echo ========================================
    echo.

    REM Копирование IPA в папку build
    if not exist "build" mkdir build
    copy /Y "%IPA_PATH%" "build\ICYOU.Mobile-ios.ipa" >nul
    echo IPA скопирован в: %CD%\build\ICYOU.Mobile-ios.ipa
    echo.
) else (
    echo ПРЕДУПРЕЖДЕНИЕ: IPA файл не найден по пути: %IPA_PATH%
    echo.
)

:END
echo.
echo ========================================
echo СКРИПТ ЗАВЕРШЕН
echo ========================================
echo.
echo Для установки IPA на устройство используйте:
echo   - Xcode (Devices and Simulators)
echo   - Apple Configurator
echo   - TestFlight (для распространения)
echo.
pause
