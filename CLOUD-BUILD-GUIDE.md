# Облачная сборка iOS приложения

## Варианты облачной сборки

### 1. GitHub Actions (Рекомендуется) ⭐

**Преимущества:**
- ✅ **БЕСПЛАТНО** для публичных репозиториев
- ✅ 2000 минут/месяц бесплатно для приватных
- ✅ macOS runners включены
- ✅ Простая настройка через YAML
- ✅ Интеграция с GitHub

**Настройка:**

1. **Загрузите код на GitHub:**
   ```bash
   git init
   git add .
   git commit -m "Initial commit"
   git remote add origin https://github.com/ваш-username/ICYOU.Android.git
   git push -u origin main
   ```

2. **GitHub Actions автоматически запустится** при push в main ветку

3. **Результаты сборки:**
   - Перейдите в Actions → выберите workflow
   - Скачайте артефакты (iOS build)

4. **Для подписи IPA (опционально):**

   Добавьте в Settings → Secrets:
   - `APPLE_CERTIFICATE_BASE64` - сертификат в base64
   - `APPLE_CERTIFICATE_PASSWORD` - пароль сертификата
   - `PROVISIONING_PROFILE_BASE64` - профиль в base64

   Конвертация в base64:
   ```bash
   # На Mac/Linux
   base64 -i certificate.p12 -o certificate.txt
   base64 -i profile.mobileprovision -o profile.txt
   ```

---

### 2. Codemagic (Специализированный для мобильных приложений)

**Преимущества:**
- ✅ 500 минут/месяц бесплатно
- ✅ Специализирован для Flutter/MAUI
- ✅ Автоматическая публикация в App Store
- ✅ Удобный UI

**Настройка:**

1. Зарегистрируйтесь на https://codemagic.io

2. Подключите GitHub/GitLab/Bitbucket репозиторий

3. Создайте файл `codemagic.yaml`:
   ```yaml
   workflows:
     ios-workflow:
       name: iOS Build
       max_build_duration: 60
       environment:
         vars:
           XCODE_WORKSPACE: "ICYOU.Mobile.xcworkspace"
           XCODE_SCHEME: "ICYOU.Mobile"
         xcode: latest
         cocoapods: default
       scripts:
         - name: Install dependencies
           script: |
             dotnet workload install maui
             dotnet workload install ios
         - name: Build
           script: |
             dotnet build ICYOU.Mobile/ICYOU.Mobile.iOS.csproj -c Release -f net10.0-ios -r ios-arm64
       artifacts:
         - ICYOU.Mobile/bin/Release/**/*.ipa
   ```

4. Push в репозиторий → сборка запустится автоматически

**Цена:** $0 (500 мин) → $99/мес (безлимит)

---

### 3. Azure DevOps Pipelines

**Преимущества:**
- ✅ 1 бесплатный macOS pipeline (1800 мин/мес)
- ✅ Интеграция с Microsoft экосистемой
- ✅ Мощные возможности CI/CD

**Настройка:**

1. Создайте проект на https://dev.azure.com

2. Создайте файл `azure-pipelines.yml`:
   ```yaml
   trigger:
     - main

   pool:
     vmImage: 'macOS-latest'

   steps:
   - task: UseDotNet@2
     inputs:
       version: '10.x'

   - script: |
       dotnet workload install maui
       dotnet workload install ios
     displayName: 'Install MAUI workload'

   - script: |
       dotnet build ICYOU.Mobile/ICYOU.Mobile.iOS.csproj -c Release -f net10.0-ios -r ios-arm64
     displayName: 'Build iOS App'

   - task: PublishBuildArtifacts@1
     inputs:
       pathToPublish: 'ICYOU.Mobile/bin/Release/net10.0-ios/ios-arm64'
       artifactName: 'ios-build'
   ```

3. Настройте pipeline в Azure DevOps

**Цена:** Бесплатно (1800 мин/мес)

---

### 4. Bitrise

**Преимущества:**
- ✅ Специализирован для мобильных приложений
- ✅ 200 бесплатных сборок/месяц
- ✅ Готовые шаги для iOS

**Настройка:**

1. Зарегистрируйтесь на https://bitrise.io

2. Добавьте репозиторий

3. Выберите шаблон для .NET MAUI

4. Bitrise автоматически создаст workflow

**Цена:** $0 (200 сборок) → $50/мес

---

### 5. MacinCloud (Аренда Mac в облаке)

**Преимущества:**
- ✅ Полный доступ к macOS
- ✅ Можно использовать Xcode напрямую
- ✅ Удаленный рабочий стол

**Настройка:**

1. Зарегистрируйтесь на https://www.macincloud.com

2. Арендуйте Mac (от $1/час)

3. Подключитесь через VNC или RDP

4. Работайте как на обычном Mac

**Цена:** От $30/мес (pay-as-you-go)

---

## Сравнительная таблица

| Сервис | Бесплатный план | Цена | Сложность настройки | Рекомендация |
|--------|----------------|------|---------------------|--------------|
| **GitHub Actions** | 2000 мин/мес | $0-8/мес | ⭐⭐ | ✅ Лучший выбор |
| **Codemagic** | 500 мин/мес | $99/мес | ⭐ | Для продакшена |
| **Azure DevOps** | 1800 мин/мес | $0 | ⭐⭐⭐ | Если используете Azure |
| **Bitrise** | 200 сборок/мес | $50/мес | ⭐ | Альтернатива Codemagic |
| **MacinCloud** | Нет | $30+/мес | ⭐⭐⭐⭐ | Для полного контроля |

---

## Быстрый старт с GitHub Actions

### Шаг 1: Создайте репозиторий на GitHub

```bash
cd C:\Users\psysh\OneDrive\Desktop\ICYOU.Android
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/ICYOU.Android.git
git push -u origin main
```

### Шаг 2: Включите GitHub Actions

GitHub Actions уже включены по умолчанию. Файл `.github/workflows/build-ios.yml` уже создан.

### Шаг 3: Запустите сборку

Способ 1 - Автоматически при push:
```bash
git add .
git commit -m "Trigger iOS build"
git push
```

Способ 2 - Вручную через UI:
1. Перейдите в репозиторий на GitHub
2. Actions → Build iOS App
3. Run workflow → выберите ветку → Run

### Шаг 4: Скачайте результат

1. Откройте workflow run
2. Artifacts → ios-build
3. Скачайте ZIP

---

## Подпись IPA для публикации

Для публикации в App Store нужны:

### 1. Apple Developer Certificate (.p12)

Создание на Mac:
```bash
# Откройте Keychain Access
# Certificate Assistant → Request Certificate from CA
# Загрузите на developer.apple.com
# Скачайте и установите сертификат
# Экспортируйте как .p12
```

### 2. Provisioning Profile (.mobileprovision)

1. Перейдите на https://developer.apple.com
2. Certificates, Identifiers & Profiles
3. Profiles → + (Create)
4. Выберите App Store / Ad Hoc
5. Скачайте .mobileprovision

### 3. Добавьте в GitHub Secrets

```bash
# Конвертируйте в base64
base64 -i certificate.p12 -o cert.txt
base64 -i profile.mobileprovision -o profile.txt

# Скопируйте содержимое файлов
```

GitHub → Settings → Secrets → New repository secret:
- `APPLE_CERTIFICATE_BASE64`: содержимое cert.txt
- `APPLE_CERTIFICATE_PASSWORD`: пароль от .p12
- `PROVISIONING_PROFILE_BASE64`: содержимое profile.txt

### 4. Раскомментируйте секцию Publish в workflow

В `.github/workflows/build-ios.yml` раскомментируйте секцию:
```yaml
- name: Publish iOS App
  run: dotnet publish ...
```

---

## Решение проблем

### Ошибка: "Xcode version not found"
```yaml
# Укажите конкретную версию Xcode в workflow
- name: Select Xcode version
  run: sudo xcode-select -s /Applications/Xcode_15.0.app
```

### Ошибка: "Workload not installed"
```yaml
# Добавьте установку workload в workflow
- name: Install workloads
  run: |
    dotnet workload install maui --source https://api.nuget.org/v3/index.json
    dotnet workload install ios
```

### Превышен лимит минут
- Оптимизируйте workflow (кэшируйте зависимости)
- Используйте self-hosted runner
- Перейдите на платный план

---

## Рекомендации

**Для начала:**
→ Используйте **GitHub Actions** (бесплатно)

**Для серьезной разработки:**
→ Используйте **Codemagic** или **Bitrise**

**Если нужен полный контроль:**
→ Используйте **MacinCloud** или купите Mac Mini

**Если уже используете Microsoft:**
→ Используйте **Azure DevOps**
