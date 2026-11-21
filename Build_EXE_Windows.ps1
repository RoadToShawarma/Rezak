$ErrorActionPreference = 'Stop'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswhere) {
    $compilerSetup = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC\Auxiliary\Build\vcvars64.bat'
}
if (-not $compilerSetup) {
    throw 'Install the Visual Studio or Build Tools "Desktop development with C++" workload to build the launcher.'
}

$project = Join-Path $PSScriptRoot 'src\PosterMaster\PosterMaster.csproj'
$launcherSource = Join-Path $PSScriptRoot 'src\PosterMaster.Launcher\Launcher.c'
$launcherResource = Join-Path $PSScriptRoot 'src\PosterMaster.Launcher\Launcher.rc'
$iconDirectory = Split-Path -Parent $project
$artifacts = Join-Path $PSScriptRoot 'artifacts'
$distribution = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
if ((Get-Item -LiteralPath $artifacts).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'The artifacts directory must be inside the project.'
}

$stage = Join-Path $artifacts ('distribution-' + [guid]::NewGuid().ToString('N'))
$stagedDistribution = Join-Path $stage 'dist'
$previousDistribution = Join-Path $stage 'previous-dist'
$buildLock = [IO.File]::Open((Join-Path $artifacts 'distribution.lock'), 'OpenOrCreate', 'Write', 'None')
$installed = $false
try {
    New-Item -ItemType Directory -Path $stagedDistribution | Out-Null

    Push-Location -LiteralPath $stage
    try {
        $compileCommand = 'call "' + $compilerSetup + '" >nul && rc.exe /nologo /fo Launcher.res /i "' +
            $iconDirectory + '" "' + $launcherResource + '" && cl.exe /nologo /O2 /W4 /WX /MT /utf-8 "' +
            $launcherSource + '" Launcher.res /link /SUBSYSTEM:WINDOWS /OUT:dist\Rezak.exe user32.lib'
        & $env:ComSpec /d /c $compileCommand
        if ($LASTEXITCODE -ne 0) { throw 'The launcher failed to build.' }
    } finally {
        Pop-Location
    }

    dotnet publish $project -c Release -r win-x64 --self-contained true -o (Join-Path $stagedDistribution 'Data')
    if ($LASTEXITCODE -ne 0) { throw 'PosterMaster publish failed.' }

    if (Test-Path -LiteralPath $distribution) {
        $current = Get-Item -LiteralPath $distribution
        if (-not $current.PSIsContainer -or $current.FullName -ne $distribution -or
            ($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            (Get-ChildItem -LiteralPath $distribution -Recurse -Force -Attributes ReparsePoint | Select-Object -First 1)) {
            throw 'The existing dist directory must be a regular directory without links.'
        }
        [IO.Directory]::Move($distribution, $previousDistribution)
    }
    try {
        [IO.Directory]::Move($stagedDistribution, $distribution)
        $installed = $true
    } catch {
        if (Test-Path -LiteralPath $previousDistribution) {
            [IO.Directory]::Move($previousDistribution, $distribution)
        }
        throw
    }
    Write-Output ('Published: "' + (Join-Path $distribution 'Rezak.exe') + '"')
} finally {
    try {
        if (($installed -or -not (Test-Path -LiteralPath $previousDistribution)) -and
            (Test-Path -LiteralPath $stage) -and (Resolve-Path -LiteralPath $stage).ProviderPath -eq $stage -and
            -not ((Get-Item -LiteralPath $stage).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            Remove-Item -LiteralPath $stage -Recurse -Force
        }
    } finally {
        $buildLock.Dispose()
    }
}
