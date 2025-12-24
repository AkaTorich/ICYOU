# ICYOU.Mobile

Кроссплатформенное мобильное приложение на .NET MAUI для Android и iOS.

## 📱 Поддерживаемые платформы

- ✅ **Android** (x86_64 эмулятор + ARM64 устройства)
- ✅ **iOS** (ARM64 устройства)

## 🏗️ Структура проекта

```
ICYOU.Android/
├── ICYOU.Mobile/           # Главное MAUI приложение
├── ICYOU.Core/             # Основная бизнес-логика
├── ICYOU.SDK/              # SDK и API клиенты
├── ICYOU.Modules.E2E/      # Модуль E2E шифрования
├── ICYOU.Modules.Quote/    # Модуль цитирования
├── ICYOU.Modules.LinkPreview/ # Модуль превью ссылок
└── ICYOU.Server.Linux/     # Серверная часть (Linux)
```

## 🚀 Быстрый старт

### Требования

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) или новее
- **Для Android:**
  - Android SDK (автоматически устанавливается с Visual Studio)
  - Java 17
- **Для iOS:**
  - macOS с Xcode 14+
  - Apple Developer Account (для публикации)

### Установка зависимостей

```bash
# Установите MAUI workload
dotnet workload install maui
dotnet workload install android
dotnet workload install ios  # только на macOS
```

## 🔨 Сборка

### Android (Windows/Linux/macOS)

#### Для эмулятора (x86_64):
```bash
# Запустите скрипт сборки
build-android.bat       # Windows
./build-android.sh      # Linux/macOS
```

#### Для физических устройств (ARM64):
```bash
# Запустите скрипт сборки ARM64
build-android-arm64.bat # Windows
```

#### Вручную:
```bash
# x64 (эмулятор)
dotnet publish ICYOU.Mobile/ICYOU.Mobile.csproj -c Release -f net10.0-android -r android-x64

# ARM64 (устройства)
dotnet publish ICYOU.Mobile/ICYOU.Mobile.ARM64.csproj -c Release -f net10.0-android -r android-arm64
```

**Выходные файлы:**
- APK: `ICYOU.Mobile/bin/Release/net10.0-android/{arch}/publish/*.apk`
- Скопировано в: `build/ICYOU.Mobile-{arch}.apk`

### iOS (только macOS)

```bash
# Запустите скрипт сборки
./build-ios.sh
```

#### Вручную:
```bash
dotnet publish ICYOU.Mobile/ICYOU.Mobile.iOS.csproj -c Release -f net10.0-ios -r ios-arm64
```

**Выходные файлы:**
- IPA: `ICYOU.Mobile/bin/Release/net10.0-ios/ios-arm64/publish/*.ipa`
- Скопировано в: `build/ICYOU.Mobile-ios.ipa`

## ☁️ Облачная сборка (CI/CD)

### GitHub Actions (Рекомендуется)

При push в репозиторий автоматически запускается сборка для обеих платформ.

**Скачать собранные APK/IPA:**
1. Откройте вкладку [Actions](../../actions)
2. Выберите последний успешный workflow
3. Скачайте артефакты:
   - `android-x64-apk` - Android для эмулятора
   - `android-arm64-apk` - Android для телефона
   - `ICYOU-iOS-unsigned` - iOS неподписанный IPA (для AltStore/Sideloadly)

**Подробнее:**
- Android: см. [CLOUD-BUILD-GUIDE.md](CLOUD-BUILD-GUIDE.md)
- iOS: см. [BUILD_IOS.md](BUILD_IOS.md) - установка через AltStore

## 📦 Установка

### Android

#### Через ADB:
```bash
adb install -r build/ICYOU.Mobile-arm64.apk
```

#### Через файл:
1. Скопируйте APK на устройство
2. Откройте файловый менеджер
3. Установите APK

### iOS

#### 🌟 Рекомендуется: AltStore (бесплатно, без Mac)

1. **Установите AltStore** на iPhone: https://altstore.io/
2. **Скачайте IPA** из [GitHub Actions](../../actions) (артефакт `ICYOU-iOS-unsigned`)
3. **Откройте IPA** через Safari → "Open in AltStore"
4. **Готово!** Приложение установлено

✅ Автоматическое обновление подписи каждые 7 дней
✅ Работает на Windows/Mac/Linux
✅ Не требует Apple Developer ($99/год)

#### Альтернативы:

**Через Sideloadly** (Windows/Mac):
- Скачать: https://sideloadly.io/
- Установка аналогична AltStore, но без автообновления

**Через Xcode** (только macOS):
1. Подключите iPhone к Mac
2. Window → Devices and Simulators
3. Перетащите IPA на устройство

**Через TestFlight** (требуется Apple Developer $99/год):
1. Загрузите IPA в App Store Connect
2. Пригласите тестировщиков

**📖 Подробная инструкция:** [BUILD_IOS.md](BUILD_IOS.md)

## 🛠️ Разработка

### Visual Studio 2022 (Windows)
```bash
# Откройте solution
start ICYOU.Android.sln
```

### Visual Studio for Mac
```bash
open ICYOU.Android.sln
```

### Visual Studio Code
```bash
code .
```

### Rider
```bash
rider ICYOU.Android.sln
```

## 🐛 Отладка

### Android Emulator:
```bash
# Список эмуляторов
emulator -list-avds

# Запуск эмулятора
emulator -avd Pixel_5_API_33

# Установка и запуск
adb install -r build/ICYOU.Mobile-x64.apk
adb shell monkey -p com.companyname.icyou.mobile -c android.intent.category.LAUNCHER 1
```

### Логи Android:
```bash
# Все логи приложения
adb logcat -s "ICYOU"

# Только ошибки
adb logcat *:E

# Очистить и показывать новые
adb logcat -c && adb logcat
```

### Логи iOS:
```bash
# На Mac
idevicesyslog
```

## ⚙️ Конфигурация

### Android Signing (для публикации)

1. Создайте keystore:
```bash
keytool -genkey -v -keystore icyou.keystore -alias icyou -keyalg RSA -keysize 2048 -validity 10000
```

2. Настройте в `ICYOU.Mobile.csproj`:
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <AndroidKeyStore>true</AndroidKeyStore>
  <AndroidSigningKeyStore>icyou.keystore</AndroidSigningKeyStore>
  <AndroidSigningKeyAlias>icyou</AndroidSigningKeyAlias>
  <AndroidSigningKeyPass>ВАШ_ПАРОЛЬ</AndroidSigningKeyPass>
  <AndroidSigningStorePass>ВАШ_ПАРОЛЬ</AndroidSigningStorePass>
</PropertyGroup>
```

⚠️ **НИКОГДА не коммитьте keystore и пароли в Git!**

### iOS Signing (для публикации)

См. [BUILD-iOS-README.md](BUILD-iOS-README.md)

## 📝 Скрипты сборки

| Скрипт | Платформа | Архитектура |
|--------|-----------|-------------|
| `build-android.bat` | Android | x86_64 (эмулятор) |
| `build-android-arm64.bat` | Android | ARM64 (устройства) |
| `build-ios.bat` | iOS | ARM64 |
| `build-ios.sh` | iOS | ARM64 (macOS) |

## 🔍 Решение проблем

### Android: "UnsatisfiedLinkError: No implementation found for n_onCreate()"

**Причина:** AOT компиляция отключена

**Решение:**
- Используйте `ICYOU.Mobile.csproj` для x64 (AOT включен)
- Используйте `ICYOU.Mobile.ARM64.csproj` для ARM64 (AOT включен)

### iOS: "No valid iOS code signing keys found"

**Причина:** Отсутствуют сертификаты разработчика

**Решение:**
1. Откройте Xcode → Preferences → Accounts
2. Добавьте Apple ID
3. Manage Certificates → Create

### Build timeout в GitHub Actions

**Решение:** Оптимизируйте workflow (кэшируйте NuGet пакеты)

## 📚 Документация

- [Облачная сборка](CLOUD-BUILD-GUIDE.md)
- [iOS сборка (детально)](BUILD-iOS-README.md)
- [.NET MAUI Documentation](https://learn.microsoft.com/dotnet/maui/)

## 🔗 Полезные ссылки

- [.NET MAUI](https://dotnet.microsoft.com/apps/maui)
- [Android Developer](https://developer.android.com)
- [Apple Developer](https://developer.apple.com)

## 📄 Лицензия

[Укажите вашу лицензию]

## 👥 Авторы

[Укажите авторов проекта]
