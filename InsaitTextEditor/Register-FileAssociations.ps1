# Register-FileAssociations.ps1
# PowerShell скрипт для реєстрації InsaitTextEditor як програми за замовчуванням для текстових файлів
# Запустіть з правами адміністратора: powershell -ExecutionPolicy Bypass -File Register-FileAssociations.ps1

param(
    [Parameter(Mandatory=$false)]
    [string]$ExePath = ""
)

# Знайти шлях до exe якщо не вказано
if ([string]::IsNullOrEmpty($ExePath)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $possiblePaths = @(
        (Join-Path $scriptDir "InsaitTextEditor.exe"),
        (Join-Path $scriptDir "bin\Release\net10.0\win-x64\publish\InsaitTextEditor.exe"),
        (Join-Path $scriptDir "bin\Release\net10.0\win-x64\InsaitTextEditor.exe"),
        (Join-Path $scriptDir "bin\Debug\net10.0\win-x64\InsaitTextEditor.exe")
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $ExePath = $path
            break
        }
    }
}

if ([string]::IsNullOrEmpty($ExePath) -or !(Test-Path $ExePath)) {
    Write-Host "ERROR: InsaitTextEditor.exe not found!" -ForegroundColor Red
    Write-Host "Please provide the path to InsaitTextEditor.exe as a parameter:"
    Write-Host "  .\Register-FileAssociations.ps1 -ExePath 'C:\path\to\InsaitTextEditor.exe'"
    exit 1
}

$ExePath = (Resolve-Path $ExePath).Path
Write-Host "Using executable: $ExePath" -ForegroundColor Cyan

# Перевірка прав адміністратора
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if (-not $isAdmin) {
    Write-Host "WARNING: Running without administrator privileges." -ForegroundColor Yellow
    Write-Host "File associations will be registered for current user only." -ForegroundColor Yellow
    $regBase = "HKCU:"
} else {
    $regBase = "HKLM:"
}

# Назва програми та ProgID
$appName = "InsaitTextEditor"
$progId = "InsaitTextEditor.TextFile"
$appDescription = "Insait Text Editor"

# Розширення файлів для реєстрації
$extensions = @(
    ".txt",    # Plain text
    ".log",    # Log files
    ".md",     # Markdown
    ".markdown",
    ".json",   # JSON
    ".xml",    # XML
    ".yaml",   # YAML
    ".yml",
    ".ini",    # INI files
    ".cfg",    # Config files
    ".conf",
    ".config",
    ".csv",    # CSV files
    ".tsv",    # Tab-separated values
    ".htm",    # HTML
    ".html",
    ".css",    # CSS
    ".js",     # JavaScript
    ".ts",     # TypeScript
    ".cs",     # C#
    ".py",     # Python
    ".java",   # Java
    ".cpp",    # C++
    ".c",      # C
    ".h",      # Header files
    ".hpp",
    ".sql",    # SQL
    ".sh",     # Shell scripts
    ".bat",    # Batch files
    ".ps1",    # PowerShell
    ".gitignore",
    ".env"     # Environment files
)

Write-Host "`nRegistering file associations..." -ForegroundColor Green

# 1. Створити ProgID
$progIdPath = "$regBase\SOFTWARE\Classes\$progId"
Write-Host "Creating ProgID: $progIdPath"

if (!(Test-Path $progIdPath)) {
    New-Item -Path $progIdPath -Force | Out-Null
}
Set-ItemProperty -Path $progIdPath -Name "(Default)" -Value $appDescription

# Створити команду відкриття
$shellPath = "$progIdPath\shell\open\command"
if (!(Test-Path $shellPath)) {
    New-Item -Path $shellPath -Force | Out-Null
}
Set-ItemProperty -Path $shellPath -Name "(Default)" -Value "`"$ExePath`" `"%1`""

# Встановити іконку
$iconPath = "$progIdPath\DefaultIcon"
if (!(Test-Path $iconPath)) {
    New-Item -Path $iconPath -Force | Out-Null
}
Set-ItemProperty -Path $iconPath -Name "(Default)" -Value "`"$ExePath`",0"

# 2. Зареєструвати розширення
foreach ($ext in $extensions) {
    $extPath = "$regBase\SOFTWARE\Classes\$ext"
    Write-Host "  Registering extension: $ext"
    
    if (!(Test-Path $extPath)) {
        New-Item -Path $extPath -Force | Out-Null
    }
    
    # Встановити ProgID для розширення
    Set-ItemProperty -Path $extPath -Name "(Default)" -Value $progId
    
    # Додати OpenWithProgids
    $openWithPath = "$extPath\OpenWithProgids"
    if (!(Test-Path $openWithPath)) {
        New-Item -Path $openWithPath -Force | Out-Null
    }
    Set-ItemProperty -Path $openWithPath -Name $progId -Value ([byte[]]@()) -Type Binary
}

# 3. Зареєструвати програму в Applications
$appPath = "$regBase\SOFTWARE\Classes\Applications\InsaitTextEditor.exe"
if (!(Test-Path $appPath)) {
    New-Item -Path $appPath -Force | Out-Null
}

$appShellPath = "$appPath\shell\open\command"
if (!(Test-Path $appShellPath)) {
    New-Item -Path $appShellPath -Force | Out-Null
}
Set-ItemProperty -Path $appShellPath -Name "(Default)" -Value "`"$ExePath`" `"%1`""

# Підтримувані типи
$supportedPath = "$appPath\SupportedTypes"
if (!(Test-Path $supportedPath)) {
    New-Item -Path $supportedPath -Force | Out-Null
}
foreach ($ext in $extensions) {
    Set-ItemProperty -Path $supportedPath -Name $ext -Value ""
}

# 4. Зареєструвати в RegisteredApplications (для Default Programs)
if ($isAdmin) {
    $registeredAppsPath = "HKLM:\SOFTWARE\RegisteredApplications"
    if (!(Test-Path $registeredAppsPath)) {
        New-Item -Path $registeredAppsPath -Force | Out-Null
    }
    Set-ItemProperty -Path $registeredAppsPath -Name $appName -Value "SOFTWARE\$appName\Capabilities"
    
    # Створити Capabilities
    $capabilitiesPath = "HKLM:\SOFTWARE\$appName\Capabilities"
    if (!(Test-Path $capabilitiesPath)) {
        New-Item -Path $capabilitiesPath -Force | Out-Null
    }
    Set-ItemProperty -Path $capabilitiesPath -Name "ApplicationDescription" -Value $appDescription
    Set-ItemProperty -Path $capabilitiesPath -Name "ApplicationName" -Value $appName
    
    # FileAssociations
    $fileAssocPath = "$capabilitiesPath\FileAssociations"
    if (!(Test-Path $fileAssocPath)) {
        New-Item -Path $fileAssocPath -Force | Out-Null
    }
    foreach ($ext in $extensions) {
        Set-ItemProperty -Path $fileAssocPath -Name $ext -Value $progId
    }
}

# 5. Оновити кеш shell
Write-Host "`nNotifying shell of changes..." -ForegroundColor Cyan
# Це сповіщає Explorer про зміни в реєстрі
$code = @'
[System.Runtime.InteropServices.DllImport("shell32.dll")]
public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
'@
$shell = Add-Type -MemberDefinition $code -Name "Shell32" -Namespace "Win32" -PassThru
$shell::SHChangeNotify(0x8000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

Write-Host "`n=== Registration Complete ===" -ForegroundColor Green
Write-Host "InsaitTextEditor has been registered for the following file types:" -ForegroundColor Cyan
$extensions | ForEach-Object { Write-Host "  $_" }

Write-Host "`nTo set InsaitTextEditor as the default app:" -ForegroundColor Yellow
Write-Host "1. Right-click on a text file"
Write-Host "2. Select 'Open with' -> 'Choose another app'"
Write-Host "3. Select 'Insait Text Editor'"
Write-Host "4. Check 'Always use this app to open .$ext files'"

Write-Host "`nOr use Windows Settings:" -ForegroundColor Yellow
Write-Host "Settings -> Apps -> Default apps -> InsaitTextEditor"

