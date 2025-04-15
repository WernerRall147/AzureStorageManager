###############################################
# Parameters - Customize these for your setup #
###############################################
param(
    [Parameter(Mandatory=$false)]
    [string]$tenantId = "your-tenant-id",
    
    [Parameter(Mandatory=$false)]
    [string]$clientId = "your-app-registration-client-id",
    
    [Parameter(Mandatory=$false)]
    [string]$clientSecret = "your-app-registration-client-secret",
    
    [Parameter(Mandatory=$false)]
    [string]$proxyUrl = "",
    
    [Parameter(Mandatory=$false)]
    [string]$storageAccountName = "yourstorageaccount",
    
    [Parameter(Mandatory=$false)]
    [string]$shareName = "yourshare",
    
    [Parameter(Mandatory=$false)]
    [string]$localFolder = "",
    
    [Parameter(Mandatory=$false)]
    [string]$csvOutputPath = ""
)

# Default values for non-supplied parameters
if ([string]::IsNullOrEmpty($localFolder)) {
    $localFolder = Join-Path $PSScriptRoot "LocalFiles"
    Write-Output "Using default local folder: $localFolder"
}

if ([string]::IsNullOrEmpty($csvOutputPath)) {
    $csvOutputPath = Join-Path $PSScriptRoot "AzureStorageReport_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"
    Write-Output "Using default report path: $csvOutputPath"
}

# Check for secrets in environment variables if not provided directly
if ($tenantId -eq "your-tenant-id" -or [string]::IsNullOrEmpty($tenantId)) {
    $tenantId = $env:AZURE_TENANT_ID
    Write-Output "Using tenant ID from environment variable: $($tenantId.Substring(0,3))***"
}

if ($clientId -eq "your-app-registration-client-id" -or [string]::IsNullOrEmpty($clientId)) {
    $clientId = $env:AZURE_CLIENT_ID
    Write-Output "Using client ID from environment variable: $($clientId.Substring(0,3))***"
}

if ($clientSecret -eq "your-app-registration-client-secret" -or [string]::IsNullOrEmpty($clientSecret)) {
    $clientSecret = $env:AZURE_CLIENT_SECRET
    Write-Output "Using client secret from environment variable"
}

# Configuration validation
$configValid = $true
if ([string]::IsNullOrEmpty($tenantId) -or $tenantId -eq "your-tenant-id") {
    Write-Error "Tenant ID is required. Set parameter -tenantId or environment variable AZURE_TENANT_ID"
    $configValid = $false
}

if ([string]::IsNullOrEmpty($clientId) -or $clientId -eq "your-app-registration-client-id") {
    Write-Error "Client ID is required. Set parameter -clientId or environment variable AZURE_CLIENT_ID"
    $configValid = $false
}

if ([string]::IsNullOrEmpty($clientSecret) -or $clientSecret -eq "your-app-registration-client-secret") {
    Write-Error "Client Secret is required. Set parameter -clientSecret or environment variable AZURE_CLIENT_SECRET"
    $configValid = $false
}

if ([string]::IsNullOrEmpty($storageAccountName) -or $storageAccountName -eq "yourstorageaccount") {
    Write-Error "Storage Account Name is required. Set parameter -storageAccountName"
    $configValid = $false
}

if ([string]::IsNullOrEmpty($shareName) -or $shareName -eq "yourshare") {
    Write-Error "Share Name is required. Set parameter -shareName"
    $configValid = $false
}

if (-not $configValid) {
    Write-Error "Invalid configuration. Please fix the issues and try again."
    exit 1
}

# Create local folder if it doesn't exist
if (-not (Test-Path $localFolder)) {
    try {
        New-Item -Path $localFolder -ItemType Directory -Force | Out-Null
        Write-Output "Created local folder: $localFolder"
    }
    catch {
        Write-Error "Failed to create local folder: $_"
        exit 1
    }
}

# Ensure directory for CSV report exists
$reportDirectory = Split-Path -Parent $csvOutputPath
if (-not (Test-Path $reportDirectory)) {
    try {
        New-Item -Path $reportDirectory -ItemType Directory -Force | Out-Null
        Write-Output "Created report directory: $reportDirectory"
    }
    catch {
        Write-Error "Failed to create report directory: $_"
        exit 1
    }
}

# Import required assemblies
try {
    Add-Type -AssemblyName System.Web
}
catch {
    Write-Error "Failed to load System.Web assembly: $_"
    exit 1
}

#####################################################
# Function: Authenticate with Azure AD to get token #
#####################################################
function Get-AzureADToken {
    param(
        [string]$tenantId,
        [string]$clientId,
        [string]$clientSecret,
        [string]$proxy
    )

    $tokenUrl = "https://login.microsoftonline.com/$tenantId/oauth2/token"
    $body = @{
        grant_type    = "client_credentials"
        client_id     = $clientId
        client_secret = $clientSecret
        resource      = "https://storage.azure.com/"
    }

    Write-Output "Authenticating to Azure AD..."
    try {
        $webRequestParams = @{
            Method = "Post"
            Uri = $tokenUrl
            Body = $body
            ErrorAction = "Stop"
        }
        
        if (-not [string]::IsNullOrEmpty($proxy)) {
            Write-Output "Using proxy: $proxy"
            $webRequestParams.Add("Proxy", $proxy)
        }
        
        $tokenResponse = Invoke-RestMethod @webRequestParams
        Write-Output "Authentication successful"
        return $tokenResponse.access_token
    }
    catch {
        Write-Error "Authentication failed: $_"
        
        # Check for common errors and provide more guidance
        if ($_.Exception.Response.StatusCode -eq 401) {
            Write-Error "Unauthorized: Check that your tenantId, clientId, and clientSecret are correct"
        }
        elseif ($_.Exception.Response.StatusCode -eq 403) {
            Write-Error "Forbidden: The service principal doesn't have the required permissions"
        }
        
        throw
    }
}
