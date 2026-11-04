<#
.SYNOPSIS
    Builds the Linux Dedicated Server for Railbound and containers it into Docker.
.DESCRIPTION
    1. Triggers Unity headless build via batchmode (or verifies existing Linux build).
    2. Verifies Docker engine daemon status.
    3. Builds the Docker image 'railbound-dedicated-server:latest'.
    4. Starts container via Docker Compose.
#>

param (
    [switch]$SkipUnityBuild = $false,
    [switch]$RunContainer = $true,
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\2023.1.22f1\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"
$ProjectPath = (Get-Location).Path
$LinuxBinary = Join-Path $ProjectPath "Builds\Server\Linux\RailboundServer.x86_64"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   RAILBOUND DEDICATED SERVER BUILD & DOCKERIZATION       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Unity Build Stage
if (-not $SkipUnityBuild) {
    if (-not (Test-Path $UnityPath)) {
        Write-Warning "Unity executable not found at '$UnityPath'."
        if (Test-Path $LinuxBinary) {
            Write-Host "Found existing Linux server build at '$LinuxBinary'. Proceeding with existing build..." -ForegroundColor Yellow
        } else {
            Write-Error "Cannot build Linux server automatically because Unity.exe path is invalid, and no pre-built server exists. Please build via Unity Editor menu: 'Tools > Railbound > Netcode > Build Linux Dedicated Server'."
            exit 1
        }
    } else {
        Write-Host "[1/3] Building Linux Dedicated Server via Unity batchmode..." -ForegroundColor Green
        Write-Host "Executing NetcodeServerBuildPipeline.BuildLinuxServer..." -ForegroundColor Gray
        
        $buildArgs = @(
            "-batchmode",
            "-quit",
            "-projectPath", "`"$ProjectPath`"",
            "-executeMethod", "Railbound.Editor.NetcodeServerBuildPipeline.BuildLinuxServer",
            "-logFile", "`"$ProjectPath\Builds\server_build.log`""
        )
        
        $process = Start-Process -FilePath $UnityPath -ArgumentList $buildArgs -Wait -NoNewWindow -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path $LinuxBinary)) {
            Write-Error "Unity build failed with exit code $($process.ExitCode). Check log at: $ProjectPath\Builds\server_build.log"
            exit 1
        }
        Write-Host "Unity Linux Dedicated Server successfully built at: $LinuxBinary" -ForegroundColor Green
    }
} else {
    Write-Host "[1/3] Skipping Unity build stage (using existing build at $LinuxBinary)..." -ForegroundColor Yellow
}

# 2. Docker Daemon Check
Write-Host "[2/3] Checking Docker daemon status..." -ForegroundColor Green
try {
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Docker engine not running"
    }
} catch {
    Write-Warning "Docker Desktop is not currently running. Starting Docker Desktop..."
    $dockerDesktopExe = "C:\Users\kushan\AppData\Local\Programs\DockerDesktop\Docker Desktop.exe"
    if (Test-Path $dockerDesktopExe) {
        Start-Process -FilePath $dockerDesktopExe
        Write-Host "Waiting 20 seconds for Docker daemon to initialize..." -ForegroundColor Gray
        Start-Sleep -Seconds 20
    } else {
        Write-Error "Please start Docker Desktop and re-run this script."
        exit 1
    }
}

# 3. Docker Build Stage
Write-Host "[3/3] Building Docker image 'railbound-dedicated-server:latest'..." -ForegroundColor Green
docker build -t railbound-dedicated-server:latest .

if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker build failed."
    exit 1
}

Write-Host "Docker image successfully built!" -ForegroundColor Green

# 4. Optional Run Stage
if ($RunContainer) {
    Write-Host "Starting container via docker compose..." -ForegroundColor Green
    docker compose down 2>$null
    docker compose up -d
    Write-Host "Dedicated Server is now running on port 7777/udp!" -ForegroundColor Green
    Write-Host "View server logs with: docker compose logs -f" -ForegroundColor Cyan
}

Write-Host "`nAll operations completed successfully!" -ForegroundColor Cyan

