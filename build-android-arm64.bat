@echo off
chcp 65001 >nul
echo ========================================
echo ICYOU.Android Build Script (ARM64)
echo ========================================
echo.

REM Установка переменных
set CONFIG=Release
set FRAMEWORK=net10.0-android
set ARCH=android-arm64
set PROJECT_FILE=ICYOU.Mobile\ICYOU.Mobile.ARM64.csproj
set APK_PATH=ICYOU.Mobile\bin\%CONFIG%\%FRAMEWORK%\%ARCH%\publish\com.companyname.icyou.mobile-Signed.apk

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

echo [1/6] Сборка ICYOU.SDK (базовая библиотека) для %ARCH%...
dotnet build ICYOU.SDK\ICYOU.SDK.arm64.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.SDK
    pause
    exit /b 1
)
echo.

echo [2/6] Сборка ICYOU.Core для %ARCH%...
dotnet build ICYOU.Core\ICYOU.Core.arm64.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Core
    pause
    exit /b 1
)
echo.

echo [3/6] Сборка ICYOU.Modules.E2E для %ARCH%...
dotnet build ICYOU.Modules.E2E\ICYOU.Modules.E2E.arm64.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Modules.E2E
    pause
    exit /b 1
)
echo.

echo [4/6] Сборка ICYOU.Modules.Quote для %ARCH%...
dotnet build ICYOU.Modules.Quote\ICYOU.Modules.Quote.arm64.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Modules.Quote
    pause
    exit /b 1
)
echo.

echo [5/6] Сборка ICYOU.Modules.LinkPreview для %ARCH%...
dotnet build ICYOU.Modules.LinkPreview\ICYOU.Modules.LinkPreview.arm64.csproj -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Modules.LinkPreview
    pause
    exit /b 1
)
echo.

echo [6/6] Сборка ICYOU.Mobile (ARM64) для %ARCH%...
dotnet build %PROJECT_FILE% -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось собрать ICYOU.Mobile ARM64
    pause
    exit /b 1
)
echo Все проекты собраны успешно
echo.

:PUBLISH
REM Вопрос 3: Опубликовать APK?
echo [Шаг 3] Опубликовать APK (publish)?
set /p PUBLISH_APK="(Y/N, по умолчанию Y): "
if /i "%PUBLISH_APK%"=="N" (
    echo Публикация пропущена
    goto INSTALL
)

echo.
echo Публикация APK для %ARCH%...
dotnet publish %PROJECT_FILE% -c %CONFIG% -f %FRAMEWORK% -r %ARCH%
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось опубликовать APK
    pause
    exit /b 1
)

if exist "%APK_PATH%" (
    echo ========================================
    echo APK СОЗДАН УСПЕШНО!
    echo ========================================
    echo Путь: %CD%\%APK_PATH%
    for %%A in ("%APK_PATH%") do echo Размер: %%~zA байт
    echo ========================================
    echo.

    REM Копирование APK в папку build
    if not exist "build" mkdir build
    copy /Y "%APK_PATH%" "build\ICYOU.Mobile-arm64.apk"
    echo APK скопирован в: %CD%\build\ICYOU.Mobile-arm64.apk
    echo.
) else (
    echo ПРЕДУПРЕЖДЕНИЕ: APK файл не найден по пути: %APK_PATH%
    echo.
)

:INSTALL
REM Вопрос 4: Установить на устройство?
echo [Шаг 4] Установить на подключенное устройство?
set /p INSTALL_DEVICE="(Y/N, по умолчанию N): "
if /i "%INSTALL_DEVICE%"=="N" (
    echo Установка пропущена
    goto END
)

if not exist "%APK_PATH%" (
    echo ОШИБКА: APK файл не найден. Сначала соберите проект.
    pause
    exit /b 1
)

echo.
echo Проверка подключенных устройств...
adb devices
echo.

echo Установка APK на устройство...
adb install -r "%APK_PATH%"
if %ERRORLEVEL% NEQ 0 (
    echo ОШИБКА: Не удалось установить APK
    pause
    exit /b 1
)

echo ========================================
echo APK УСТАНОВЛЕН УСПЕШНО!
echo ========================================
echo.

echo Запустить приложение сейчас?
set /p LAUNCH_APP="(Y/N, по умолчанию N): "
if /i "%LAUNCH_APP%"=="Y" (
    echo Запуск приложения...
    adb shell monkey -p com.companyname.icyou.mobile -c android.intent.category.LAUNCHER 1
    echo Приложение запущено
)

:END
echo.
echo ========================================
echo СКРИПТ ЗАВЕРШЕН
echo ========================================
pause
