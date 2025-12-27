#!/bin/bash
# Скрипт для добавления .NET в PATH навсегда

echo "Добавление /usr/share/dotnet в PATH..."

# Проверка наличия .NET
if [ ! -d "/usr/share/dotnet" ]; then
    echo "ОШИБКА: .NET не установлен в /usr/share/dotnet"
    exit 1
fi

# Добавление в ~/.bashrc
if [ -f ~/.bashrc ] && ! grep -q "/usr/share/dotnet" ~/.bashrc; then
    echo "" >> ~/.bashrc
    echo "# .NET Core" >> ~/.bashrc
    echo "export PATH=\$PATH:/usr/share/dotnet" >> ~/.bashrc
    echo "✓ Добавлено в ~/.bashrc"
fi

# Добавление в ~/.profile (если не в bashrc)
if [ -f ~/.profile ] && ! grep -q "/usr/share/dotnet" ~/.profile; then
    echo "" >> ~/.profile
    echo "# .NET Core" >> ~/.profile
    echo "export PATH=\$PATH:/usr/share/dotnet" >> ~/.profile
    echo "✓ Добавлено в ~/.profile"
fi

# Применение в текущей сессии
export PATH=$PATH:/usr/share/dotnet

echo ""
echo "✓ PATH обновлен!"
echo ""
echo "Проверка:"
dotnet --version
echo ""
echo "Готово! PATH будет автоматически добавлен при следующем входе в систему."

