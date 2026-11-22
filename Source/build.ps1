# RimWard build script — compiles Source\**\*.cs to Assemblies\RimWard.dll with Roslyn csc (no .NET SDK needed).
# Usage:  powershell -ExecutionPolicy Bypass -File Source\build.ps1
$ErrorActionPreference = 'Stop'
$modRoot = Split-Path $PSScriptRoot -Parent

$csc      = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$managed  = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"
$harmony  = "C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll"
# Optional refs (added only if present — keeps the build working as features land):
$prepatch = "C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2934420800\1.6\Assemblies\PrepatcherAPI.dll"
$mpapi    = "C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2992224079\1.6\Assemblies\0MultiplayerAPI.dll"

if (-not (Test-Path $csc))     { Write-Error "csc not found: $csc"; exit 1 }
if (-not (Test-Path $managed)) { Write-Error "Managed dir not found: $managed"; exit 1 }

# Reference list
$coreRefs = @(
  "mscorlib.dll","System.dll","System.Core.dll","System.Xml.dll","netstandard.dll",
  "Assembly-CSharp.dll",
  "UnityEngine.CoreModule.dll","UnityEngine.IMGUIModule.dll","UnityEngine.TextRenderingModule.dll","UnityEngine.InputLegacyModule.dll"
) | ForEach-Object { Join-Path $managed $_ }
$refs = @($coreRefs) + @($harmony)
foreach ($opt in @($prepatch,$mpapi)) { if (Test-Path $opt) { $refs += $opt } }

# Sources
$srcs = Get-ChildItem (Join-Path $modRoot "Source") -Recurse -Filter *.cs |
        Where-Object { $_.Name -ne 'build.ps1' } | ForEach-Object { $_.FullName }
if (-not $srcs) { Write-Error "No .cs sources found under Source\"; exit 1 }

# Response file (avoids quoting issues with spaces in paths)
$tmpDll = Join-Path $env:TEMP "RimWard_build.dll"
$rsp = Join-Path $env:TEMP "rimward.rsp"
$lines = @("/nologo","/target:library","/optimize+","/nostdlib+","/langversion:latest","/nowarn:1701,1702","/deterministic+","/out:`"$tmpDll`"")
$lines += ($refs | ForEach-Object { "/reference:`"$_`"" })
$lines += ($srcs | ForEach-Object { "`"$_`"" })
Set-Content -Path $rsp -Value $lines -Encoding UTF8

Write-Output ("Compiling {0} source file(s), {1} reference(s)..." -f $srcs.Count, $refs.Count)
& $csc "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Error "Compile FAILED (exit $LASTEXITCODE)"; exit 1 }
Write-Output "Compile OK -> $tmpDll"

# Deploy only if RimWorld is closed (it memory-maps the DLL while running)
$running = Get-Process RimWorldWin64 -ErrorAction SilentlyContinue
if ($running) {
  Write-Warning "RimWorld is RUNNING. DLL built to temp but NOT deployed. Close RimWorld, then re-run to deploy."
  exit 2
}
$outDir = Join-Path $modRoot "Assemblies"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Copy-Item $tmpDll (Join-Path $outDir "RimWard.dll") -Force
Write-Output ("Deployed -> {0}" -f (Join-Path $outDir "RimWard.dll"))
