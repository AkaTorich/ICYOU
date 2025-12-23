@echo off
chcp 65001 >nul
echo ========================================
echo ICYOU.Android - Просмотр логов
echo ========================================
echo.

set ADB=C:\Users\psysh\AppData\Local\Android\Sdk\platform-tools\adb.exe
set PACKAGE=com.companyname.icyou.mobile

echo Выберите режим просмотра логов:
echo.
echo [1] Очистить логи и показать новые (живой просмотр)
echo [2] Показать все текущие логи приложения
echo [3] Показать только ошибки (FATAL, ERROR)
echo [4] Запустить приложение и показать логи запуска
echo [5] Очистить все логи
echo.
set /p CHOICE="Введите номер (1-5): "

if "%CHOICE%"=="1" goto LIVE_LOGS
if "%CHOICE%"=="2" goto ALL_LOGS
if "%CHOICE%"=="3" goto ERROR_LOGS
if "%CHOICE%"=="4" goto LAUNCH_AND_LOGS
if "%CHOICE%"=="5" goto CLEAR_LOGS

echo Неверный выбор
pause
exit /b 1

:LIVE_LOGS
echo.
echo Очистка логов...
%ADB% logcat -c
echo Логи очищены. Запуск живого просмотра...
echo Нажмите Ctrl+C для остановки
echo ========================================
%ADB% logcat | findstr /I "ICYOU AndroidRuntime FATAL ERROR MonoDroid"
goto END

:ALL_LOGS
echo.
echo Все логи приложения:
echo ========================================
%ADB% logcat -d | findstr /I "ICYOU AndroidRuntime MonoDroid"
goto END

:ERROR_LOGS
echo.
echo Только ошибки:
echo ========================================
%ADB% logcat -d | findstr /I "FATAL ERROR UnsatisfiedLinkError"
goto END

:LAUNCH_AND_LOGS
echo.
echo Очистка старых логов...
%ADB% logcat -c
echo.
echo Запуск приложения...
%ADB% shell monkey -p %PACKAGE% -c android.intent.category.LAUNCHER 1
echo.
echo Ожидание запуска приложения (3 секунды)...
timeout /t 3 /nobreak >nul
echo.
echo Логи запуска:
echo ========================================
%ADB% logcat -d | findstr /I "ICYOU AndroidRuntime FATAL ERROR MonoDroid UnsatisfiedLinkError"
goto END

:CLEAR_LOGS
echo.
echo Очистка всех логов...
%ADB% logcat -c
echo Логи очищены
goto END

:END
echo.
echo ========================================
pause
