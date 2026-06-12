param(
    [string]$AppPath = (Join-Path (Get-Location) 'src\WinDS9.Viewer\bin\Debug\net8.0-windows\WinDS9.Viewer.exe'),
    [string[]]$Extensions = @('.fits', '.fit', '.fts', '.evt'),
    [switch]$Unregister
)

$progId = 'WinDS9.NativeFile'

if ($Unregister) {
    foreach ($extension in $Extensions) {
        Remove-Item -LiteralPath "HKCU:\Software\Classes\$extension" -Recurse -Force -ErrorAction SilentlyContinue
    }

    Remove-Item -LiteralPath "HKCU:\Software\Classes\$progId" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'Removed WinDS9 native file associations for current user.'
    exit 0
}

if (!(Test-Path -LiteralPath $AppPath)) {
    throw "WinDS9 executable not found: $AppPath"
}

New-Item -Path "HKCU:\Software\Classes\$progId\shell\open\command" -Force | Out-Null
Set-Item -Path "HKCU:\Software\Classes\$progId" -Value 'WinDS9 native file'
Set-Item -Path "HKCU:\Software\Classes\$progId\shell\open\command" -Value "`"$AppPath`" `"%1`""

foreach ($extension in $Extensions) {
    New-Item -Path "HKCU:\Software\Classes\$extension" -Force | Out-Null
    Set-Item -Path "HKCU:\Software\Classes\$extension" -Value $progId
}

Write-Host "Registered WinDS9 native file associations for current user: $($Extensions -join ', ')"
