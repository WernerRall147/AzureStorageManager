# Log Collection Script for Azure Storage Manager Testing
# This script helps collect application logs, system information, and diagnostic data
# during testing on Windows Server 2016

param (
    [Parameter(Mandatory=$true)]
    [string]$TestName,
    
    [Parameter(Mandatory=$false)]
    [string]$LogDirectory = ".\TestLogs"
)

# Create timestamp for this test run
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$testLogDir = Join-Path -Path $LogDirectory -ChildPath "${TestName}_${timestamp}"

# Create directory structure
New-Item -ItemType Directory -Path $testLogDir -Force | Out-Null
New-Item -ItemType Directory -Path "$testLogDir\SystemInfo" -Force | Out-Null
New-Item -ItemType Directory -Path "$testLogDir\AppLogs" -Force | Out-Null
New-Item -ItemType Directory -Path "$testLogDir\NetworkInfo" -Force | Out-Null

# Function to get system information
function Get-SystemDetails {
    Write-Host "Collecting system information..."
    
    # Basic system info
    Get-ComputerInfo | Out-File "$testLogDir\SystemInfo\ComputerInfo.txt"
    
    # Memory details
    Get-CimInstance Win32_PhysicalMemory | Format-Table Manufacturer,Capacity,DeviceLocator,Tag -AutoSize | Out-File "$testLogDir\SystemInfo\Memory.txt"
    
    # CPU details
    Get-CimInstance -ClassName Win32_Processor | Format-Table Name,MaxClockSpeed,NumberOfCores,NumberOfLogicalProcessors -AutoSize | Out-File "$testLogDir\SystemInfo\CPU.txt"
    
    # Disk space
    Get-CimInstance -ClassName Win32_LogicalDisk | Format-Table DeviceID,Size,FreeSpace | Out-File "$testLogDir\SystemInfo\DiskSpace.txt"
    
    # Operating system details
    Get-CimInstance -ClassName Win32_OperatingSystem | Format-List * | Out-File "$testLogDir\SystemInfo\OS.txt"

    # List running processes
    Get-Process | Sort-Object -Property CPU -Descending | Select-Object -First 20 | Format-Table | Out-File "$testLogDir\SystemInfo\RunningProcesses.txt"
}

# Function to collect application logs
function Get-ApplicationLogs {
    Write-Host "Collecting application logs..."
    
    # Copy AzureStorageManager logs
    if (Test-Path "$env:USERPROFILE\AppData\Local\AzureStorageManager\logs") {
        Copy-Item -Path "$env:USERPROFILE\AppData\Local\AzureStorageManager\logs\*" -Destination "$testLogDir\AppLogs" -Recurse
    }
    
    # Copy CSV reports
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    if (Test-Path "$desktopPath\*Report*.csv") {
        Copy-Item -Path "$desktopPath\*Report*.csv" -Destination "$testLogDir\AppLogs"
    }
    
    # Application Event Logs
    Get-EventLog -LogName Application -Newest 100 | Out-File "$testLogDir\AppLogs\ApplicationEventLog.txt"
    
    # Check for any error dumps
    if (Test-Path "$env:USERPROFILE\AppData\Local\CrashDumps") {
        Get-ChildItem -Path "$env:USERPROFILE\AppData\Local\CrashDumps" -Recurse | Select-Object -Property FullName,Length,CreationTime | Out-File "$testLogDir\AppLogs\CrashDumps.txt"
    }
}

# Function to collect network information
function Get-NetworkDetails {
    Write-Host "Collecting network information..."
    
    # Network adapters
    Get-NetAdapter | Format-Table Name,InterfaceDescription,Status,LinkSpeed -AutoSize | Out-File "$testLogDir\NetworkInfo\NetAdapters.txt"
    
    # IP configuration
    Get-NetIPAddress | Format-Table InterfaceAlias,IPAddress,PrefixLength,AddressState -AutoSize | Out-File "$testLogDir\NetworkInfo\IPAddresses.txt"
    
    # DNS settings
    Get-DnsClientServerAddress | Format-Table InterfaceAlias,ServerAddresses -AutoSize | Out-File "$testLogDir\NetworkInfo\DNSSettings.txt"
    
    # Test connection to Azure Storage
    Test-NetConnection -ComputerName "blob.core.windows.net" -InformationLevel Detailed | Out-File "$testLogDir\NetworkInfo\ConnectivityTest.txt"
}

# Function to create test summary
function Create-TestSummary {
    param (
        [string]$TestDescription,
        [string]$Result,
        [string]$Notes
    )
    
    Write-Host "Creating test summary..."
    
    @"
# Test Summary
- Test: $TestName
- Date/Time: $(Get-Date)
- Description: $TestDescription
- Result: $Result

## Notes
$Notes

## System Information
- OS: $(Get-CimInstance -ClassName Win32_OperatingSystem).Caption
- Memory: $([math]::Round((Get-CimInstance -ClassName Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)) GB
- CPU: $((Get-CimInstance -ClassName Win32_Processor).Name)

## Log Directory
$testLogDir
"@ | Out-File "$testLogDir\TestSummary.md"
}

# Main execution
Write-Host "Starting log collection for test: $TestName"

try {
    Get-SystemDetails
    Get-ApplicationLogs
    Get-NetworkDetails
    
    # Create a placeholder summary (to be filled in manually)
    Create-TestSummary -TestDescription "Please fill in test description" -Result "Please fill in test result" -Notes "Please add any observations here"
    
    # Create a ZIP file of all logs
    $zipFile = "$LogDirectory\${TestName}_${timestamp}.zip"
    Compress-Archive -Path $testLogDir -DestinationPath $zipFile -Force
    
    Write-Host "Log collection complete. Files saved to:"
    Write-Host " - Raw logs: $testLogDir"
    Write-Host " - ZIP file: $zipFile"
}
catch {
    Write-Host "Error collecting logs: $_" -ForegroundColor Red
}
