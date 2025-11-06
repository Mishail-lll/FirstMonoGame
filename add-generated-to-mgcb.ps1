param(
    [Parameter(Mandatory=$true)][string]$GeneratedDir,
    [Parameter(Mandatory=$true)][string]$MgcbFile,
    [string]$Platform = "DesktopGL"
)

# --- Helpers ---
function Write-ErrAndExit($msg, [int]$code = 1) {
    Write-Error $msg
    exit $code
}

# Проверки наличия
if (-not (Test-Path $MgcbFile)) {
    Write-ErrAndExit "Content.mgcb not found: $MgcbFile"
}
if (-not (Test-Path $GeneratedDir)) {
    Write-ErrAndExit "Generated dir not found: $GeneratedDir"
}

# Вычисляем Content root — папку, в которой лежит Content.mgcb (обычно ...\Content)
$mgcbFull = (Get-Item -LiteralPath $MgcbFile).FullName
$contentRoot = (Split-Path -Parent $mgcbFull)  # path to directory containing Content.mgcb

Write-Host "Content.mgcb: $mgcbFull"
Write-Host "Content root: $contentRoot"
Write-Host "Generated dir: $GeneratedDir"
Write-Host "Platform: $Platform"

# Находим PNG
$pngFiles = Get-ChildItem -LiteralPath $GeneratedDir -Recurse -File -Filter *.png | Sort-Object FullName
if ($pngFiles.Count -eq 0) {
    Write-Host "No PNG files found in $GeneratedDir. Nothing to do."
    exit 0
}

# Прочитаем mgcb в память для поиска существующих блоков
$mgcbText = Get-Content -LiteralPath $MgcbFile -Raw

foreach ($f in $pngFiles) {
    # Полный путь к файлу
    $full = $f.FullName

    # Делаем относительный путь относительно папки Content (contentRoot).
    # Если файл не внутри contentRoot — используем путь относительно Content папки имени (всё равно допустимо).
    if ($full.StartsWith($contentRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $rel = $full.Substring($contentRoot.Length + 1)  # без ведущего '\'
    } else {
        # Попробуем найти "Content\" в пути и резать после неё
        $idx = $full.IndexOf([IO.Path]::Combine("Content"), [System.StringComparison]::OrdinalIgnoreCase)
        if ($idx -ge 0) {
            $rel = $full.Substring($idx + ("Content").Length + 1)
        } else {
            # если не найдена — просто используем имя файла
            $rel = [IO.Path]::GetFileName($full)
        }
    }

    # Нормализуем слэши для mgcb — forward slashes
    $rel = $rel -replace '\\','/'

    $beginLine = "#begin $rel"

    if ($mgcbText -match [regex]::Escape($beginLine)) {
        Write-Host "Already exists in mgcb: $rel"
        continue
    }

    Write-Host "Adding to mgcb: $rel"

    $block = @"
#begin $rel
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyEnabled=false
/processorParam:GenerateMipmaps=true
/build:$rel
"@

    # Append block to the end of file
    Add-Content -LiteralPath $MgcbFile -Value $block

    # Update local cache string so duplicates in same run are visible
    $mgcbText += "`n" + $block
}

# Теперь rebuild content
Write-Host "Restoring dotnet tools (if any)..."
dotnet tool restore

Write-Host "Building content with dotnet mgcb..."
# Используем правильное quoting
$mgcbArg = "/@:`"$MgcbFile`""
$platformArg = "/platform:$Platform"
# Запускаем dotnet mgcb и пробрасываем вывод
$procInfo = New-Object System.Diagnostics.ProcessStartInfo
$procInfo.FileName = "dotnet"
$procInfo.Arguments = "mgcb $mgcbArg $platformArg"
$procInfo.RedirectStandardOutput = $true
$procInfo.RedirectStandardError = $true
$procInfo.UseShellExecute = $false

$proc = New-Object System.Diagnostics.Process
$proc.StartInfo = $procInfo
$proc.Start() | Out-Null
$stdout = $proc.StandardOutput.ReadToEnd()
$stderr = $proc.StandardError.ReadToEnd()
$proc.WaitForExit()

Write-Host $stdout
if ($proc.ExitCode -ne 0) {
    Write-Error "dotnet mgcb failed with exit code $($proc.ExitCode). Error output:"
    Write-Error $stderr
    exit $proc.ExitCode
}

Write-Host "Content built successfully."
exit 0
