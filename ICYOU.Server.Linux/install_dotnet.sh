#!/bin/bash
# Скрипт установки .NET 8 на Linux сервер
# Поддерживает: Ubuntu/Debian, CentOS/RHEL, Fedora

echo "=========================================="
echo "  Установка .NET 8 на Linux сервер"
echo "=========================================="
echo ""

# Определение дистрибутива
if [ -f /etc/os-release ]; then
    . /etc/os-release
    OS=$ID
    VER=$VERSION_ID
else
    echo "ОШИБКА: Не удалось определить дистрибутив Linux"
    exit 1
fi

echo "Обнаружен дистрибутив: $OS $VER"
echo ""

# Функция установки для Ubuntu
install_ubuntu() {
    echo "Установка для Ubuntu..."
    
    # Обновление пакетов
    sudo apt-get update
    
    # Установка зависимостей
    sudo apt-get install -y wget apt-transport-https software-properties-common
    
    # Определение версии Ubuntu
    UBUNTU_VERSION=$(lsb_release -rs)
    
    # Добавление репозитория Microsoft для Ubuntu
    wget https://packages.microsoft.com/config/ubuntu/${UBUNTU_VERSION}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    
    # Обновление пакетов
    sudo apt-get update
    
    # Установка .NET 8 SDK (для сборки) или Runtime (только для запуска)
    echo ""
    echo "Выберите вариант установки:"
    echo "1) .NET 8 SDK (для сборки и запуска)"
    echo "2) .NET 8 Runtime (только для запуска)"
    read -p "Ваш выбор (1 или 2): " choice
    
    if [ "$choice" = "1" ]; then
        sudo apt-get install -y dotnet-sdk-8.0
        echo ""
        echo "✓ .NET 8 SDK установлен"
    elif [ "$choice" = "2" ]; then
        sudo apt-get install -y dotnet-runtime-8.0 aspnetcore-runtime-8.0
        echo ""
        echo "✓ .NET 8 Runtime установлен"
    else
        echo "Неверный выбор, установка SDK по умолчанию"
        sudo apt-get install -y dotnet-sdk-8.0
    fi
}

# Функция установки для Debian (универсальный установщик)
install_debian() {
    echo "Установка для Debian..."
    
    # Обновление пакетов
    sudo apt-get update
    
    # Установка зависимостей
    sudo apt-get install -y wget tar gzip
    
    # Установка .NET 8 SDK (для сборки) или Runtime (только для запуска)
    echo ""
    echo "Выберите вариант установки:"
    echo "1) .NET 8 SDK (для сборки и запуска)"
    echo "2) .NET 8 Runtime (только для запуска)"
    read -p "Ваш выбор (1 или 2): " choice
    
    if [ "$choice" = "1" ]; then
        # Установка SDK через универсальный установщик
        echo "Скачивание .NET 8 SDK..."
        wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
        chmod +x dotnet-install.sh
        sudo ./dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet
        rm dotnet-install.sh
        
        # Добавление в PATH
        if ! grep -q "/usr/share/dotnet" /etc/profile; then
            echo 'export PATH=$PATH:/usr/share/dotnet' | sudo tee -a /etc/profile
        fi
        export PATH=$PATH:/usr/share/dotnet
        
        echo ""
        echo "✓ .NET 8 SDK установлен"
    elif [ "$choice" = "2" ]; then
        # Установка Runtime через универсальный установщик
        echo "Скачивание .NET 8 Runtime..."
        wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
        chmod +x dotnet-install.sh
        sudo ./dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet
        sudo ./dotnet-install.sh --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet
        rm dotnet-install.sh
        
        # Добавление в PATH
        if ! grep -q "/usr/share/dotnet" /etc/profile; then
            echo 'export PATH=$PATH:/usr/share/dotnet' | sudo tee -a /etc/profile
        fi
        export PATH=$PATH:/usr/share/dotnet
        
        echo ""
        echo "✓ .NET 8 Runtime установлен"
    else
        echo "Неверный выбор, установка SDK по умолчанию"
        wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
        chmod +x dotnet-install.sh
        sudo ./dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet
        rm dotnet-install.sh
        if ! grep -q "/usr/share/dotnet" /etc/profile; then
            echo 'export PATH=$PATH:/usr/share/dotnet' | sudo tee -a /etc/profile
        fi
        export PATH=$PATH:/usr/share/dotnet
    fi
}

# Функция установки для CentOS/RHEL
install_centos_rhel() {
    echo "Установка для CentOS/RHEL..."
    
    # Установка зависимостей
    sudo yum install -y wget
    
    # Добавление репозитория Microsoft
    sudo rpm -Uvh https://packages.microsoft.com/config/centos/8/packages-microsoft-prod.rpm
    
    # Установка .NET 8
    echo ""
    echo "Выберите вариант установки:"
    echo "1) .NET 8 SDK (для сборки и запуска)"
    echo "2) .NET 8 Runtime (только для запуска)"
    read -p "Ваш выбор (1 или 2): " choice
    
    if [ "$choice" = "1" ]; then
        sudo yum install -y dotnet-sdk-8.0
        echo ""
        echo "✓ .NET 8 SDK установлен"
    elif [ "$choice" = "2" ]; then
        sudo yum install -y dotnet-runtime-8.0 aspnetcore-runtime-8.0
        echo ""
        echo "✓ .NET 8 Runtime установлен"
    else
        echo "Неверный выбор, установка SDK по умолчанию"
        sudo yum install -y dotnet-sdk-8.0
    fi
}

# Функция установки для Fedora
install_fedora() {
    echo "Установка для Fedora..."
    
    # Установка зависимостей
    sudo dnf install -y wget
    
    # Добавление репозитория Microsoft
    sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
    wget -q https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/packages-microsoft-prod.rpm
    sudo rpm -Uvh packages-microsoft-prod.rpm
    rm packages-microsoft-prod.rpm
    
    # Установка .NET 8
    echo ""
    echo "Выберите вариант установки:"
    echo "1) .NET 8 SDK (для сборки и запуска)"
    echo "2) .NET 8 Runtime (только для запуска)"
    read -p "Ваш выбор (1 или 2): " choice
    
    if [ "$choice" = "1" ]; then
        sudo dnf install -y dotnet-sdk-8.0
        echo ""
        echo "✓ .NET 8 SDK установлен"
    elif [ "$choice" = "2" ]; then
        sudo dnf install -y dotnet-runtime-8.0 aspnetcore-runtime-8.0
        echo ""
        echo "✓ .NET 8 Runtime установлен"
    else
        echo "Неверный выбор, установка SDK по умолчанию"
        sudo dnf install -y dotnet-sdk-8.0
    fi
}

# Установка в зависимости от дистрибутива
case $OS in
    ubuntu)
        install_ubuntu
        ;;
    debian)
        install_debian
        ;;
    centos|rhel|rocky|almalinux)
        install_centos_rhel
        ;;
    fedora)
        install_fedora
        ;;
    *)
        echo "ОШИБКА: Неподдерживаемый дистрибутив: $OS"
        echo ""
        echo "Попытка установки через универсальный установщик..."
        sudo apt-get update
        sudo apt-get install -y wget tar gzip
        wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
        chmod +x dotnet-install.sh
        sudo ./dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet
        rm dotnet-install.sh
        if ! grep -q "/usr/share/dotnet" /etc/profile; then
            echo 'export PATH=$PATH:/usr/share/dotnet' | sudo tee -a /etc/profile
        fi
        export PATH=$PATH:/usr/share/dotnet
        ;;
esac

# Проверка установки
echo ""
echo "Проверка установки..."

# Добавление в PATH если еще не добавлено
if [ -d "/usr/share/dotnet" ] && ! echo "$PATH" | grep -q "/usr/share/dotnet"; then
    export PATH=$PATH:/usr/share/dotnet
fi

if command -v dotnet &> /dev/null || [ -f "/usr/share/dotnet/dotnet" ]; then
    if [ -f "/usr/share/dotnet/dotnet" ]; then
        /usr/share/dotnet/dotnet --version
    else
        dotnet --version
    fi
    echo ""
    echo "✓ .NET установлен успешно!"
    echo ""
    echo "ВАЖНО: Если команда 'dotnet' не работает, выполните:"
    echo "  export PATH=\$PATH:/usr/share/dotnet"
    echo "Или перезапустите терминал"
else
    echo "✗ ОШИБКА: .NET не найден после установки"
    echo ""
    echo "Попробуйте вручную:"
    echo "  export PATH=\$PATH:/usr/share/dotnet"
    echo "  /usr/share/dotnet/dotnet --version"
    exit 1
fi

