<#
.SYNOPSIS
    Builds USBDongle_AudioSwitch without requiring Visual Studio or the .NET SDK.

.DESCRIPTION
    Looks for an MSBuild in this order:
      1. -MSBuildPath if supplied
      2. A Visual Studio / Build Tools install (via vswhere)
      3. MSBuild on PATH
      4. The MSBuild bundled with a scoop-installed JetBrains Rider

    A .NET Framework 4.8 *targeting pack* is normally required to build a v4.8
    project. It ships with the Windows SDK, which many machines lack even though
    the 4.8 runtime is present and the program runs fine. When the targeting pack
    is missing this script passes FrameworkPathOverride so MSBuild compiles
    against the installed runtime assemblies instead.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build.ps1
    powershell -ExecutionPolicy Bypass -File build.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$MSBuildPath,

    [switch]$NoFrameworkPathOverride
)

$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$projectFile = Join-Path $projectDir 'USBDongle_AudioSwitch.csproj'

if (-not (Test-Path $projectFile)) {
    throw "找不到项目文件：$projectFile"
}

function Find-MSBuild {
    param([string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path $Explicit)) { throw "指定的 MSBuild 不存在：$Explicit" }
        return $Explicit
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
                           -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }

    $onPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    # JetBrains Rider ships a self-contained MSBuild + Roslyn toolchain.
    $riderRoots = @(
        (Join-Path $env:USERPROFILE 'scoop\apps\rider\current\IDE\tools\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path $env:LOCALAPPDATA 'JetBrains\Toolbox\apps\Rider\ch-0\*\tools\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path ${env:ProgramFiles} 'JetBrains\*\tools\MSBuild\Current\Bin\MSBuild.exe')
    )
    foreach ($root in $riderRoots) {
        $match = Get-ChildItem $root -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($match) { return $match.FullName }
    }

    throw @'
未找到 MSBuild。请安装 Visual Studio / Build Tools，或使用 -MSBuildPath 显式指定。
'@
}

function Test-TargetingPack {
    param([string]$Version = 'v4.8')

    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} "Reference Assemblies\Microsoft\Framework\.NETFramework\$Version"),
        (Join-Path ${env:ProgramFiles} "Reference Assemblies\Microsoft\Framework\.NETFramework\$Version"),
        (Join-Path $env:WINDIR "Microsoft.NET\Framework64\$Version")
    )
    foreach ($root in $roots) {
        if (Test-Path (Join-Path $root 'System.Windows.Forms.dll')) { return $true }
    }
    return $false
}

$msbuild = Find-MSBuild -Explicit $MSBuildPath
Write-Host "MSBuild : $msbuild"

$arguments = @(
    $projectFile
    '/nologo'
    '/v:minimal'
    "/p:Configuration=$Configuration"
    "/p:Platform=AnyCPU"
)

$hasTargetingPack = Test-TargetingPack
if ($hasTargetingPack) {
    Write-Host "目标包  : 已安装"
}
elseif ($NoFrameworkPathOverride) {
    Write-Host "目标包  : 缺失（已按 -NoFrameworkPathOverride 跳过回退）" -ForegroundColor Yellow
}
else {
    $runtimeDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    if (-not (Test-Path $runtimeDir)) {
        $runtimeDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    }
    Write-Host "目标包  : 未安装，改用运行时程序集：$runtimeDir" -ForegroundColor Yellow
    $arguments += "/p:FrameworkPathOverride=$runtimeDir"
}

Write-Host "配置    : $Configuration"
Write-Host ''

& $msbuild @arguments
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    throw "编译失败，MSBuild 退出码 $exitCode"
}

$output = Join-Path $projectDir "bin\$Configuration\USBDongle_AudioSwitch.exe"
Write-Host ''
Write-Host "生成成功：$output" -ForegroundColor Green
Write-Host "运行：& '$output'"
