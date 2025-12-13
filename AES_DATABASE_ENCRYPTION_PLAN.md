# План впровадження AES шифрування та розділення баз даних

## 📋 Огляд

**Мета**: Впровадити повне AES-256 шифрування для всіх баз даних, розділити їх на окремі сховища, додати автоматичну ротацію/шардінг з обмеженням розміру до 1 ГБ, зберігаючи доступ до старих баз даних.

---

## 🎯 Основні вимоги

### 1. Шифрування
- ✅ AES-256 шифрування для всіх баз даних
- ✅ Автоматична генерація ключа шифрування (без введення користувачем)
- ✅ Безпечне зберігання ключа у захищеному місці системи
- ✅ Шифрування на рівні файлів LiteDB

### 2. Розділення баз даних
- ✅ **ChatHistory DB** - історія чату
- ✅ **Memory DB** - глобальна пам'ять (факти)
- ✅ **Reasoning DB** - reasoning chains
- ✅ **Documents DB** - документи користувача
- ✅ **Settings DB** - налаштування та метадані

### 3. Ротація та шардінг
- ✅ Обмеження розміру кожної БД до 1 ГБ
- ✅ Автоматична ротація при досягненні ліміту
- ✅ Збереження доступу до архівних БД
- ✅ Індексація та пошук по всіх шардах

---

## 🏗️ Нова структура проекту

### 📁 Повна структура з позначеннями

```
InsaitTextEditor/
│
├── Services/
│   │
│   ├── Database/                                 🆕 НОВА ПАПКА
│   │   │
│   │   ├── Core/                                 🆕 НОВА ПАПКА
│   │   │   ├── DatabaseServiceBase.cs           🆕 НОВИЙ - Базовий клас для всіх БД сервісів
│   │   │   ├── EncryptedDatabaseService.cs      🆕 НОВИЙ - Сервіс з AES шифруванням
│   │   │   ├── DatabaseEncryptionManager.cs     🆕 НОВИЙ - Менеджер ключів шифрування
│   │   │   ├── DatabaseShardManager.cs          🆕 НОВИЙ - Менеджер шардів/ротації
│   │   │   ├── ShardMetadata.cs                 🆕 НОВИЙ - Метадані шарда
│   │   │   └── DatabaseConfig.cs                🆕 НОВИЙ - Конфігурація БД
│   │   │
│   │   ├── Specialized/                          🆕 НОВА ПАПКА
│   │   │   ├── ChatHistoryDatabaseService.cs    🆕 НОВИЙ - Спеціалізований сервіс для чату
│   │   │   ├── MemoryDatabaseService.cs         🆕 НОВИЙ - Спеціалізований сервіс для пам'яті
│   │   │   ├── ReasoningDatabaseService.cs      🆕 НОВИЙ - Спеціалізований сервіс для reasoning
│   │   │   ├── DocumentsDatabaseService.cs      🆕 НОВИЙ - Спеціалізований сервіс для документів
│   │   │   └── SettingsDatabaseService.cs       🆕 НОВИЙ - Спеціалізований сервіс для налаштувань
│   │   │
│   │   ├── Sharding/                             🆕 НОВА ПАПКА
│   │   │   ├── ShardSelector.cs                 🆕 НОВИЙ - Логіка вибору шарда
│   │   │   ├── ShardRotationStrategy.cs         🆕 НОВИЙ - Стратегія ротації
│   │   │   ├── ShardQuery.cs                    🆕 НОВИЙ - Запити по всіх шардах
│   │   │   └── ShardArchiver.cs                 🆕 НОВИЙ - Архівування старих шардів
│   │   │
│   │   └── Migration/                            🆕 НОВА ПАПКА
│   │       ├── DatabaseMigrationService.cs      🆕 НОВИЙ - Міграція старих даних
│   │       └── EncryptionMigrationHelper.cs     🆕 НОВИЙ - Помічник для міграції з шифруванням
│   │
│   ├── ChatHistoryService.cs                    🔄 ОНОВИТИ - Використовувати новий ChatHistoryDatabaseService
│   │
│   ├── Memory/
│   │   ├── MemoryService.cs                     🔄 ОНОВИТИ - Використовувати новий MemoryDatabaseService
│   │   └── MemoryStorage.cs                     ❌ ВИДАЛИТИ - Замінюється на MemoryDatabaseService
│   │
│   ├── Reasoning/
│   │   ├── ReasoningService.cs                  🔄 ОНОВИТИ - Використовувати новий ReasoningDatabaseService
│   │   └── ReasoningChainStorage.cs             ❌ ВИДАЛИТИ - Замінюється на ReasoningDatabaseService
│   │
│   └── DatabaseService.cs                        ❌ ВИДАЛИТИ - Замінюється на DocumentsDatabaseService
│
├── Data/                                         🆕 НОВА ПАПКА - Замість старої Database/
│   │
│   ├── Encrypted/                                🆕 НОВА ПАПКА - Всі зашифровані БД тут
│   │   │
│   │   ├── ChatHistory/                          🆕 НОВА ПАПКА
│   │   │   ├── chat_0001.litedb                 🆕 НОВИЙ - Активний шард чату
│   │   │   ├── chat_0002.litedb                 🆕 НОВИЙ - Архівний шард (при ротації)
│   │   │   ├── chat_NNNN.litedb                 🆕 НОВИЙ - Додаткові шарди...
│   │   │   └── metadata.json                    🆕 НОВИЙ - Метадані шардів чату
│   │   │
│   │   ├── Memory/                               🆕 НОВА ПАПКА
│   │   │   ├── memory_0001.litedb               🆕 НОВИЙ - Активний шард пам'яті
│   │   │   ├── memory_NNNN.litedb               🆕 НОВИЙ - Додаткові шарди...
│   │   │   └── metadata.json                    🆕 НОВИЙ - Метадані шардів пам'яті
│   │   │
│   │   ├── Reasoning/                            🆕 НОВА ПАПКА
│   │   │   ├── reasoning_0001.litedb            🆕 НОВИЙ - Активний шард reasoning
│   │   │   ├── reasoning_NNNN.litedb            🆕 НОВИЙ - Додаткові шарди...
│   │   │   └── metadata.json                    🆕 НОВИЙ - Метадані шардів reasoning
│   │   │
│   │   ├── Documents/                            🆕 НОВА ПАПКА
│   │   │   ├── documents_0001.litedb            🆕 НОВИЙ - Активний шард документів
│   │   │   ├── documents_NNNN.litedb            🆕 НОВИЙ - Додаткові шарди...
│   │   │   └── metadata.json                    🆕 НОВИЙ - Метадані шардів документів
│   │   │
│   │   └── Settings/                             🆕 НОВА ПАПКА
│   │       └── settings.litedb                  🆕 НОВИЙ - Одна БД для налаштувань (не ротується)
│   │
│   └── Keys/                                     🆕 НОВА ПАПКА - Ключі шифрування
│       ├── master.key                           🆕 НОВИЙ - Головний ключ (зашифрований DPAPI)
│       └── database.keyring                     🆕 НОВИЙ - Кільце ключів для різних БД
│
├── Database/                                     ❌ ВИДАЛИТИ ПАПКУ - Після міграції
│   ├── documents.litedb                         ❌ ВИДАЛИТИ - Стара незашифрована БД
│   ├── chat.db                                  ❌ ВИДАЛИТИ - Стара незашифрована БД
│   └── README.txt                               ⚠️  ЗБЕРЕГТИ - Можна залишити
│
└── Models/
    │
    └── Database/                                 🆕 НОВА ПАПКА
        ├── ShardInfo.cs                         🆕 НОВИЙ - Інформація про шард
        ├── DatabaseMetrics.cs                   🆕 НОВИЙ - Метрики БД для моніторингу
        └── EncryptionKeyInfo.cs                 🆕 НОВИЙ - Інформація про ключ шифрування
```

---

### 📊 Легенда позначень

| Позначення | Значення | Опис |
|-----------|----------|------|
| 🆕 | **НОВИЙ** | Новий файл або папка, яку потрібно створити |
| 🔄 | **ОНОВИТИ** | Існуючий файл, який потрібно модифікувати |
| ❌ | **ВИДАЛИТИ** | Файл або папка для видалення після міграції |
| ⚠️ | **ЗБЕРЕГТИ** | Можна залишити без змін |

---

### 🗂️ Зміни в структурі - коротко

#### ✅ Що додається (🆕 НОВЕ):

**Папки:**
- `Services/Database/` - вся нова логіка баз даних
  - `Core/` - базові класи та менеджери
  - `Specialized/` - спеціалізовані сервіси для кожної БД
  - `Sharding/` - логіка шардінгу та ротації
  - `Migration/` - інструменти міграції
- `Data/` - замість старої `Database/`
  - `Encrypted/` - всі зашифровані БД
    - `ChatHistory/`, `Memory/`, `Reasoning/`, `Documents/`, `Settings/`
  - `Keys/` - ключі шифрування
- `Models/Database/` - моделі для роботи з БД

**Ключові файли:**
- `DatabaseEncryptionManager.cs` - управління AES ключами
- `DatabaseShardManager.cs` - ротація та шардінг
- `ChatHistoryDatabaseService.cs` - новий сервіс чату
- `MemoryDatabaseService.cs` - новий сервіс пам'яті
- `ReasoningDatabaseService.cs` - новий сервіс reasoning
- `DocumentsDatabaseService.cs` - новий сервіс документів
- `SettingsDatabaseService.cs` - новий сервіс налаштувань

#### 🔄 Що оновлюється:

- `ChatHistoryService.cs` - буде використовувати `ChatHistoryDatabaseService`
- `MemoryService.cs` - буде використовувати `MemoryDatabaseService`
- `ReasoningService.cs` - буде використовувати `ReasoningDatabaseService`
- `App.axaml.cs` - ініціалізація нових сервісів

#### ❌ Що видаляється після міграції:

**Файли:**
- `Services/DatabaseService.cs` - замінений на спеціалізовані сервіси
- `Services/Memory/MemoryStorage.cs` - замінений на `MemoryDatabaseService`
- `Services/Reasoning/ReasoningChainStorage.cs` - замінений на `ReasoningDatabaseService`

**Папка (після успішної міграції):**
- `Database/` - стара папка з незашифрованими БД
  - `documents.litedb` - стара БД документів
  - `chat.db` - стара БД чатів

---

### 📦 Міграція структури - покроково

#### Крок 1: Створити нову структуру
```
✅ Створити папку Data/
✅ Створити папку Data/Encrypted/
✅ Створити папку Data/Keys/
✅ Створити підпапки для кожної БД (ChatHistory, Memory, і т.д.)
✅ Створити папку Services/Database/ з підпапками
✅ Створити папку Models/Database/
```

#### Крок 2: Реалізувати нові компоненти
```
✅ Реалізувати всі файли в Services/Database/Core/
✅ Реалізувати всі файли в Services/Database/Specialized/
✅ Реалізувати всі файли в Services/Database/Sharding/
✅ Реалізувати всі файли в Services/Database/Migration/
✅ Реалізувати моделі в Models/Database/
```

#### Крок 3: Мігрувати дані
```
✅ Створити резервну копію Database/ → Database.backup/
✅ Згенерувати ключі шифрування → Data/Keys/
✅ Мігрувати chat.db → Data/Encrypted/ChatHistory/chat_0001.litedb
✅ Мігрувати documents.litedb → Data/Encrypted/Documents/documents_0001.litedb
✅ Мігрувати дані пам'яті → Data/Encrypted/Memory/memory_0001.litedb
✅ Мігрувати reasoning → Data/Encrypted/Reasoning/reasoning_0001.litedb
✅ Перевірити цілісність даних
```

#### Крок 4: Оновити існуючий код
```
✅ Оновити ChatHistoryService.cs
✅ Оновити MemoryService.cs
✅ Оновити ReasoningService.cs
✅ Оновити App.axaml.cs
✅ Протестувати всі функції
```

#### Крок 5: Очищення
```
✅ Видалити DatabaseService.cs
✅ Видалити MemoryStorage.cs
✅ Видалити ReasoningChainStorage.cs
✅ Видалити папку Database/ (після підтвердження)
```

---

### 🎯 Результат міграції

**До:**
```
Database/
├── documents.litedb  (1 файл, все разом, не шифроване)
└── chat.db           (1 файл, не шифроване)
```

**Після:**
```
Data/
├── Encrypted/
│   ├── ChatHistory/
│   │   ├── chat_0001.litedb  (зашифровано AES-256)
│   │   ├── chat_0002.litedb  (авто-ротація при 1GB)
│   │   └── metadata.json
│   ├── Memory/
│   │   ├── memory_0001.litedb  (зашифровано, окрема БД)
│   │   └── metadata.json
│   ├── Reasoning/
│   │   ├── reasoning_0001.litedb  (зашифровано, окрема БД)
│   │   └── metadata.json
│   ├── Documents/
│   │   ├── documents_0001.litedb  (зашифровано, окрема БД)
│   │   └── metadata.json
│   └── Settings/
│       └── settings.litedb  (зашифровано)
└── Keys/
    └── master.key  (зашифрований DPAPI)
```

---

## 🔐 Архітектура шифрування

### 1. Генерація та зберігання ключів

```csharp
// Автоматична генерація при першому запуску
1. Згенерувати 256-bit AES ключ (RandomNumberGenerator)
2. Зашифрувати ключ через Windows DPAPI (ProtectedData)
3. Зберегти у Data/Keys/master.key
4. Для кожної БД генерувати похідний ключ (PBKDF2)
```

### 2. Шифрування LiteDB

```csharp
// LiteDB підтримує AES encryption нативно
var connectionString = new ConnectionString
{
    Filename = dbPath,
    Password = encryptionKey,  // AES-256
    Connection = ConnectionType.Shared
};
```

### 3. Захист ключів

- **Windows**: DPAPI (Data Protection API)
- **Linux**: Keyring або файл з правами 600
- **macOS**: Keychain

---

## 📊 Механізм ротації та шардінгу

### Стратегія ротації

```
1. Моніторинг розміру активного шарда
2. При досягненні 950 МБ (буфер 50 МБ):
   - Створити новий шард
   - Позначити поточний як "read-only"
   - Перемкнути запис на новий шард
   - Оновити метадані
3. Старі шарди залишаються доступними для читання
```

### Нумерація шардів

```
chat_0001.litedb     # Перший шард (активний)
chat_0002.litedb     # Другий шард (архів)
chat_0003.litedb     # Третій шард (архів)
...
```

### Metadata.json структура

```json
{
  "database_type": "ChatHistory",
  "active_shard": 1,
  "shards": [
    {
      "shard_number": 1,
      "file_name": "chat_0001.litedb",
      "created_at": "2025-01-15T10:00:00Z",
      "size_bytes": 524288000,
      "is_active": true,
      "is_readonly": false,
      "encryption_key_id": "key_001",
      "record_count": 15420
    },
    {
      "shard_number": 2,
      "file_name": "chat_0002.litedb",
      "created_at": "2025-01-10T08:30:00Z",
      "size_bytes": 1073741824,
      "is_active": false,
      "is_readonly": true,
      "encryption_key_id": "key_001",
      "record_count": 28950
    }
  ],
  "max_shard_size_bytes": 1073741824,
  "total_records": 44370,
  "last_rotation": "2025-01-15T10:00:00Z"
}
```

---

## 🔄 Запити по всіх шардах

### Пошук по історії

```csharp
// Автоматичний пошук по всіх шардах (від нових до старих)
var results = await chatDbService.SearchAsync(query, limit: 50);

// Внутрішня логіка:
// 1. Шукати в активному шарді
// 2. Якщо не вистачає - шукати в архівних (від нових до старих)
// 3. Об'єднати результати
// 4. Відсортувати по Timestamp
```

### Агрегація даних

```csharp
// Підрахунок по всіх шардах
var totalMessages = await chatDbService.GetTotalCountAsync();

// Статистика по періоду (може охоплювати кілька шардів)
var stats = await chatDbService.GetStatsByPeriodAsync(startDate, endDate);
```

---

## 🚀 План впровадження (поетапно)

### Фаза 1: Підготовка (2-3 дні)

#### 1.1 Створення базової інфраструктури

- [ ] Створити папку `Services/Database/Core/`
- [ ] Реалізувати `DatabaseEncryptionManager.cs`
  - Генерація AES ключів
  - Зберігання через DPAPI
  - Завантаження ключів
- [ ] Реалізувати `DatabaseConfig.cs`
  - Налаштування шляхів
  - Ліміти розміру
  - Параметри шифрування
- [ ] Реалізувати `ShardMetadata.cs`
  - Модель метаданих
  - Серіалізація в JSON

#### 1.2 Тестування шифрування

- [ ] Юніт-тести для `DatabaseEncryptionManager`
- [ ] Перевірка генерації ключів
- [ ] Перевірка DPAPI захисту
- [ ] Тест створення зашифрованої LiteDB

---

### Фаза 2: Шардінг (3-4 дні)

#### 2.1 Менеджер шардів

- [ ] Реалізувати `DatabaseShardManager.cs`
  - Моніторинг розміру БД
  - Створення нових шардів
  - Завантаження метаданих
  - Збереження метаданих
- [ ] Реалізувати `ShardSelector.cs`
  - Вибір активного шарда для запису
  - Вибір шардів для читання
- [ ] Реалізувати `ShardRotationStrategy.cs`
  - Логіка ротації
  - Умови перемикання

#### 2.2 Запити по шардах

- [ ] Реалізувати `ShardQuery.cs`
  - Пошук по всіх шардах
  - Агрегація результатів
  - Пагінація
- [ ] Реалізувати `ShardArchiver.cs`
  - Архівування старих шардів
  - Компресія (опціонально)

---

### Фаза 3: Спеціалізовані сервіси (4-5 днів)

#### 3.1 Базовий клас

- [ ] Реалізувати `DatabaseServiceBase.cs`
  - Загальна логіка роботи з шардами
  - Методи для CRUD операцій
  - Інтеграція з `DatabaseEncryptionManager`
  - Інтеграція з `DatabaseShardManager`

#### 3.2 Спеціалізовані сервіси

- [ ] `ChatHistoryDatabaseService.cs`
  - Методи для історії чату
  - Пошук по повідомленнях
  - Фільтрація по датах
  
- [ ] `MemoryDatabaseService.cs`
  - Збереження фактів пам'яті
  - Пошук по тегах
  - Векторний пошук (embeddings)
  
- [ ] `ReasoningDatabaseService.cs`
  - Збереження reasoning chains
  - Пошук по conversation ID
  - Статистика reasoning
  
- [ ] `DocumentsDatabaseService.cs`
  - Збереження документів
  - Версіонування
  
- [ ] `SettingsDatabaseService.cs`
  - Налаштування (без ротації)
  - Інструкції користувача

---

### Фаза 4: Міграція (2-3 дні)

#### 4.1 Міграція даних

- [ ] Реалізувати `DatabaseMigrationService.cs`
  - Читання зі старих БД
  - Запис у нові зашифровані БД
  - Перевірка цілісності
  - Резервне копіювання

- [ ] Реалізувати `EncryptionMigrationHelper.cs`
  - Пакетна обробка записів
  - Progress reporting
  - Відновлення після помилок

#### 4.2 Сценарій міграції

```csharp
1. Створити резервну копію старих БД
2. Згенерувати ключі шифрування
3. Створити нову структуру папок
4. Мігрувати дані по частинах:
   - ChatHistory (chat.db → ChatHistory/chat_0001.litedb)
   - Memory (documents.litedb → Memory/memory_0001.litedb)
   - Reasoning (documents.litedb → Reasoning/reasoning_0001.litedb)
   - Documents (documents.litedb → Documents/documents_0001.litedb)
5. Перевірити цілісність
6. Оновити посилання в сервісах
7. Видалити старі БД (опціонально, після підтвердження)
```

---

### Фаза 5: Інтеграція (3-4 дні)

#### 5.1 Оновлення існуючих сервісів

- [ ] Оновити `ChatHistoryService.cs`
  - Використовувати `ChatHistoryDatabaseService`
  - Видалити пряму роботу з LiteDB
  
- [ ] Оновити `MemoryStorage.cs` → `MemoryDatabaseService`
  - Перенести логіку
  - Додати шардінг
  
- [ ] Оновити `ReasoningChainStorage.cs` → `ReasoningDatabaseService`
  - Перенести логіку
  - Додати шардінг
  
- [ ] Видалити старий `DatabaseService.cs`
  - Замінити на `DocumentsDatabaseService`

#### 5.2 Оновлення App.axaml.cs

- [ ] Зареєструвати нові сервіси
- [ ] Ініціалізувати ключі шифрування
- [ ] Перевірити міграцію при запуску

---

### Фаза 6: Тестування та оптимізація (3-4 дні)

#### 6.1 Тестування

- [ ] Юніт-тести для кожного сервісу
- [ ] Інтеграційні тести
- [ ] Тест ротації шардів
- [ ] Тест пошуку по архівних шардах
- [ ] Навантажувальне тестування
- [ ] Тест відновлення після помилок

#### 6.2 Оптимізація

- [ ] Оптимізація запитів по шардах
- [ ] Кешування метаданих
- [ ] Асинхронні операції
- [ ] Паралельний пошук по шардах

#### 6.3 Моніторинг

- [ ] Логування операцій
- [ ] Метрики продуктивності
- [ ] Алерти при помилках шифрування
- [ ] Дашборд розмірів БД

---

## 📝 Детальна реалізація компонентів

### 1. DatabaseEncryptionManager.cs

```csharp
public class DatabaseEncryptionManager
{
    private readonly string _keysDirectory;
    private readonly Dictionary<string, byte[]> _keyCache;
    
    // Генерація головного ключа
    public async Task<byte[]> GenerateMasterKeyAsync()
    {
        using var rng = RandomNumberGenerator.Create();
        var key = new byte[32]; // 256 bits
        rng.GetBytes(key);
        
        // Захист через DPAPI (Windows)
        var protectedKey = ProtectedData.Protect(key, 
            optionalEntropy: null, 
            DataProtectionScope.CurrentUser);
        
        await File.WriteAllBytesAsync(
            Path.Combine(_keysDirectory, "master.key"), 
            protectedKey);
        
        return key;
    }
    
    // Завантаження ключа
    public async Task<byte[]> LoadMasterKeyAsync()
    {
        var protectedKey = await File.ReadAllBytesAsync(
            Path.Combine(_keysDirectory, "master.key"));
        
        return ProtectedData.Unprotect(protectedKey, 
            optionalEntropy: null, 
            DataProtectionScope.CurrentUser);
    }
    
    // Похідний ключ для конкретної БД
    public byte[] DeriveKey(byte[] masterKey, string dbName)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            masterKey, 
            Encoding.UTF8.GetBytes(dbName), 
            iterations: 100000, 
            HashAlgorithmName.SHA256);
        
        return pbkdf2.GetBytes(32);
    }
    
    // Отримати ключ для БД (з кешем)
    public async Task<string> GetDatabasePasswordAsync(string dbName)
    {
        if (_keyCache.TryGetValue(dbName, out var cachedKey))
            return Convert.ToBase64String(cachedKey);
        
        var masterKey = await LoadMasterKeyAsync();
        var dbKey = DeriveKey(masterKey, dbName);
        
        _keyCache[dbName] = dbKey;
        return Convert.ToBase64String(dbKey);
    }
}
```

### 2. DatabaseShardManager.cs

```csharp
public class DatabaseShardManager
{
    private const long MaxShardSizeBytes = 1_073_741_824; // 1 GB
    private const long RotationThresholdBytes = 994_050_048; // 950 MB
    
    // Перевірка чи потрібна ротація
    public async Task<bool> ShouldRotateAsync(string dbPath)
    {
        var fileInfo = new FileInfo(dbPath);
        return fileInfo.Exists && fileInfo.Length >= RotationThresholdBytes;
    }
    
    // Створити новий шард
    public async Task<ShardInfo> CreateNewShardAsync(
        string dbDirectory, 
        string dbType, 
        int nextShardNumber)
    {
        var shardFileName = $"{dbType.ToLower()}_{nextShardNumber:D4}.litedb";
        var shardPath = Path.Combine(dbDirectory, shardFileName);
        
        var shardInfo = new ShardInfo
        {
            ShardNumber = nextShardNumber,
            FileName = shardFileName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsReadOnly = false
        };
        
        await UpdateMetadataAsync(dbDirectory, dbType, shardInfo);
        return shardInfo;
    }
    
    // Ротація шарда
    public async Task RotateShardAsync(
        string dbDirectory, 
        string dbType, 
        ShardInfo currentShard)
    {
        // Позначити поточний шард як readonly
        currentShard.IsActive = false;
        currentShard.IsReadOnly = true;
        
        // Створити новий шард
        var newShard = await CreateNewShardAsync(
            dbDirectory, 
            dbType, 
            currentShard.ShardNumber + 1);
        
        Console.WriteLine($"🔄 Ротація {dbType}: {currentShard.FileName} → {newShard.FileName}");
    }
    
    // Отримати всі шарди
    public async Task<List<ShardInfo>> GetAllShardsAsync(
        string dbDirectory, 
        string dbType)
    {
        var metadataPath = Path.Combine(dbDirectory, "metadata.json");
        if (!File.Exists(metadataPath))
            return new List<ShardInfo>();
        
        var json = await File.ReadAllTextAsync(metadataPath);
        var metadata = JsonSerializer.Deserialize<ShardMetadata>(json);
        return metadata?.Shards ?? new List<ShardInfo>();
    }
}
```

### 3. EncryptedDatabaseService.cs (Базовий)

```csharp
public abstract class EncryptedDatabaseService
{
    protected readonly DatabaseEncryptionManager EncryptionManager;
    protected readonly DatabaseShardManager ShardManager;
    protected readonly string DatabaseDirectory;
    protected readonly string DatabaseType;
    
    protected async Task<LiteDatabase> OpenActiveShardAsync()
    {
        var shards = await ShardManager.GetAllShardsAsync(
            DatabaseDirectory, DatabaseType);
        
        var activeShard = shards.FirstOrDefault(s => s.IsActive);
        if (activeShard == null)
        {
            activeShard = await ShardManager.CreateNewShardAsync(
                DatabaseDirectory, DatabaseType, 1);
        }
        
        // Перевірка ротації
        var shardPath = Path.Combine(DatabaseDirectory, activeShard.FileName);
        if (await ShardManager.ShouldRotateAsync(shardPath))
        {
            await ShardManager.RotateShardAsync(
                DatabaseDirectory, DatabaseType, activeShard);
            
            // Отримати новий активний шард
            shards = await ShardManager.GetAllShardsAsync(
                DatabaseDirectory, DatabaseType);
            activeShard = shards.First(s => s.IsActive);
            shardPath = Path.Combine(DatabaseDirectory, activeShard.FileName);
        }
        
        // Відкрити з шифруванням
        var password = await EncryptionManager.GetDatabasePasswordAsync(DatabaseType);
        var cs = new ConnectionString
        {
            Filename = shardPath,
            Password = password,
            Connection = ConnectionType.Shared
        };
        
        return new LiteDatabase(cs);
    }
    
    protected async Task<List<LiteDatabase>> OpenAllShardsAsync()
    {
        var shards = await ShardManager.GetAllShardsAsync(
            DatabaseDirectory, DatabaseType);
        
        var databases = new List<LiteDatabase>();
        var password = await EncryptionManager.GetDatabasePasswordAsync(DatabaseType);
        
        foreach (var shard in shards.OrderByDescending(s => s.ShardNumber))
        {
            var shardPath = Path.Combine(DatabaseDirectory, shard.FileName);
            if (File.Exists(shardPath))
            {
                var cs = new ConnectionString
                {
                    Filename = shardPath,
                    Password = password,
                    Connection = ConnectionType.Shared
                };
                databases.Add(new LiteDatabase(cs));
            }
        }
        
        return databases;
    }
}
```

### 4. ChatHistoryDatabaseService.cs (Приклад)

```csharp
public class ChatHistoryDatabaseService : EncryptedDatabaseService
{
    private const string MessagesCollection = "chat_messages";
    
    public ChatHistoryDatabaseService(
        DatabaseEncryptionManager encryptionManager,
        DatabaseShardManager shardManager,
        string dataDirectory)
        : base(encryptionManager, shardManager, 
               Path.Combine(dataDirectory, "Encrypted", "ChatHistory"), 
               "ChatHistory")
    {
    }
    
    // Додати повідомлення (завжди в активний шард)
    public async Task AddMessageAsync(ChatMessage message)
    {
        using var db = await OpenActiveShardAsync();
        var collection = db.GetCollection<ChatMessage>(MessagesCollection);
        collection.EnsureIndex(x => x.Timestamp);
        collection.Insert(message);
    }
    
    // Отримати останні повідомлення (пошук по всіх шардах)
    public async Task<List<ChatMessage>> GetRecentAsync(int limit = 100)
    {
        var allDatabases = await OpenAllShardsAsync();
        var results = new List<ChatMessage>();
        
        try
        {
            foreach (var db in allDatabases)
            {
                var collection = db.GetCollection<ChatMessage>(MessagesCollection);
                var messages = collection.Query()
                    .OrderByDescending(x => x.Timestamp)
                    .Limit(limit)
                    .ToList();
                
                results.AddRange(messages);
                
                if (results.Count >= limit)
                    break;
            }
            
            return results
                .OrderByDescending(x => x.Timestamp)
                .Take(limit)
                .ToList();
        }
        finally
        {
            foreach (var db in allDatabases)
                db.Dispose();
        }
    }
    
    // Пошук по всіх шардах
    public async Task<List<ChatMessage>> SearchAsync(string query, int limit = 50)
    {
        var allDatabases = await OpenAllShardsAsync();
        var results = new List<ChatMessage>();
        
        try
        {
            foreach (var db in allDatabases)
            {
                var collection = db.GetCollection<ChatMessage>(MessagesCollection);
                var messages = collection.Query()
                    .Where(x => x.Content.Contains(query))
                    .OrderByDescending(x => x.Timestamp)
                    .Limit(limit)
                    .ToList();
                
                results.AddRange(messages);
            }
            
            return results
                .OrderByDescending(x => x.Timestamp)
                .Take(limit)
                .ToList();
        }
        finally
        {
            foreach (var db in allDatabases)
                db.Dispose();
        }
    }
    
    // Очистити всі шарди
    public async Task ClearAllAsync()
    {
        var shards = await ShardManager.GetAllShardsAsync(
            DatabaseDirectory, DatabaseType);
        
        foreach (var shard in shards)
        {
            var shardPath = Path.Combine(DatabaseDirectory, shard.FileName);
            if (File.Exists(shardPath))
                File.Delete(shardPath);
        }
        
        // Очистити метадані
        var metadataPath = Path.Combine(DatabaseDirectory, "metadata.json");
        if (File.Exists(metadataPath))
            File.Delete(metadataPath);
    }
    
    // Отримати загальну кількість (по всіх шардах)
    public async Task<int> GetTotalCountAsync()
    {
        var allDatabases = await OpenAllShardsAsync();
        var totalCount = 0;
        
        try
        {
            foreach (var db in allDatabases)
            {
                var collection = db.GetCollection<ChatMessage>(MessagesCollection);
                totalCount += collection.Count();
            }
            
            return totalCount;
        }
        finally
        {
            foreach (var db in allDatabases)
                db.Dispose();
        }
    }
}
```

---

## 🔧 Оновлення App.axaml.cs

```csharp
public partial class App : Application
{
    // Нові сервіси
    public static DatabaseEncryptionManager EncryptionManager { get; private set; }
    public static DatabaseShardManager ShardManager { get; private set; }
    public static ChatHistoryDatabaseService ChatHistoryDb { get; private set; }
    public static MemoryDatabaseService MemoryDb { get; private set; }
    public static ReasoningDatabaseService ReasoningDb { get; private set; }
    public static DocumentsDatabaseService DocumentsDb { get; private set; }
    public static SettingsDatabaseService SettingsDb { get; private set; }
    
    public override async void OnFrameworkInitializationCompleted()
    {
        // Ініціалізація шифрування
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        
        EncryptionManager = new DatabaseEncryptionManager(
            Path.Combine(dataDirectory, "Keys"));
        
        // Згенерувати або завантажити ключ
        if (!await EncryptionManager.MasterKeyExistsAsync())
        {
            await EncryptionManager.GenerateMasterKeyAsync();
            Console.WriteLine("🔐 Згенеровано новий головний ключ шифрування");
        }
        
        ShardManager = new DatabaseShardManager();
        
        // Ініціалізація спеціалізованих сервісів
        ChatHistoryDb = new ChatHistoryDatabaseService(
            EncryptionManager, ShardManager, dataDirectory);
        
        MemoryDb = new MemoryDatabaseService(
            EncryptionManager, ShardManager, dataDirectory);
        
        ReasoningDb = new ReasoningDatabaseService(
            EncryptionManager, ShardManager, dataDirectory);
        
        DocumentsDb = new DocumentsDatabaseService(
            EncryptionManager, ShardManager, dataDirectory);
        
        SettingsDb = new SettingsDatabaseService(
            EncryptionManager, dataDirectory);
        
        // Міграція старих даних (якщо потрібно)
        await MigrateOldDatabasesIfNeededAsync();
        
        // ... інша ініціалізація
    }
    
    private async Task MigrateOldDatabasesIfNeededAsync()
    {
        var oldDbPath = Path.Combine(AppContext.BaseDirectory, "Database", "documents.litedb");
        var migrationService = new DatabaseMigrationService(
            EncryptionManager, 
            ShardManager);
        
        if (File.Exists(oldDbPath))
        {
            Console.WriteLine("📦 Виявлено стару базу даних. Початок міграції...");
            await migrationService.MigrateAsync(oldDbPath);
            Console.WriteLine("✅ Міграція завершена");
        }
    }
}
```

---

## 📊 Моніторинг та метрики

### Dashboard метрики

```csharp
public class DatabaseMetrics
{
    public string DatabaseType { get; set; }
    public int TotalShards { get; set; }
    public int ActiveShards { get; set; }
    public int ArchivedShards { get; set; }
    public long TotalSizeBytes { get; set; }
    public long ActiveShardSizeBytes { get; set; }
    public double RotationProgress { get; set; } // 0-100%
    public int TotalRecords { get; set; }
    public DateTime LastRotation { get; set; }
    public DateTime NextEstimatedRotation { get; set; }
}
```

### Логування

```csharp
- [DatabaseService] 🔐 Шифрування ініціалізовано
- [DatabaseService] 📊 ChatHistory: 3 шарди, 45,230 записів
- [DatabaseService] 🔄 Ротація ChatHistory: chat_0003.litedb → chat_0004.litedb
- [DatabaseService] 🔍 Пошук по 5 шардах: знайдено 127 результатів
- [DatabaseService] 💾 Збережено в активний шард: chat_0004.litedb
```

---

## ⚡ Оптимізації

### 1. Паралельний пошук

```csharp
public async Task<List<ChatMessage>> SearchParallelAsync(string query, int limit)
{
    var allDatabases = await OpenAllShardsAsync();
    
    var tasks = allDatabases.Select(db => Task.Run(() =>
    {
        var collection = db.GetCollection<ChatMessage>(MessagesCollection);
        return collection.Query()
            .Where(x => x.Content.Contains(query))
            .OrderByDescending(x => x.Timestamp)
            .Limit(limit)
            .ToList();
    })).ToArray();
    
    var results = await Task.WhenAll(tasks);
    
    return results
        .SelectMany(x => x)
        .OrderByDescending(x => x.Timestamp)
        .Take(limit)
        .ToList();
}
```

### 2. Кешування метаданих

```csharp
private static readonly MemoryCache _metadataCache = new MemoryCache(
    new MemoryCacheOptions { SizeLimit = 100 });

public async Task<ShardMetadata> GetMetadataAsync(string dbType)
{
    var cacheKey = $"metadata_{dbType}";
    
    if (_metadataCache.TryGetValue(cacheKey, out ShardMetadata cached))
        return cached;
    
    var metadata = await LoadMetadataFromDiskAsync(dbType);
    
    _metadataCache.Set(cacheKey, metadata, new MemoryCacheEntryOptions
    {
        Size = 1,
        SlidingExpiration = TimeSpan.FromMinutes(5)
    });
    
    return metadata;
}
```

### 3. Ледаче відкриття БД

```csharp
// Відкривати БД тільки коли потрібно
private readonly Lazy<Task<LiteDatabase>> _activeShard;

public EncryptedDatabaseService()
{
    _activeShard = new Lazy<Task<LiteDatabase>>(OpenActiveShardAsync);
}
```

---

## 🔒 Безпека

### Checklist безпеки

- ✅ AES-256 шифрування для всіх БД
- ✅ Ключі захищені DPAPI (Windows) або Keyring (Linux)
- ✅ Ключі НЕ зберігаються в відкритому вигляді
- ✅ Кожна БД має унікальний похідний ключ
- ✅ Логи НЕ містять ключі або паролі
- ✅ Автоматичне очищення ключів з пам'яті
- ✅ Файли ключів мають обмежені права (600 на Linux)
- ✅ Резервні копії також шифруються

### Процедура відновлення

```
1. Якщо master.key втрачений:
   - Дані незворотно втрачені (це особливість AES)
   - Рекомендація: регулярні експорти незашифрованих бекапів
   
2. Якщо master.key скомпрометований:
   - Згенерувати новий ключ
   - Ре-шифрування всіх БД
   - Міграція на нові шарди
```

---

## 📦 Необхідні NuGet пакети

```xml
<PackageReference Include="LiteDB" Version="5.0.17" />
<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
```

---

## 🎯 Очікувані результати

### Продуктивність

- ✅ Шифрування додає < 5% overhead
- ✅ Пошук по 10 шардах: < 500ms
- ✅ Запис: < 10ms (в активний шард)
- ✅ Ротація: < 100ms (асинхронна)

### Безпека

- ✅ Повне шифрування всіх даних
- ✅ Автоматичне управління ключами
- ✅ Захист від витоку даних з файлової системи

### Масштабованість

- ✅ Необмежена кількість шардів
- ✅ Кожен шард < 1 ГБ
- ✅ Автоматична ротація
- ✅ Доступ до історичних даних

---

## 📅 Загальний Timeline

| Фаза | Тривалість | Складність |
|------|-----------|-----------|
| 1. Підготовка | 2-3 дні | Середня |
| 2. Шардінг | 3-4 дні | Висока |
| 3. Спеціалізовані сервіси | 4-5 днів | Висока |
| 4. Міграція | 2-3 дні | Середня |
| 5. Інтеграція | 3-4 дні | Середня |
| 6. Тестування | 3-4 дні | Висока |
| **ВСЬОГО** | **17-23 дні** | **3-4 тижні** |

---

## 🚨 Ризики та міtigація

### Ризик 1: Втрата ключа шифрування
**Міtigація**: 
- Автоматичні експорти даних в незашифрованому вигляді
- Інструкція користувачу про backup папки Keys/

### Ризик 2: Помилка при міграції
**Міtigація**:
- Повна резервна копія перед міграцією
- Поетапна міграція з валідацією
- Rollback механізм

### Ризик 3: Деградація продуктивності
**Міtigація**:
- Кешування метаданих
- Паралельні запити
- Ледаче завантаження

### Ризик 4: Корупція шарда
**Міtigація**:
- LiteDB вбудована перевірка цілісності
- Автоматичні backup
- Ізоляція шардів (один пошкоджений не впливає на інші)

---

## ✅ Критерії успіху

1. ✅ Всі дані зашифровані AES-256
2. ✅ Жоден файл БД не перевищує 1 ГБ
3. ✅ Автоматична ротація працює безпомилково
4. ✅ Пошук по архівних даних працює швидко (< 500ms)
5. ✅ Міграція існуючих даних без втрат
6. ✅ Користувач НЕ вводить паролі вручну
7. ✅ Всі тести проходять (unit + integration)
8. ✅ Документація оновлена

---

## 📖 Наступні кроки

1. ✅ **Review** цього плану
2. ✅ **Затвердження** архітектури
3. ✅ **Створення** бренча для розробки
4. ✅ **Початок** імплементації з Фази 1
5. ✅ **Regular** code reviews та тестування
6. ✅ **Фінальне** тестування та деплой

---

**Автор плану**: GitHub Copilot  
**Дата**: 19 жовтня 2025  
**Версія**: 1.0  
**Статус**: ✅ Готово до імплементації

