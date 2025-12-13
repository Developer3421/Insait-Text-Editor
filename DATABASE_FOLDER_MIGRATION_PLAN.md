# План міграції папок бази даних до директорії виконуваного файлу

## Поточний стан

### Існуючі шляхи до бази даних

1. **DatabaseService.cs** (старий сервіс):
   - Шлях: `AppContext.BaseDirectory\Database\documents.litedb`
   - Створює папку "Database" у базовій директорії
   - Використовується для збереження документів

2. **DatabaseConfig.cs** (нова система з шифруванням):
   - EncryptedDataPath: `AppDomain.CurrentDomain.BaseDirectory\Data\Encrypted`
   - KeysPath: `AppDomain.CurrentDomain.BaseDirectory\Data\Keys`
   - Підпапки в Data\Encrypted:
     - ChatHistory
     - Documents
     - Memory
     - Reasoning
     - Settings

### Проблема

У `.csproj` файлі визначені папки як частина проєкту:
```xml
<Folder Include="Database\" />
<Folder Include="Data\Encrypted\ChatHistory\" />
<Folder Include="Data\Encrypted\Documents\" />
<Folder Include="Data\Encrypted\Memory\" />
<Folder Include="Data\Encrypted\Reasoning\" />
<Folder Include="Data\Encrypted\Settings\" />
<Folder Include="Data\Keys\" />
```

При публікації single-file ці папки можуть не створюватися автоматично, оскільки вони порожні та включені лише як `<Folder>` items.

## Мета міграції

Забезпечити автоматичне створення всіх необхідних папок бази даних у директорії, де знаходиться виконуваний файл (exe), під час першого запуску додатка.

## План реалізації

### Етап 1: Створення централізованого сервісу ініціалізації папок

**Файл**: `Services/Database/Core/DatabaseFolderInitializer.cs`

**Функціонал**:
- Визначення всіх необхідних папок для бази даних
- Автоматичне створення папок при запуску додатка
- Перевірка існування папок
- Логування процесу створення
- Обробка помилок при створенні папок

**Структура папок**:
```
{BaseDirectory}/
├── Database/
│   └── documents.litedb (старий формат)
├── Data/
│   ├── Encrypted/
│   │   ├── ChatHistory/
│   │   ├── Documents/
│   │   ├── Memory/
│   │   ├── Reasoning/
│   │   └── Settings/
│   └── Keys/
```

### Етап 2: Оновлення DatabaseConfig.cs

**Зміни**:
- Додати метод `EnsureDirectoriesExist()`
- Використовувати `Path.Combine()` для кросплатформеності
- Додати валідацію шляхів
- Додати можливість користувацького шляху через конфігурацію

**Приклад**:
```csharp
public void EnsureDirectoriesExist()
{
    Directory.CreateDirectory(EncryptedDataPath);
    Directory.CreateDirectory(KeysPath);
    
    // Підпапки для encrypted data
    string[] subfolders = { "ChatHistory", "Documents", "Memory", "Reasoning", "Settings" };
    foreach (var folder in subfolders)
    {
        Directory.CreateDirectory(Path.Combine(EncryptedDataPath, folder));
    }
}
```

### Етап 3: Оновлення DatabaseService.cs

**Зміни**:
- Викликати `Directory.CreateDirectory()` у конструкторі (вже є)
- Додати перевірку доступу до запису
- Додати логування створення папки

### Етап 4: Інтеграція в App.axaml.cs

**Зміни в Program.cs або App.axaml.cs**:
- Викликати ініціалізацію папок до створення будь-яких сервісів БД
- Додати обробку помилок при неможливості створити папки
- Логувати розташування папок для діагностики

**Приклад**:
```csharp
public override void OnFrameworkInitializationCompleted()
{
    try
    {
        // Ініціалізація папок БД
        var folderInitializer = new DatabaseFolderInitializer();
        folderInitializer.EnsureAllDatabaseFoldersExist();
        
        // Створення сервісів БД
        DatabaseService = new DatabaseService();
        // ... інші сервіси
    }
    catch (Exception ex)
    {
        // Критична помилка - неможливо створити папки
        Logger.LogError($"Failed to initialize database folders: {ex}");
        // Показати користувачу повідомлення про помилку
    }
}
```

### Етап 5: Видалення папок з .csproj

**Зміни в InsaitTextEditor.csproj**:
- Видалити всі `<Folder Include="..." />` записи для БД
- Папки створюватимуться динамічно під час виконання

**До:**
```xml
<ItemGroup>
    <Folder Include="Database\" />
    <Folder Include="Data\Encrypted\ChatHistory\" />
    <Folder Include="Data\Encrypted\Documents\" />
    <Folder Include="Data\Encrypted\Memory\" />
    <Folder Include="Data\Encrypted\Reasoning\" />
    <Folder Include="Data\Encrypted\Settings\" />
    <Folder Include="Data\Keys\" />
</ItemGroup>
```

**Після:**
```xml
<!-- Папки БД створюються автоматично при першому запуску -->
```

### Етап 6: Тестування публікації

**Кроки тестування**:

1. **Чиста публікація**:
   ```cmd
   dotnet publish -c Release
   ```

2. **Перевірка структури**:
   - Скопіювати `publish\InsaitTextEditor.exe` у чисту папку
   - Запустити додаток
   - Переконатися, що папки створюються автоматично

3. **Тестові сценарії**:
   - [ ] Запуск із повністю чистої папки
   - [ ] Запуск з існуючою частковою структурою папок
   - [ ] Запуск без прав на запис (має показати помилку)
   - [ ] Перевірка створення всіх підпапок
   - [ ] Перевірка роботи шифрування після створення папок

### Етап 7: Оновлення документації

**Файли для оновлення**:
- `README.md` - додати інформацію про автоматичне створення папок
- `AES_DATABASE_ENCRYPTION_PROGRESS.md` - оновити статус міграції
- Додати коментарі в код про процес ініціалізації

## Технічні деталі

### Використання AppContext.BaseDirectory vs AppDomain.CurrentDomain.BaseDirectory

**Рекомендація**: Використовувати `AppContext.BaseDirectory`

**Причини**:
- `AppContext.BaseDirectory` - сучасний .NET підхід
- Краща підтримка в .NET 10
- Правильно працює з single-file publish
- Кросплатформеність

### Обробка помилок створення папок

**Можливі помилки**:
1. `UnauthorizedAccessException` - недостатньо прав
2. `IOException` - папка заблокована іншим процесом
3. `PathTooLongException` - шлях занадто довгий
4. `DirectoryNotFoundException` - батьківська папка не існує

**Стратегія**:
- Логувати всі помилки
- Показувати користувачу зрозуміле повідомлення
- Пропонувати альтернативне розташування (наприклад, `%APPDATA%`)

### Альтернативні розташування для БД

**Варіанти**:
1. **Поточне**: `{ExeDirectory}\Data\` (біля exe)
2. **AppData**: `%APPDATA%\InsaitTextEditor\Data\`
3. **LocalAppData**: `%LOCALAPPDATA%\InsaitTextEditor\Data\`
4. **Користувацьке**: Дозволити користувачу вибрати шлях

**Рекомендація**: 
- За замовчуванням: біля exe файлу (поточне)
- Fallback: `%LOCALAPPDATA%\InsaitTextEditor\Data\` якщо немає прав

## Переваги нового підходу

1. ✅ **Портативність**: Додаток створює папки автоматично
2. ✅ **Single-file publish**: Працює правильно з single-file
3. ✅ **Чистота проєкту**: Не захаращуємо .csproj порожніми папками
4. ✅ **Гнучкість**: Легко змінити розташування БД
5. ✅ **Діагностика**: Логування процесу створення папок
6. ✅ **Безпека**: Перевірка прав доступу перед створенням

## Ризики та мітігація

### Ризик 1: Втрата існуючих даних при міграції

**Мітігація**:
- Перевірити існування папок перед створенням
- Не перезаписувати існуючі файли
- Створити backup перед першим запуском нової версії

### Ризик 2: Відмінності в поведінці Debug vs Release

**Мітігація**:
- Тестувати на обох конфігураціях
- Використовувати однакові шляхи в Debug та Release
- Додати unit-тести для перевірки створення папок

### Ризик 3: Проблеми з правами доступу

**Мітігація**:
- Обробляти всі exception при створенні папок
- Надавати fallback варіанти
- Чітко інформувати користувача про проблему

## Чеклист виконання

- [x] Створити `DatabaseFolderInitializer.cs` ✅
- [x] Оновити `DatabaseConfig.cs` з методом `EnsureDirectoriesExist()` ✅
- [x] Інтегрувати ініціалізацію в `App.axaml.cs` ✅
- [x] Видалити `<Folder>` записи з `.csproj` ✅
- [x] Додати логування створення папок ✅
- [x] Додати обробку помилок ✅
- [ ] Написати unit-тести
- [x] Протестувати Debug build ✅ (успішно скомпільовано)
- [ ] Протестувати Release build (потрібно закрити запущений додаток)
- [ ] Протестувати single-file publish
- [ ] Протестувати на чистій системі
- [ ] Оновити документацію
- [ ] Code review
- [ ] Commit та deploy

## Очікувані результати

Після виконання міграції:
1. Додаток автоматично створює всі необхідні папки при першому запуску
2. Single-file publish працює коректно без ручного створення папок
3. Код стає чистішим та більш підтримуваним
4. Легше додавати нові папки БД в майбутньому
5. Покращена діагностика проблем зі створенням папок

## Тайминг

- **Етап 1-2**: 1 година (створення сервісів)
- **Етап 3-4**: 30 хвилин (інтеграція)
- **Етап 5**: 5 хвилин (видалення з .csproj)
- **Етап 6**: 1 година (тестування)
- **Етап 7**: 30 хвилин (документація)

**Загальний час**: ~3 години

## Пріоритет

**ВИСОКИЙ** - Критично для коректної роботи single-file publish та портативності додатка.

---

*Документ створено: 21 жовтня 2025*
*Автор: GitHub Copilot*

