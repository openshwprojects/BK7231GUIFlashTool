param(
    [string]$FlasherAssembly,
    [string]$MSBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $FlasherAssembly) {
    $candidates = @(
        (Join-Path $repo 'BK7231Flasher/bin/Release/BK7231Flasher.exe'),
        (Join-Path $repo 'BK7231Flasher/BuildOutput/Release/BK7231Flasher.exe')
    )
    $FlasherAssembly = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $FlasherAssembly -or -not (Test-Path -LiteralPath $FlasherAssembly)) {
    throw 'Build BK7231Flasher first, or provide -FlasherAssembly with its EXE path.'
}
$FlasherAssembly = (Resolve-Path -LiteralPath $FlasherAssembly).Path
if (-not $MSBuild) {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) { $MSBuild = $command.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        if (Test-Path -LiteralPath $vswhere) {
            $MSBuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        }
    }
}
if (-not $MSBuild) { throw 'MSBuild was not found. Install the Visual Studio .NET desktop build tools.' }
& $MSBuild (Join-Path $PSScriptRoot 'ali/ALIM3801SelfTests.csproj') /nologo /verbosity:minimal /p:Configuration=Release "/p:FlasherAssembly=$FlasherAssembly"
if ($LASTEXITCODE -ne 0) { throw "ALi selftest build failed ($LASTEXITCODE)." }
& (Join-Path $PSScriptRoot 'ali/bin/Release/ALIM3801SelfTests.exe')
if ($LASTEXITCODE -ne 0) { throw "ALi selftests failed ($LASTEXITCODE)." }
