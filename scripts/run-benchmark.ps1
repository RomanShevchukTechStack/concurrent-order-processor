<#
.SYNOPSIS
  Runs each mode in a fresh process against a fresh API and appends results to benchmarks/.

.EXAMPLE
  ./scripts/run-benchmark.ps1 -Modes sync,async -Runs 3 -DelayMs 5
#>
param(
    [string[]] $Modes = @('sync', 'async'),
    [int] $Runs = 3,
    [int] $Rows = 5000,
    [int] $DelayMs = 5,
    [double] $ErrorRate = 0,
    [int] $Seed = 42,
    [int] $Port = 5080
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet build -c Release -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$apiExe = Join-Path $root 'src/OrderProcessor.Api/bin/Release/net8.0/OrderProcessor.Api.exe'
$appDll = Join-Path $root 'src/OrderProcessor.App/bin/Release/net8.0/OrderProcessor.App.dll'
$orders = Join-Path $root 'data/orders.csv'

# Same input for every run: regenerate with the fixed seed.
dotnet $appDll generate --rows $Rows --seed $Seed --output $orders | Out-Null

foreach ($mode in $Modes) {
    foreach ($run in 1..$Runs) {
        Write-Host "== $mode, run $run of $Runs"

        # Fresh API each time: failures depend on per-product attempt counts held in memory.
        $apiArgs = @(
            '--urls', "http://localhost:$Port",
            "--Simulation:DelayMs=$DelayMs",
            "--Simulation:ErrorRate=$($ErrorRate.ToString([cultureinfo]::InvariantCulture))",
            "--Simulation:Seed=$Seed"
        )
        $api = Start-Process $apiExe -ArgumentList $apiArgs -PassThru -WindowStyle Hidden
        try {
            $ready = $false
            foreach ($attempt in 1..50) {
                try {
                    Invoke-WebRequest "http://localhost:$Port/simulation" -UseBasicParsing | Out-Null
                    $ready = $true
                    break
                } catch { Start-Sleep -Milliseconds 200 }
            }
            if (-not $ready) { throw 'API did not start' }

            dotnet $appDll process --mode $mode --input $orders --api "http://localhost:$Port" `
                --report (Join-Path $root "data/report-$mode.txt") --measure true `
                --benchmarks-dir (Join-Path $root 'benchmarks') | Select-String 'Measured|Saved'
            if ($LASTEXITCODE -ne 0) { throw "Run failed: $mode" }
        }
        finally {
            Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "Results: $(Join-Path $root 'benchmarks/results.csv')"
