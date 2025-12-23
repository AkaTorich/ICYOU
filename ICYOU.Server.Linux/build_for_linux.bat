@echo off
chcp 65001 >nul
echo ==========================================
echo   Сборка ICYOU Server для Linux
echo ==========================================
echo.

set OUTPUT=publish_linux
set CONFIG=Release

:: Очистка
if exist "%OUTPUT%" rd /s /q "%OUTPUT%"
if exist "libs" rd /s /q "libs"
mkdir libs

:: Сборка Core и SDK
echo [1/4] Сборка ICYOU.Core...
dotnet build ..\ICYOU.Core\ICYOU.Core.csproj -c %CONFIG% >nul 2>&1
if errorlevel 1 (
    echo ОШИБКА: Сборка ICYOU.Core не удалась
    dotnet build ..\ICYOU.Core\ICYOU.Core.csproj -c %CONFIG%
    pause
    exit /b 1
)
echo       OK

echo [2/4] Сборка ICYOU.SDK...
dotnet build ..\ICYOU.SDK\ICYOU.SDK.csproj -c %CONFIG% >nul 2>&1
if errorlevel 1 (
    echo ОШИБКА: Сборка ICYOU.SDK не удалась
    dotnet build ..\ICYOU.SDK\ICYOU.SDK.csproj -c %CONFIG%
    pause
    exit /b 1
)
echo       OK

:: Копирование DLL
echo [3/4] Копирование DLL...
copy "..\ICYOU.Core\bin\%CONFIG%\net8.0\ICYOU.Core.dll" "libs\" >nul
copy "..\ICYOU.SDK\bin\%CONFIG%\net8.0\ICYOU.SDK.dll" "libs\" >nul
copy "..\ICYOU.Core\bin\%CONFIG%\net8.0\Microsoft.Data.Sqlite.dll" "libs\" >nul 2>nul
copy "..\ICYOU.Core\bin\%CONFIG%\net8.0\SQLitePCLRaw.*.dll" "libs\" >nul 2>nul
echo       OK

:: Публикация для Linux
echo [4/4] Публикация для Linux x64...
dotnet publish -c %CONFIG% -r linux-x64 --self-contained false -o "%OUTPUT%" -p:BuildOnLinux=true >nul 2>&1
if errorlevel 1 (
    echo ОШИБКА: Публикация не удалась
    dotnet publish -c %CONFIG% -r linux-x64 --self-contained false -o "%OUTPUT%" -p:BuildOnLinux=true
    pause
    exit /b 1
)
echo       OK

:: Копирование дополнительных файлов
copy "run_server.sh" "%OUTPUT%\" >nul
copy "fix_dotnet_path.sh" "%OUTPUT%\" >nul

echo.
echo ==========================================
echo   Готово!
echo ==========================================
echo.
echo Файлы для Linux в папке: %OUTPUT%
echo.
echo Загрузите папку %OUTPUT% на сервер и выполните:
echo   chmod +x run_server.sh
echo   ./run_server.sh
echo.
pause


