param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
foreach ($rid in @("osx-x64", "osx-arm64")) {
  $root = Join-Path $repo "dist/macos/$rid"; $publish = Join-Path $root "publish"; $app = Join-Path $root "Keyboard Screen Studio.app"
  if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app -Recurse -Force }
  $macos = Join-Path $app "Contents/MacOS"; $resources = Join-Path $app "Contents/Resources"
  New-Item -ItemType Directory -Path $macos,$resources -Force | Out-Null
  Copy-Item -Path (Join-Path $publish "*") -Destination $macos -Recurse -Force
  $icon = Join-Path $repo "src/KeyboardScreen.App.Avalonia/Assets/AppIcon.png"; if (Test-Path $icon) { Copy-Item $icon (Join-Path $resources "AppIcon.png") -Force }
  $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleDevelopmentRegion</key><string>zh_CN</string><key>CFBundleDisplayName</key><string>Keyboard Screen Studio</string>
<key>CFBundleExecutable</key><string>KeyboardScreen.App.Avalonia</string><key>CFBundleIdentifier</key><string>io.github.zcat95.keyboard-screen-studio</string>
<key>CFBundleInfoDictionaryVersion</key><string>6.0</string><key>CFBundleName</key><string>Keyboard Screen Studio</string>
<key>CFBundlePackageType</key><string>APPL</string><key>CFBundleShortVersionString</key><string>1.0.0</string><key>CFBundleVersion</key><string>1</string>
<key>CFBundleIconFile</key><string>AppIcon.png</string><key>LSMinimumSystemVersion</key><string>11.0</string><key>NSHighResolutionCapable</key><true/><key>NSPrincipalClass</key><string>NSApplication</string>
</dict></plist>
"@
  [IO.File]::WriteAllText((Join-Path $app "Contents/Info.plist"), $plist, [Text.UTF8Encoding]::new($false))
  $zipPath = Join-Path $repo "dist/macos/KSS-1.0.0-$rid-unsigned.zip"; if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
  Add-Type -AssemblyName System.IO.Compression
  $stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
  try { $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try { $base = Split-Path -Parent $app; Get-ChildItem -LiteralPath $app -Recurse -File | ForEach-Object {
      $relative = $_.FullName.Substring($base.Length + 1).Replace('\','/'); $entry = $zip.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
      $entry.ExternalAttributes = if ($_.Name -eq "KeyboardScreen.App.Avalonia" -or $_.Extension -eq ".dylib") { -2115174400 } else { -2119958528 }
      $input=[IO.File]::OpenRead($_.FullName);$output=$entry.Open();try{$input.CopyTo($output)}finally{$output.Dispose();$input.Dispose()}
    }} finally {$zip.Dispose()}
  } finally {$stream.Dispose()}
}
