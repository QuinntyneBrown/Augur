<#
.SYNOPSIS
    Regenerates the package-lock.json files that augur embeds for emitted Angular workspaces.

.DESCRIPTION
    An emitted Angular workspace must ship a package-lock.json that matches its package.json exactly, and augur must
    never run npm while emitting (L2-052, L2-058). Each combination of ui-library, state-management,
    server-side-rendering, and authentication has its own dependency set, so augur embeds 16 lockfiles.

    Run this script whenever NpmPackages in src/Augur.Emission.Angular/AngularModel.cs changes. It:
      1. builds augur with AugurBootstrapLockfiles=true, so it can emit workspaces before their lockfiles exist;
      2. emits one workspace per combination;
      3. runs `npm install --package-lock-only` in each, which resolves the lockfile without installing anything;
      4. replaces the project name with a placeholder, gzips the result, and writes it to
         src/Augur.Emission.Angular/Lockfiles/<variant>.json.gz.

    Commit the changed lockfiles, then run the slow suite (AUGUR_RUN_SLOW=1) to confirm `npm ci` works.

.EXAMPLE
    pwsh eng/Regenerate-AngularLockfiles.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Split-Path -Parent $PSScriptRoot
$work = Join-Path ([System.IO.Path]::GetTempPath()) "augur-lockfiles-$([guid]::NewGuid().ToString('N'))"
$augur = Join-Path $work 'augur'
$destination = Join-Path $repo 'src/Augur.Emission.Angular/Lockfiles'
$placeholder = '__AUGUR_PROJECT_NAME__'
$projectName = 'lockfile-template'

New-Item -ItemType Directory -Force -Path $work, $destination | Out-Null
try {
    Write-Host "Building augur in bootstrap mode..."
    dotnet build (Join-Path $repo 'src/Augur.Cli/Augur.Cli.csproj') -c Release -p:AugurBootstrapLockfiles=true -o $augur --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }

    foreach ($material in $true, $false) {
        foreach ($store in $true, $false) {
            foreach ($ssr in $true, $false) {
                foreach ($auth in $true, $false) {
                    $key = '{0}-{1}-{2}-{3}' -f ($(if ($material) { 'material' } else { 'plain' })),
                        ($(if ($store) { 'store' } else { 'signals' })),
                        ($(if ($ssr) { 'ssr' } else { 'csr' })),
                        ($(if ($auth) { 'auth' } else { 'anon' }))
                    $variant = Join-Path $work $key
                    New-Item -ItemType Directory -Force -Path $variant | Out-Null

                    $plan = [ordered]@{
                        planVersion    = 1
                        catalogVersion = 2
                        solutionName   = 'LockfileTemplate'
                        inputHash      = ('0' * 64)
                        decisions      = [ordered]@{
                            'target'                = @{ value = 'angular'; source = 'override' }
                            'authentication'        = @{ value = $auth; source = 'override' }
                            'ui-library'            = @{ value = $(if ($material) { 'angular-material' } else { 'none' }); source = 'override' }
                            'state-management'      = @{ value = $(if ($store) { 'ngrx-signal-store' } else { 'signals' }); source = 'override' }
                            'server-side-rendering' = @{ value = $ssr; source = 'override' }
                        }
                    }
                    $planPath = Join-Path $variant 'plan.json'
                    $plan | ConvertTo-Json -Depth 5 | Set-Content -Path $planPath -Encoding utf8NoBOM

                    $workspace = Join-Path $variant 'workspace'
                    dotnet (Join-Path $augur 'augur.dll') emit --plan $planPath --out $workspace --verbosity quiet
                    if ($LASTEXITCODE -ne 0) { throw "augur emit failed for $key" }

                    Write-Host "Resolving $key..."
                    Push-Location $workspace
                    try {
                        npm install --package-lock-only --ignore-scripts --no-audit --no-fund --loglevel=error
                        if ($LASTEXITCODE -ne 0) { throw "npm install failed for $key" }
                    }
                    finally {
                        Pop-Location
                    }

                    $lockfile = (Get-Content -Raw -Path (Join-Path $workspace 'package-lock.json')) -replace "`r`n", "`n"
                    $lockfile = $lockfile.Replace("""name"": ""$projectName""", """name"": ""$placeholder""")
                    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($lockfile)
                    $target = Join-Path $destination "$key.json.gz"
                    $file = [System.IO.File]::Create($target)
                    try {
                        $gzip = [System.IO.Compression.GZipStream]::new($file, [System.IO.Compression.CompressionLevel]::SmallestSize)
                        try { $gzip.Write($bytes, 0, $bytes.Length) } finally { $gzip.Dispose() }
                    }
                    finally {
                        $file.Dispose()
                    }
                }
            }
        }
    }

    Write-Host "Wrote 16 lockfiles to $destination"
}
finally {
    Remove-Item -Recurse -Force -Path $work -ErrorAction SilentlyContinue
}
