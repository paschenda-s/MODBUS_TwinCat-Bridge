# TwinBridge Administration CLI
param(
    [switch]$Status,
    [switch]$Start,
    [switch]$Stop,
    [switch]$Restart,
    [switch]$Logs,
    [switch]$Test,
    [switch]$Health,
    [switch]$Config,
    [int]$LogLines = 50,
    [string]$HealthPort = "8080"
)

$ServiceName = "TwinBridge"
$ErrorActionPreference = "Stop"

function Show-Help {
    Write-Host "TwinBridge Administration CLI" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  .\admin-cli.ps1 -Status          Show service status"
    Write-Host "  .\admin-cli.ps1 -Start           Start the service"
    Write-Host "  .\admin-cli.ps1 -Stop            Stop the service"
    Write-Host "  .\admin-cli.ps1 -Restart         Restart the service"
    Write-Host "  .\admin-cli.ps1 -Logs            Show recent event logs"
    Write-Host "  .\admin-cli.ps1 -Test            Test connections"
    Write-Host "  .\admin-cli.ps1 -Health          Check health endpoints"
    Write-Host "  .\admin-cli.ps1 -Config          Show current configuration"
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Cyan
    Write-Host "  -LogLines <number>               Number of log lines to show (default: 50)"
    Write-Host "  -HealthPort <port>               Health check port (default: 8080)"
}

function Get-ServiceStatus {
    Write-Host "Checking TwinBridge service status..." -ForegroundColor Yellow
    
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        
        if ($service) {
            Write-Host "Service Status: $($service.Status)" -ForegroundColor $(
                if ($service.Status -eq "Running") { "Green" } else { "Red" }
            )
            Write-Host "Start Type: $($service.StartType)" -ForegroundColor Gray
            
            if ($service.Status -eq "Running") {
                $process = Get-Process -Name "ModbusAdsBridge" -ErrorAction SilentlyContinue
                if ($process) {
                    $uptime = (Get-Date) - $process.StartTime
                    Write-Host "Uptime: $($uptime.ToString('dd\.hh\:mm\:ss'))" -ForegroundColor Green
                    Write-Host "Process ID: $($process.Id)" -ForegroundColor Gray
                    Write-Host "Memory Usage: $([math]::Round($process.WorkingSet64 / 1MB, 2)) MB" -ForegroundColor Gray
                }
            }
        } else {
            Write-Host "Service not found!" -ForegroundColor Red
        }
    } catch {
        Write-Host "Error checking service status: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Start-TwinBridgeService {
    Write-Host "Starting TwinBridge service..." -ForegroundColor Yellow
    
    try {
        Start-Service -Name $ServiceName
        Start-Sleep -Seconds 3
        Get-ServiceStatus
    } catch {
        Write-Host "Error starting service: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Stop-TwinBridgeService {
    Write-Host "Stopping TwinBridge service..." -ForegroundColor Yellow
    
    try {
        Stop-Service -Name $ServiceName -Force
        Write-Host "Service stopped successfully" -ForegroundColor Green
    } catch {
        Write-Host "Error stopping service: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Restart-TwinBridgeService {
    Write-Host "Restarting TwinBridge service..." -ForegroundColor Yellow
    
    try {
        Restart-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 3
        Get-ServiceStatus
    } catch {
        Write-Host "Error restarting service: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Show-EventLogs {
    Write-Host "Retrieving recent event logs..." -ForegroundColor Yellow
    
    try {
        $logs = Get-WinEvent -FilterHashtable @{
            LogName = 'Application'
            ProviderName = 'TwinBridge'
        } -MaxEvents $LogLines -ErrorAction SilentlyContinue
        
        if ($logs) {
            $logs | Sort-Object TimeCreated | ForEach-Object {
                $color = switch ($_.LevelDisplayName) {
                    "Error" { "Red" }
                    "Warning" { "Yellow" }
                    "Information" { "White" }
                    default { "Gray" }
                }
                
                Write-Host "$($_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss')) [$($_.LevelDisplayName)] $($_.Message)" -ForegroundColor $color
            }
        } else {
            Write-Host "No recent log entries found for TwinBridge" -ForegroundColor Yellow
            
            # Try system event log as fallback
            $systemLogs = Get-WinEvent -FilterHashtable @{
                LogName = 'System'
                ProviderName = 'Service Control Manager'
            } -MaxEvents 20 -ErrorAction SilentlyContinue | Where-Object { $_.Message -like "*TwinBridge*" }
            
            if ($systemLogs) {
                Write-Host "System service events:" -ForegroundColor Cyan
                $systemLogs | Sort-Object TimeCreated | ForEach-Object {
                    Write-Host "$($_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss')) $($_.Message)" -ForegroundColor Gray
                }
            }
        }
    } catch {
        Write-Host "Error retrieving logs: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Test-Connections {
    Write-Host "Testing connections..." -ForegroundColor Yellow
    
    # Test health endpoint
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:$HealthPort/health" -TimeoutSec 5
        if ($response.isHealthy) {
            Write-Host "✓ Health endpoint: Healthy" -ForegroundColor Green
        } else {
            Write-Host "⚠ Health endpoint: Unhealthy" -ForegroundColor Yellow
            $response.checks | ForEach-Object {
                $color = if ($_.status -eq "Healthy") { "Green" } else { "Red" }
                Write-Host "  $($_.name): $($_.status)" -ForegroundColor $color
                if ($_.message) {
                    Write-Host "    $($_.message)" -ForegroundColor Gray
                }
            }
        }
    } catch {
        Write-Host "✗ Health endpoint: Not accessible ($($_.Exception.Message))" -ForegroundColor Red
    }
    
    # Test serial ports
    Write-Host ""
    Write-Host "Available serial ports:" -ForegroundColor Cyan
    $ports = [System.IO.Ports.SerialPort]::GetPortNames()
    if ($ports) {
        $ports | ForEach-Object {
            Write-Host "  $_" -ForegroundColor White
        }
    } else {
        Write-Host "  No serial ports detected" -ForegroundColor Yellow
    }
    
    # Test ADS (basic check)
    Write-Host ""
    Write-Host "TwinCAT ADS:" -ForegroundColor Cyan
    try {
        $adsService = Get-Service -Name "TcSysSrv" -ErrorAction SilentlyContinue
        if ($adsService) {
            $color = if ($adsService.Status -eq "Running") { "Green" } else { "Red" }
            Write-Host "  TwinCAT System Service: $($adsService.Status)" -ForegroundColor $color
        } else {
            Write-Host "  TwinCAT System Service: Not installed" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  Error checking TwinCAT service" -ForegroundColor Red
    }
}

function Get-HealthStatus {
    Write-Host "Checking health endpoints..." -ForegroundColor Yellow
    
    $endpoints = @("/health", "/status", "/metrics")
    
    foreach ($endpoint in $endpoints) {
        try {
            Write-Host ""
            Write-Host "Endpoint: $endpoint" -ForegroundColor Cyan
            $response = Invoke-RestMethod -Uri "http://localhost:$HealthPort$endpoint" -TimeoutSec 5
            $json = $response | ConvertTo-Json -Depth 10
            Write-Host $json -ForegroundColor White
        } catch {
            Write-Host "Error accessing $endpoint`: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

function Show-Configuration {
    Write-Host "Current configuration:" -ForegroundColor Yellow
    
    $configPath = "appsettings.json"
    if (Test-Path $configPath) {
        try {
            $config = Get-Content $configPath -Raw | ConvertFrom-Json
            $config | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor White
        } catch {
            Write-Host "Error reading configuration: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "Configuration file not found: $configPath" -ForegroundColor Red
    }
}

# Main script logic
if (-not ($Status -or $Start -or $Stop -or $Restart -or $Logs -or $Test -or $Health -or $Config)) {
    Show-Help
    exit 0
}

# Require admin for service operations
if ($Start -or $Stop -or $Restart) {
    if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host "Error: Administrator privileges required for service operations" -ForegroundColor Red
        Write-Host "Please run PowerShell as Administrator" -ForegroundColor Yellow
        exit 1
    }
}

if ($Status) { Get-ServiceStatus }
if ($Start) { Start-TwinBridgeService }
if ($Stop) { Stop-TwinBridgeService }
if ($Restart) { Restart-TwinBridgeService }
if ($Logs) { Show-EventLogs }
if ($Test) { Test-Connections }
if ($Health) { Get-HealthStatus }
if ($Config) { Show-Configuration }