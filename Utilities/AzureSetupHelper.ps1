# AzureSetupHelper.ps1
# This script helps set up Azure app registration with proper permissions and updates appsettings.json

# Global variables for proxy settings
$global:proxySettingsPath = Join-Path $PSScriptRoot "proxy_config.json"
$global:proxyEnabled = $false
$global:proxyServer = ""
$global:proxyBypass = ""
$global:proxyCredentials = $null

# Load proxy settings if they exist
function Load-ProxySettings {
    if (Test-Path $global:proxySettingsPath) {
        try {
            $proxyConfig = Get-Content $global:proxySettingsPath -Raw | ConvertFrom-Json
            $global:proxyEnabled = $proxyConfig.Enabled
            $global:proxyServer = $proxyConfig.Server
            $global:proxyBypass = $proxyConfig.Bypass
            
            if ($proxyConfig.UseCredentials -and $proxyConfig.Username) {
                $securePassword = if ($proxyConfig.Password) { 
                    ConvertTo-SecureString -String $proxyConfig.Password -AsPlainText -Force 
                } else { 
                    (New-Object System.Security.SecureString) 
                }
                $global:proxyCredentials = New-Object System.Management.Automation.PSCredential($proxyConfig.Username, $securePassword)
            }
            
            Write-Host "Loaded proxy configuration:" -ForegroundColor Green
            Write-Host "  Enabled: $($global:proxyEnabled)" -ForegroundColor Green
            if ($global:proxyEnabled) {
                Write-Host "  Server: $($global:proxyServer)" -ForegroundColor Green
                Write-Host "  Bypass: $($global:proxyBypass)" -ForegroundColor Green
                Write-Host "  Credentials: $(if ($global:proxyCredentials) { "Yes" } else { "No" })" -ForegroundColor Green
            }
        }
        catch {
            Write-Warning "Error loading proxy settings: $_"
        }
    }
}

# Apply proxy settings to the current session
function Apply-ProxySettings {
    if ($global:proxyEnabled -and $global:proxyServer) {
        $proxy = New-Object System.Net.WebProxy($global:proxyServer, $true)
        
        if ($global:proxyBypass) {
            $bypassList = $global:proxyBypass -split ";"
            $proxy.BypassList = $bypassList
        }
        
        if ($global:proxyCredentials) {
            $proxy.Credentials = $global:proxyCredentials
        }
        
        [System.Net.WebRequest]::DefaultWebProxy = $proxy
        
        # Set for Az PowerShell modules
        if (Get-Command Connect-AzAccount -ErrorAction SilentlyContinue) {
            $PSDefaultParameterValues["Connect-AzAccount:UseDeviceAuthentication"] = $true
        }
        
        Write-Host "Applied proxy settings to current session" -ForegroundColor Green
        return $true
    }
    else {
        # Clear proxy if it was disabled
        [System.Net.WebRequest]::DefaultWebProxy = $null
        return $false
    }
}

# Configure proxy settings
function Configure-ProxySettings {
    Clear-Host
    Write-Host "===== Proxy Configuration =====" -ForegroundColor Cyan
    
    $enableProxy = Read-Host "Enable proxy? (y/n)"
    $global:proxyEnabled = ($enableProxy -eq "y")
    
    if ($global:proxyEnabled) {
        $global:proxyServer = Read-Host "Enter proxy server URL (e.g., http://proxy.example.com:8080)"
        $global:proxyBypass = Read-Host "Enter proxy bypass list (semicolon separated, leave empty for none)"
        
        $useCredentials = Read-Host "Does your proxy require authentication? (y/n)"
        if ($useCredentials -eq "y") {
            $username = Read-Host "Enter proxy username"
            $password = Read-Host "Enter proxy password" -AsSecureString
            $global:proxyCredentials = New-Object System.Management.Automation.PSCredential($username, $password)
            
            # For storing in the config (optional, can be skipped for security reasons)
            $plainPassword = Read-Host "Save password in config file? (leave empty to not save)"
        }
        else {
            $global:proxyCredentials = $null
            $username = ""
            $plainPassword = ""
        }
        
        # Save settings
        $proxyConfig = [PSCustomObject]@{
            Enabled = $global:proxyEnabled
            Server = $global:proxyServer
            Bypass = $global:proxyBypass
            UseCredentials = ($useCredentials -eq "y")
            Username = $username
            Password = $plainPassword
        }
        
        $proxyConfig | ConvertTo-Json | Set-Content $global:proxySettingsPath
        Write-Host "Proxy settings saved" -ForegroundColor Green
    }
    else {
        # Save disabled state
        $proxyConfig = [PSCustomObject]@{
            Enabled = $false
            Server = ""
            Bypass = ""
            UseCredentials = $false
            Username = ""
            Password = ""
        }
        
        $proxyConfig | ConvertTo-Json | Set-Content $global:proxySettingsPath
        Write-Host "Proxy disabled" -ForegroundColor Green
    }
    
    # Apply the new settings
    Apply-ProxySettings
}

# Ensure Az module is installed
if (!(Get-Module -ListAvailable -Name Az.Accounts) -or 
    !(Get-Module -ListAvailable -Name Az.Resources) -or 
    !(Get-Module -ListAvailable -Name Az.Storage)) {
    Write-Host "Installing required Az modules..." -ForegroundColor Yellow
    Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force
}

# Import required modules
Import-Module Az.Accounts
Import-Module Az.Resources
Import-Module Az.Storage

# Load proxy settings at startup
Load-ProxySettings
Apply-ProxySettings

# Function to setup Azure App Registration and permissions
function Setup-AzureAppRegistration {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppName,
        
        [Parameter(Mandatory = $false)]
        [string]$CertPath,
        
        [Parameter(Mandatory = $false)]
        [switch]$UseClientSecret,
        
        [Parameter(Mandatory = $false)]
        [string]$StorageAccountName
    )

    # Check if user is logged in
    try {
        $context = Get-AzContext
        if (!$context) {
            Write-Host "Logging in to Azure..." -ForegroundColor Yellow
            Connect-AzAccount
        }
    }
    catch {
        Write-Host "Logging in to Azure..." -ForegroundColor Yellow
        Connect-AzAccount
    }

    # Get tenant ID
    $tenantId = (Get-AzContext).Tenant.Id
    Write-Host "Using Tenant ID: $tenantId" -ForegroundColor Green

    # Create app registration if it doesn't exist
    Write-Host "Creating app registration '$AppName'..." -ForegroundColor Yellow
    $app = Get-AzADApplication -DisplayName $AppName -ErrorAction SilentlyContinue
    
    if (!$app) {
        if ($UseClientSecret) {
            $app = New-AzADApplication -DisplayName $AppName
            $appSecret = New-AzADAppCredential -ApplicationId $app.AppId -StartDate (Get-Date) -EndDate (Get-Date).AddYears(1)
            $clientSecret = $appSecret.SecretText
            Write-Host "Created app registration with client secret" -ForegroundColor Green
        }
        elseif ($CertPath) {
            # Use certificate authentication
            if (!(Test-Path $CertPath)) {
                Write-Error "Certificate file not found at path: $CertPath"
                return
            }
            
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
            $cert.Import($CertPath)
            
            $app = New-AzADApplication -DisplayName $AppName
            New-AzADAppCredential -ApplicationId $app.AppId -CertValue ([Convert]::ToBase64String($cert.GetRawCertData())) -StartDate $cert.NotBefore -EndDate $cert.NotAfter
            $certThumbprint = $cert.Thumbprint
            Write-Host "Created app registration with certificate authentication" -ForegroundColor Green
        }
        else {
            # Create with no credentials - user will add later
            $app = New-AzADApplication -DisplayName $AppName
            Write-Host "Created app registration without authentication credentials" -ForegroundColor Green
        }
    }
    else {
        Write-Host "App registration already exists" -ForegroundColor Yellow
        
        if ($UseClientSecret) {
            $appSecret = New-AzADAppCredential -ApplicationId $app.AppId -StartDate (Get-Date) -EndDate (Get-Date).AddYears(1)
            $clientSecret = $appSecret.SecretText
            Write-Host "Added new client secret to existing app registration" -ForegroundColor Green
        }
        elseif ($CertPath) {
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
            $cert.Import($CertPath)
            
            New-AzADAppCredential -ApplicationId $app.AppId -CertValue ([Convert]::ToBase64String($cert.GetRawCertData())) -StartDate $cert.NotBefore -EndDate $cert.NotAfter
            $certThumbprint = $cert.Thumbprint
            Write-Host "Added certificate to existing app registration" -ForegroundColor Green
        }
    }

    # Create service principal if it doesn't exist
    $sp = Get-AzADServicePrincipal -ApplicationId $app.AppId -ErrorAction SilentlyContinue
    if (!$sp) {
        $sp = New-AzADServicePrincipal -ApplicationId $app.AppId
        Write-Host "Created service principal for the application" -ForegroundColor Green
    }
    
    # If storage account provided, assign roles
    if ($StorageAccountName) {
        $storageAccount = Get-AzStorageAccount | Where-Object { $_.StorageAccountName -eq $StorageAccountName }
        
        if ($storageAccount) {
            # Assign Storage Blob Data Contributor role
            Write-Host "Assigning 'Storage Blob Data Contributor' role..." -ForegroundColor Yellow
            $roleAssignment = Get-AzRoleAssignment -ObjectId $sp.Id -RoleDefinitionName "Storage Blob Data Contributor" -Scope $storageAccount.Id -ErrorAction SilentlyContinue
            if (!$roleAssignment) {
                New-AzRoleAssignment -ObjectId $sp.Id -RoleDefinitionName "Storage Blob Data Contributor" -Scope $storageAccount.Id
                Write-Host "Assigned 'Storage Blob Data Contributor' role" -ForegroundColor Green
            }
            else {
                Write-Host "'Storage Blob Data Contributor' role already assigned" -ForegroundColor Green
            }
            
            # Assign Storage File Data Privileged Contributor role
            Write-Host "Assigning 'Storage File Data Privileged Contributor' role..." -ForegroundColor Yellow
            $roleAssignment = Get-AzRoleAssignment -ObjectId $sp.Id -RoleDefinitionName "Storage File Data Privileged Contributor" -Scope $storageAccount.Id -ErrorAction SilentlyContinue
            if (!$roleAssignment) {
                New-AzRoleAssignment -ObjectId $sp.Id -RoleDefinitionName "Storage File Data Privileged Contributor" -Scope $storageAccount.Id
                Write-Host "Assigned 'Storage File Data Privileged Contributor' role" -ForegroundColor Green
            }
            else {
                Write-Host "'Storage File Data Privileged Contributor' role already assigned" -ForegroundColor Green
            }
        }
        else {
            Write-Warning "Storage account '$StorageAccountName' not found. Unable to assign roles."
        }
    }
    
    # Update appsettings.json
    $appSettingsPath = Join-Path $PSScriptRoot "..\appsettings.json"
    if (Test-Path $appSettingsPath) {
        Write-Host "Updating appsettings.json..." -ForegroundColor Yellow
        $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        
        # Create Azure section if it doesn't exist
        if (!$appSettings.Azure) {
            $appSettings | Add-Member -MemberType NoteProperty -Name "Azure" -Value ([PSCustomObject]@{})
        }
        
        $appSettings.Azure.TenantId = $tenantId
        $appSettings.Azure.ClientId = $app.AppId
        
        if ($UseClientSecret) {
            $appSettings.Azure.ClientSecret = $clientSecret
            $appSettings.Azure.CertificateThumbprint = ""
        }
        elseif ($CertPath) {
            $appSettings.Azure.CertificateThumbprint = $certThumbprint
            $appSettings.Azure.ClientSecret = ""
        }
        
        $appSettings | ConvertTo-Json -Depth 10 | Set-Content $appSettingsPath
        Write-Host "Updated appsettings.json with new values" -ForegroundColor Green
    }
    else {
        Write-Warning "appsettings.json not found at path: $appSettingsPath"
    }
    
    # Return app registration details
    return [PSCustomObject]@{
        TenantId = $tenantId
        ClientId = $app.AppId
        ClientSecret = $clientSecret
        CertificateThumbprint = $certThumbprint
        ServicePrincipalId = $sp.Id
    }
}

# Function to validate Azure access
function Test-AzureAccess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StorageAccountName,
        
        [Parameter(Mandatory = $false)]
        [switch]$UseAppSettings
    )
    
    if ($UseAppSettings) {
        $appSettingsPath = Join-Path $PSScriptRoot "..\appsettings.json"
        if (Test-Path $appSettingsPath) {
            $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
            
            if ($appSettings.Azure) {
                # Test authentication using appsettings values
                $tenantId = $appSettings.Azure.TenantId
                $clientId = $appSettings.Azure.ClientId
                $clientSecret = $appSettings.Azure.ClientSecret
                $certThumbprint = $appSettings.Azure.CertificateThumbprint
                
                if ($certThumbprint) {
                    Write-Host "Testing access using certificate authentication..." -ForegroundColor Yellow
                    try {
                        # Use certificate auth
                        $cert = Get-Item -Path "Cert:\CurrentUser\My\$certThumbprint" -ErrorAction Stop
                        Connect-AzAccount -ServicePrincipal -Tenant $tenantId -ApplicationId $clientId -CertificateThumbprint $certThumbprint
                        Write-Host "Certificate authentication successful" -ForegroundColor Green
                    }
                    catch {
                        Write-Error "Certificate authentication failed: $_"
                        return $false
                    }
                }
                elseif ($clientSecret) {
                    Write-Host "Testing access using client secret authentication..." -ForegroundColor Yellow
                    try {
                        # Use client secret auth
                        $secureSecret = ConvertTo-SecureString -String $clientSecret -AsPlainText -Force
                        $credential = New-Object System.Management.Automation.PSCredential($clientId, $secureSecret)
                        Connect-AzAccount -ServicePrincipal -Tenant $tenantId -Credential $credential
                        Write-Host "Client secret authentication successful" -ForegroundColor Green
                    }
                    catch {
                        Write-Error "Client secret authentication failed: $_"
                        return $false
                    }
                }
                else {
                    Write-Error "No authentication credentials found in appsettings.json"
                    return $false
                }
            }
            else {
                Write-Error "Azure section not found in appsettings.json"
                return $false
            }
        }
        else {
            Write-Error "appsettings.json not found at path: $appSettingsPath"
            return $false
        }
    }
    else {
        # Use current Az PowerShell context
        $context = Get-AzContext
        if (!$context) {
            Write-Error "Not logged in to Azure. Please run Connect-AzAccount first."
            return $false
        }
    }
    
    # Test access to the storage account
    try {
        Write-Host "Testing access to storage account '$StorageAccountName'..." -ForegroundColor Yellow
        
        # Test blob access
        Write-Host "Testing blob access..." -ForegroundColor Yellow
        $storageAccount = Get-AzStorageAccount -Name $StorageAccountName -ErrorAction Stop
        $ctx = $storageAccount.Context
        Get-AzStorageContainer -Context $ctx -MaxCount 1 -ErrorAction Stop | Out-Null
        Write-Host "Successfully accessed blob storage" -ForegroundColor Green
        
        # Test file share access
        Write-Host "Testing file share access..." -ForegroundColor Yellow
        Get-AzStorageShare -Context $ctx -MaxCount 1 -ErrorAction Stop | Out-Null
        Write-Host "Successfully accessed file shares" -ForegroundColor Green
        
        return $true
    }
    catch {
        Write-Error "Access test failed: $_"
        return $false
    }
}

# Main menu function
function Show-Menu {
    Clear-Host
    Write-Host "===== Azure Storage Manager Setup Helper =====" -ForegroundColor Cyan
    Write-Host "1. Create new app registration and setup permissions"
    Write-Host "2. Add certificate authentication to existing app"
    Write-Host "3. Add client secret authentication to existing app"
    Write-Host "4. Assign roles to existing app registration"
    Write-Host "5. Test Azure access with current settings"
    Write-Host "6. Update appsettings.json manually"
    Write-Host "7. Generate self-signed certificate"
    Write-Host "8. Configure proxy settings"
    Write-Host "Q. Quit"
    
    # Show current proxy status in the menu
    if ($global:proxyEnabled) {
        Write-Host ""
        Write-Host "Current proxy configuration: ENABLED" -ForegroundColor Green
        Write-Host "  Server: $($global:proxyServer)" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "Current proxy configuration: DISABLED" -ForegroundColor Yellow
    }
    Write-Host ""
    
    $choice = Read-Host "Enter your choice"
    
    switch ($choice) {
        "1" {
            $appName = Read-Host "Enter a name for the app registration"
            $authType = Read-Host "Authentication type (cert/secret)"
            $storageAccount = Read-Host "Enter storage account name (optional)"
            
            if ($authType -eq "cert") {
                $generateCert = Read-Host "Generate new self-signed certificate? (y/n)"
                if ($generateCert -eq "y") {
                    $certPath = Generate-SelfSignedCertificate -AppName $appName
                }
                else {
                    $certPath = Read-Host "Enter path to certificate file (.pfx)"
                }
                
                Setup-AzureAppRegistration -AppName $appName -CertPath $certPath -StorageAccountName $storageAccount
            }
            else {
                Setup-AzureAppRegistration -AppName $appName -UseClientSecret -StorageAccountName $storageAccount
            }
            
            Write-Host "Press Enter to continue..."
            Read-Host
            Show-Menu
        }
        "2" {
            $appName = Read-Host "Enter the name of the existing app registration"
            $generateCert = Read-Host "Generate new self-signed certificate? (y/n)"
            
            if ($generateCert -eq "y") {
                $certPath = Generate-SelfSignedCertificate -AppName $appName
            }
            else {
                $certPath = Read-Host "Enter path to certificate file (.pfx)"
            }
            
            Setup-AzureAppRegistration -AppName $appName -CertPath $certPath
            
            Write-Host "Press Enter to continue..."
            Read-Host
            Show-Menu
        }
        "3" {
            $appName = Read-Host "Enter the name of the existing app registration"
            Setup-AzureAppRegistration -AppName $appName -UseClientSecret
            
            Write-Host "Press Enter to continue..."
            Read-Host
            Show-Menu
        }
        "4" {
            $appName = Read-Host "Enter the name of the existing app registration"
            $storageAccount = Read-Host "Enter storage account name"
            
            if ([string]::IsNullOrEmpty($storageAccount)) {
                Write-Error "Storage account name is required"
            }
            else {
                $app = Get-AzADApplication -DisplayName $appName -ErrorAction SilentlyContinue
                if (!$app) {
                    Write-Error "App registration '$appName' not found"
                }
                else {
                    $sp = Get-AzADServicePrincipal -ApplicationId $app.AppId -ErrorAction SilentlyContinue
                    if (!$sp) {
                        $sp = New-AzADServicePrincipal -ApplicationId $app.AppId
                    }
                    
                    $storageAcct = Get-AzStorageAccount | Where-Object { $_.StorageAccountName -eq $storageAccount }
                    
                    if ($storageAcct) {
                        # Assign roles
                        New-AzRoleAssignment -ObjectId $sp.Id -RoleDefinitionName "Storage Blob Data Contributor" -Scope $storageAcct.Id
                        New-AzRoleAssignment -ObjectId $sp.Id -RoleDefinitionName "Storage File Data Privileged Contributor" -Scope $storageAcct.Id
                        Write-Host "Roles assigned successfully" -ForegroundColor Green
                    }
                    else {
                        Write-Error "Storage account '$storageAccount' not found"
                    }
                }
            }
            
            Write-Host "Press Enter to continue..."
            Read-Host
            Show-Menu
        }
        "5" {
            $storageAccount = Read-Host "Enter storage account name"
            $useAppSettings = Read-Host "Use appsettings.json credentials? (y/n)"
            
            if ([string]::IsNullOrEmpty($storageAccount)) {
                Write-Error "Storage account name is required"
            }
            else {
                if ($useAppSettings -eq "y") {
                    Test-AzureAccess -StorageAccountName $storageAccount -UseAppSettings
                }
                else {
                    Test-AzureAccess -StorageAccountName $storageAccount
                }
            }
            
            Write-Host "Press Enter to continue..."
            Read-Host
            Show-Menu
        }
        "6" {
            $appSettingsPath = Join-Path $PSScriptRoot "..\appsettings.json"
            if (Test-Path $appSettingsPath) {
                $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
                
                if (!$appSettings.Azure) {
                    $appSettings | Add-Member -MemberType NoteProperty -Name "Azure" -Value ([PSCustomObject]@{})
                }
                
                $appSettings.Azure.TenantId = Read-Host "Enter Tenant ID"
                $appSettings.Azure.ClientId = Read-Host "Enter Client ID (Application ID)"
                
                $authType = Read-Host "Authentication type (cert/secret)"
                if ($authType -eq "cert") {
                    $appSettings.Azure.CertificateThumbprint = Read-Host "Enter Certificate Thumbprint"
                    $appSettings.Azure.ClientSecret = ""
                }
                else {
                    $appSettings.Azure.ClientSecret = Read-Host "Enter Client Secret"
                    $appSettings.Azure.CertificateThumbprint = ""
                }
                
                $appSettings | ConvertTo-Json -Depth 10 | Set-Content $appSettingsPath
                Write-Host "Updated appsettings.json with new values" -ForegroundColor Green
            }
            else {
                $json = [PSCustomObject]@{
                    Azure = [PSCustomObject]@{
                        TenantId = Read-Host "Enter Tenant ID"
                        ClientId = Read-Host "Enter Client ID (Application ID)"
                    }
                }
                
                $authType = Read-Host "Authentication type (cert/secret)"
                if ($authType -eq "cert") {
                    $json.Azure | Add-Member -MemberType NoteProperty -Name "CertificateThumbprint" -Value (Read-Host "Enter Certificate Thumbprint")
                    $json.Azure | Add-Member -MemberType NoteProperty -Name "ClientSecret" -Value ""
                }
                else {
                    $json.Azure | Add-Member -MemberType NoteProperty -Name "ClientSecret" -Value (Read-Host "Enter Client Secret")
                    $json.Azure | Add-Member -MemberType NoteProperty -Name "CertificateThumbprint" -Value ""
                }
                
                $json | ConvertTo-Json -Depth 10 | Set-Content $appSettingsPath
                Write-Host "Created appsettings.json with new values" -ForegroundColor Green
            }
            
            Write-Host "Press Enter to continue..."
            Read-Host
            Show-Menu
        }        "7" {
            $appName = Read-Host "Enter a name for the certificate"
            New-SelfSignedCertificateFile -AppName $appName
            
            Write-Host "Press Enter to continue..."
            Read-Host
            Show-Menu
        }
        "8" {
            # Call the proxy configuration function
            Configure-ProxySettings
            
            Write-Host "Press Enter to continue..."
            Read-Host
            Show-Menu
        }
        "Q" {
            return
        }
        "q" {
            return
        }
        default {
            Write-Host "Invalid choice. Press Enter to continue..."
            Read-Host
            Show-Menu
        }
    }
}

# Function to generate a self-signed certificate
function Generate-SelfSignedCertificate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppName
    )
    
    $certPath = Join-Path $PSScriptRoot "$AppName.pfx"
    $certPassword = Read-Host -AsSecureString "Enter a password for the certificate" 
    
    Write-Host "Generating self-signed certificate..." -ForegroundColor Yellow
    
    $cert = New-SelfSignedCertificate -Subject "CN=$AppName" -CertStoreLocation "Cert:\CurrentUser\My" -KeyExportPolicy Exportable -KeySpec Signature -KeyLength 2048 -KeyAlgorithm RSA -HashAlgorithm SHA256 -NotAfter (Get-Date).AddYears(2)
    
    Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $certPath -Password $certPassword | Out-Null
    
    Write-Host "Certificate generated successfully at: $certPath" -ForegroundColor Green
    Write-Host "Certificate Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green
    
    return $certPath
}

# Start the menu
Show-Menu
