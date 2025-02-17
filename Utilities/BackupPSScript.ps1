###############################################
# Parameters - Customize these for your setup #
###############################################
$tenantId           = "your-tenant-id"
$clientId           = "your-app-registration-client-id"
$clientSecret       = "your-app-registration-client-secret"
$proxyUrl           = "http://your.proxy.address:port"  # e.g. http://proxy.company.com:8080

$storageAccountName = "yourstorageaccount"
$shareName          = "yourshare"  # The name of your Azure file share
$localFolder        = "C:\Path\To\LocalFolder"  # On-prem folder containing files
$csvOutputPath      = "C:\Path\To\Report.csv"

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
    $tokenResponse = Invoke-RestMethod -Method Post -Uri $tokenUrl -Body $body -Proxy $proxy
    return $tokenResponse.access_token
}

############################################################
# Function: Build the Azure File URL from a relative path  #
############################################################
function Get-AzureFileUrl {
    param(
        [string]$storageAccountName,
        [string]$shareName,
        [string]$relativePath
    )
    # Replace backslashes with forward slashes and URL-encode each segment.
    $pathParts    = $relativePath -split "[\\\/]"
    $encodedParts = $pathParts | ForEach-Object { [System.Web.HttpUtility]::UrlEncode($_) }
    $encodedPath  = $encodedParts -join '/'
    return "https://$storageAccountName.file.core.windows.net/$shareName/$encodedPath"
}

#########################################################
# Function: Get Azure File Metadata (via HEAD request)  #
#########################################################
function Get-AzureFileMetadata {
    param(
        [string]$fileUrl,
        [string]$accessToken,
        [string]$proxy
    )
    $headers = @{
        "Authorization" = "Bearer $accessToken"
        "x-ms-date"     = (Get-Date).ToUniversalTime().ToString("R")
        "x-ms-version"  = "2021-04-10"
    }
    try {
        $response = Invoke-WebRequest -Uri $fileUrl -Method Head -Headers $headers -Proxy $proxy -ErrorAction Stop
        return $response.Headers
    }
    catch {
        # If not found or any error, return $null.
        return $null
    }
}

###############################################################
# Function: Update Azure File Metadata with MD5 (via PUT)      #
###############################################################
function Update-AzureFileMetadata {
    param(
        [string]$fileUrl,
        [string]$accessToken,
        [string]$md5Hash,
        [string]$proxy
    )
    # Append ?comp=metadata to update only the metadata.
    $metadataUrl = "$fileUrl?comp=metadata"
    $headers = @{
        "Authorization"  = "Bearer $accessToken"
        "x-ms-date"      = (Get-Date).ToUniversalTime().ToString("R")
        "x-ms-version"   = "2021-04-10"
        "x-ms-meta-md5"  = $md5Hash
        "Content-Length" = "0"
    }
    try {
        Invoke-WebRequest -Uri $metadataUrl -Method Put -Headers $headers -Proxy $proxy -ErrorAction Stop
        return $true
    }
    catch {
        Write-Error "Failed to update metadata for $fileUrl: $_"
        return $false
    }
}

#########################################
# Main Script: Process and Compare Files #
#########################################

# 1. Authenticate and get an access token.
$accessToken = Get-AzureADToken -tenantId $tenantId -clientId $clientId -clientSecret $clientSecret -proxy $proxyUrl

# 2. Prepare a report array.
$report = @()

Write-Output "Processing local files in $localFolder..."
# Get all files recursively from the on-prem folder.
$localFiles = Get-ChildItem -Path $localFolder -File -Recurse
foreach ($file in $localFiles) {
    # Compute the relative path (so we can match Azure’s folder structure).
    $relativePath = $file.FullName.Substring($localFolder.Length).TrimStart("\")
    
    # Compute MD5 hash for the file.
    $fileHashObj = Get-FileHash -Path $file.FullName -Algorithm MD5
    $localMD5    = $fileHashObj.Hash

    # Build the corresponding Azure File URL.
    $azureFileUrl = Get-AzureFileUrl -storageAccountName $storageAccountName -shareName $shareName -relativePath $relativePath

    # 3. Try to get metadata for the Azure file.
    $azureHeaders = Get-AzureFileMetadata -fileUrl $azureFileUrl -accessToken $accessToken -proxy $proxyUrl

    $azureMD5 = ""
    $status   = ""
    if ($azureHeaders -eq $null) {
        # File not found in Azure.
        $status = "Not Found in Azure"
    }
    else {
        # Check if MD5 metadata is already present.
        if ($azureHeaders["x-ms-meta-md5"]) {
            $azureMD5 = $azureHeaders["x-ms-meta-md5"]
        }
        elseif ($azureHeaders["Content-MD5"]) {
            # In some cases the file may have a Content-MD5 header.
            $azureMD5 = $azureHeaders["Content-MD5"]
        }

        if ($azureMD5) {
            if ($azureMD5 -eq $localMD5) {
                $status = "Match"
            }
            else {
                $status = "Mismatch"
            }
        }
        else {
            $status = "No MD5 metadata present"
        }

        # 4. Update the Azure file’s metadata with the computed MD5.
        $updateSuccess = Update-AzureFileMetadata -fileUrl $azureFileUrl -accessToken $accessToken -md5Hash $localMD5 -proxy $proxyUrl
        if ($updateSuccess) {
            # After a successful update, assume the Azure MD5 now equals the local MD5.
            $azureMD5 = $localMD5
            $status   = "Updated"
        }
        else {
            $status = "Update Failed"
        }
    }

    # 5. Build the report entry.
    $reportItem = [PSCustomObject]@{
        FilePath      = $relativePath
        LocalFullPath = $file.FullName
        FileSize      = $file.Length
        LocalMD5      = $localMD5
        AzureMD5      = $azureMD5
        Status        = $status
    }
    $report += $reportItem
    Write-Output "Processed: $relativePath - Status: $status"
}

# 6. Export the report to CSV.
$report | Export-Csv -Path $csvOutputPath -NoTypeInformation -Encoding UTF8
Write-Output "CSV report saved to $csvOutputPath"
