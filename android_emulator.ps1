param(
    [ValidateSet("install", "launch", "install-and-launch")]
    [string]$Action = "install-and-launch",
    [string]$Device = "emulator-5554"
)

$adb = Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
$apk = Join-Path $PSScriptRoot "Builds\Android\Scene1Only.apk"
$packageName = "com.kailius.gauntlet11"

if (-not (Test-Path -LiteralPath $adb)) {
    throw "ADB not found at '$adb'. Check the Android SDK installation."
}

if ($Action -ne "launch" -and -not (Test-Path -LiteralPath $apk)) {
    throw "APK not found at '$apk'. Build the Kailius playtest APK in Unity first."
}

$devices = & $adb devices
if ($LASTEXITCODE -ne 0) {
    throw "ADB could not list devices."
}

if ($devices -notmatch "(?m)^$([regex]::Escape($Device))\s+device$") {
    & $adb devices -l
    throw "Device '$Device' is not connected and authorized. Start the emulator, then retry."
}

if ($Action -ne "launch") {
    & $adb -s $Device install -r $apk
    if ($LASTEXITCODE -ne 0) {
        throw "APK installation failed."
    }
}

if ($Action -ne "install") {
    & $adb -s $Device shell monkey -p $packageName 1
    if ($LASTEXITCODE -ne 0) {
        throw "Game launch failed."
    }
}
