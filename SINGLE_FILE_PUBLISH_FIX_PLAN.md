# План вирішення проблеми з публікацією у єдиному файлі

## Проблема

При спробі створити single-file публікацію виникає помилка NETSDK1152:
- **Причина**: LlamaSharp.Backend.CPU містить нативні DLL для 4 варіантів CPU (avx, avx2, avx512, noavx)
- **Конфлікт**: Всі варіанти мають однакові імена файлів (ggml-base.dll, ggml-cpu.dll, ggml.dll, llama.dll, mtmd.dll)
- **Результат**: MSBuild не може визначити, яку версію включити в single-file publish

## Поточний стан

У InsaitTextEditor.csproj вже є спроба вирішення через Target `PruneLlamaCpuVariants`, але:
1. Target виконується на етапі `ResolvePackageAssets` → `ResolveReferences`
2. Це **занадто рано** - файли для публікації визначаються пізніше
3. Помилка виникає на етапі `ComputeFilesToPublish` → `_HandleFileConflictsForPublish`

## Рішення

### Варіант 1: Виправлення існуючого Target (Рекомендовано)

**Переваги:**
- Мінімальні зміни в проекті
- Використовує вже наявний механізм
- Контроль над вибором CPU варіанту

**Кроки:**

1. **Додати новий Target для фільтрації файлів публікації**
   ```xml
   <Target Name="FilterLlamaNativesForPublish" 
           AfterTargets="_ComputeFilesToPublish" 
           BeforeTargets="_HandleFileConflictsForPublish">
     <ItemGroup>
       <!-- Видаляємо всі варіанти крім avx2 -->
       <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" 
         Condition="$(ResolvedFileToPublish.Identity.Contains('llamasharp.backend.cpu')) AND 
                    ($(ResolvedFileToPublish.Identity.Contains('\avx\')) OR 
                     $(ResolvedFileToPublish.Identity.Contains('\avx512\')) OR 
                     $(ResolvedFileToPublish.Identity.Contains('\noavx\')))" />
     </ItemGroup>
   </Target>
   ```

2. **Оновити PropertyGroup для Runtime Identifier**
   ```xml
   <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
     <RuntimeIdentifier>win-x64</RuntimeIdentifier>
     <PublishSingleFile>true</PublishSingleFile>
     <SelfContained>true</SelfContained>
     <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
     <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
   </PropertyGroup>
   ```

3. **Залишити існуючий PruneLlamaCpuVariants** для фільтрації при звичайній збірці

### Варіант 2: Використання ErrorOnDuplicatePublishOutputFiles

**Переваги:**
- Найпростіше рішення
- Не потребує фільтрації

**Недоліки:**
- Включає ВСІ варіанти нативних бібліотек
- Збільшує розмір публікації на ~100-200 MB
- Runtime вибирає потрібний варіант сам

**Кроки:**

1. **Вимкнути перевірку дублікатів**
   ```xml
   <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
     <ErrorOnDuplicatePublishOutputFiles>false</ErrorOnDuplicatePublishOutputFiles>
   </PropertyGroup>
   ```

2. **Додати Target для збереження структури папок**
   ```xml
   <Target Name="PreserveLlamaNativeStructure" 
           AfterTargets="_ComputeFilesToPublish">
     <ItemGroup>
       <ResolvedFileToPublish Update="@(ResolvedFileToPublish)" 
         Condition="$(ResolvedFileToPublish.Identity.Contains('llamasharp.backend.cpu'))">
         <RelativePath>runtimes/%(RecursiveDir)%(Filename)%(Extension)</RelativePath>
       </ResolvedFileToPublish>
     </ItemGroup>
   </Target>
   ```

### Варіант 3: Створення власного RuntimeConfig.json

**Переваги:**
- Точний контроль над включеними бібліотеками
- Можливість динамічного вибору CPU варіанту

**Кроки:**

1. **Створити runtimeconfig.template.json**
   ```json
   {
     "configProperties": {
       "System.Runtime.TieredCompilation": true,
       "System.GC.Server": true
     }
   }
   ```

2. **Додати MSBuild логіку для копіювання тільки avx2**

### Варіант 4: Використання Directory.Build.targets (Глобальне рішення)

**Переваги:**
- Рішення застосовується до всіх проектів у solution
- Централізоване управління

**Кроки:**

1. **Редагувати існуючий Directory.Build.targets**
   ```xml
   <Target Name="RemoveDuplicateLlamaNatives" 
           BeforeTargets="_HandleFileConflictsForPublish">
     <ItemGroup>
       <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" 
         Condition="$([System.String]::Copy('%(Identity)').Contains('llamasharp.backend.cpu\0.25.0\runtimes\win-x64\native\avx\'))" />
       <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" 
         Condition="$([System.String]::Copy('%(Identity)').Contains('llamasharp.backend.cpu\0.25.0\runtimes\win-x64\native\avx512\'))" />
       <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" 
         Condition="$([System.String]::Copy('%(Identity)').Contains('llamasharp.backend.cpu\0.25.0\runtimes\win-x64\native\noavx\'))" />
     </ItemGroup>
   </Target>
   ```

## Рекомендоване рішення

**Комбінація Варіанту 1 + Варіанту 4:**

1. Оновити `Directory.Build.targets` з правильним Target для публікації
2. Додати Runtime Identifier у Release конфігурацію
3. Зберегти існуючий `PruneLlamaCpuVariants` для звичайної збірки
4. Додати коментарі для майбутньої підтримки

## Етапи впровадження

### Крок 1: Резервне копіювання
```cmd
copy InsaitTextEditor\InsaitTextEditor.csproj InsaitTextEditor\InsaitTextEditor.csproj.backup
copy Directory.Build.targets Directory.Build.targets.backup
```

### Крок 2: Оновити Directory.Build.targets
- Додати Target `RemoveDuplicateLlamaNatives`
- Переконатися що він виконується **ДО** `_HandleFileConflictsForPublish`

### Крок 3: Оновити InsaitTextEditor.csproj
- Додати `RuntimeIdentifier` у Release PropertyGroup
- Залишити існуючі налаштування single-file

### Крок 4: Тестування
```cmd
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained
```

### Крок 5: Перевірка результату
- Переконатися що публікація успішна
- Перевірити розмір файлу (має бути ~150-200 MB)
- Запустити опубліковану версію
- Перевірити роботу LLamaSharp

### Крок 6: Верифікація CPU варіанту
- Додати логування при запуску для перевірки завантаженої версії GGML
- Переконатися що використовується avx2

## Альтернативні сценарії

### Якщо Варіант 1 не спрацює:

1. Спробувати Варіант 2 (відключення перевірки)
2. Якщо розмір критичний - використати Варіант 3
3. Як останній варіант - перейти на LLamaSharp.Backend.Cuda або інший backend

### Якщо потрібна підтримка різних CPU:

Створити окремі профілі публікації для кожного варіанту:
```xml
<PropertyGroup Condition="'$(CpuVariant)' == 'avx'">
  <LlamaPreferredCpuVariant>avx</LlamaPreferredCpuVariant>
</PropertyGroup>
```

Публікувати командами:
```cmd
dotnet publish -c Release -p:CpuVariant=avx2
dotnet publish -c Release -p:CpuVariant=avx
```

## Відомі обмеження

1. **AVX2 сумісність**: Випущений додаток буде працювати ТІЛЬКИ на CPU з підтримкою AVX2
   - Intel: Core 4-го покоління (Haswell, 2013+)
   - AMD: Excavator/Ryzen (2015+)

2. **Розмір файлу**: Single-file буде великим (~150+ MB) через:
   - .NET Runtime (~80 MB)
   - Avalonia UI (~20 MB)
   - LLamaSharp нативні бібліотеки (~30-40 MB)
   - SkiaSharp (~15 MB)

3. **Час запуску**: Перший запуск буде повільнішим через розпакування

## Моніторинг після впровадження

- [ ] Успішна публікація без помилок
- [ ] Розмір файлу прийнятний
- [ ] Додаток запускається
- [ ] LLamaSharp працює коректно
- [ ] Немає помилок завантаження нативних бібліотек
- [ ] Продуктивність не знизилась

## Додаткові оптимізації (опціонально)

### Після успішної публікації можна розглянути:

1. **R2R (ReadyToRun) компіляція**
   ```xml
   <PublishReadyToRun>true</PublishReadyToRun>
   ```

2. **Trimming специфічних assembly** (обережно!)
   ```xml
   <TrimMode>partial</TrimMode>
   <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
   ```

3. **Компресія** (вже включена)
   - Зменшує розмір на ~30%
   - Збільшує час запуску на ~200-300ms

## Контакти для підтримки

При виникненні проблем:
1. Перевірити build.log та app.log
2. Запустити з --verbosity detailed
3. Перевірити GitHub Issues для LLamaSharp

## Дата створення плану
19 жовтня 2025

## Статус
🔴 Не розпочато

