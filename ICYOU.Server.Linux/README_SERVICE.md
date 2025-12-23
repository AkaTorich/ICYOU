# Установка и использование ICYOU Server как systemd сервиса

## Требования

- Linux система с systemd
- .NET 8.0 Runtime установлен
- Права root для установки сервиса

## Установка

### 1. Сборка проекта

Сначала соберите проект для Linux:

```bash
cd ICYOU.Server.Linux
dotnet publish -c Release -r linux-x64 --self-contained false
```

Или используйте существующую сборку в `bin/Release/net8.0/linux-x64/` или `publish_linux/`.

### 2. Установка сервиса

**Для существующего сервера в `/home/psyshout/publish_linux`:**

Просто скопируйте service файл и обновите systemd:

```bash
sudo cp icyou-server.service /etc/systemd/system/
sudo systemctl daemon-reload
```

**Или используйте скрипт установки:**

```bash
sudo chmod +x install_service.sh
sudo ./install_service.sh /home/psyshout/publish_linux psyshout
```

Скрипт выполнит:
- Проверку существования пользователя и директории
- Копирование `run_server.sh` если его нет
- Установку прав доступа
- Установку systemd unit файла
- Перезагрузку systemd

**По умолчанию используется:**
- Путь: `/home/psyshout/publish_linux`
- Пользователь: `psyshout`

### 3. Запуск и автозапуск

Запустить сервис:

```bash
sudo systemctl start icyou-server
```

Включить автозапуск при загрузке системы:

```bash
sudo systemctl enable icyou-server
```

Или сразу запустить и включить автозапуск:

```bash
sudo systemctl enable --now icyou-server
```

## Управление сервисом

### Проверка статуса

```bash
sudo systemctl status icyou-server
```

### Просмотр логов

**ВАЖНО: Логи находятся в systemd journal!**

В реальном времени (следить за логами):

```bash
sudo journalctl -u icyou-server -f
```

Последние 100 строк:

```bash
sudo journalctl -u icyou-server -n 100
```

Логи за сегодня:

```bash
sudo journalctl -u icyou-server --since today
```

Все логи с начала:

```bash
sudo journalctl -u icyou-server
```

Логи с временными метками:

```bash
sudo journalctl -u icyou-server --no-pager
```

Экспорт логов в файл:

```bash
sudo journalctl -u icyou-server > /tmp/icyou-server.log
```

**Если сервис не запускается, обязательно проверьте логи:**

```bash
sudo journalctl -u icyou-server -n 50 --no-pager
```

### Остановка сервиса

```bash
sudo systemctl stop icyou-server
```

### Перезапуск сервиса

```bash
sudo systemctl restart icyou-server
```

### Отключение автозапуска

```bash
sudo systemctl disable icyou-server
```

## Удаление сервиса

```bash
# Остановить и отключить сервис
sudo systemctl stop icyou-server
sudo systemctl disable icyou-server

# Удалить файлы
sudo rm /etc/systemd/system/icyou-server.service
sudo systemctl daemon-reload

# Опционально: удалить пользователя и файлы
sudo userdel icyou
sudo rm -rf /opt/icyou-server
```

## Настройка

### Изменение порта

Отредактируйте файл `/opt/icyou-server/run_server.sh` и измените порт:

```bash
sudo nano /opt/icyou-server/run_server.sh
```

Измените строку с `--port 7777` на нужный порт, затем перезапустите:

```bash
sudo systemctl restart icyou-server
```

### Изменение рабочей директории

Отредактируйте service файл:

```bash
sudo nano /etc/systemd/system/icyou-server.service
```

Измените `WorkingDirectory` и `ExecStart`, затем:

```bash
sudo systemctl daemon-reload
sudo systemctl restart icyou-server
```

## Решение проблем

### Сервис не запускается

Проверьте логи:

```bash
sudo journalctl -u icyou-server -n 50
```

### Проверка прав доступа

Убедитесь, что файлы принадлежат пользователю `icyou`:

```bash
sudo chown -R icyou:icyou /opt/icyou-server
```

### Проверка .NET Runtime

Убедитесь, что .NET установлен:

```bash
dotnet --version
```

Если не установлен, установите .NET 8.0 Runtime:

```bash
# Ubuntu/Debian
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
sudo ./dotnet-install.sh --runtime dotnet --version 8.0.0
```

