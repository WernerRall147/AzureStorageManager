# Azure Storage Manager Tool

**Azure Storage Manager Tool** is a command-line utility for verifying file integrity between local files and Azure Blob Storage or Azure File Shares. This tool compares MD5 hashes of local files with those stored in Azure, ensuring data accuracy for backups, migrations, and routine integrity checks.

## Features

- **Verify file integrity**: Ensures that files stored in Azure match local copies by comparing MD5 hashes.
- **Copy files to Azure**: Upload local files to Azure Blob Storage or File Shares with automatic MD5 hash calculation.
- **Download files from Azure**: Download files from Azure Blob Storage or File Shares with integrity verification.
- **Supports Azure Blob Storage and Azure File Shares**: Use the tool for different Azure storage types.
- **CSV Report Generation**: Produces a detailed report showing processed files, MD5 hashes, and match statuses.
- **Self-contained executable**: Distribute the tool as a single executable for easy deployment.

## Requirements

- Azure Storage Account Name and Key
- Name of the Blob Container or File Share to be verified
- Local directory path containing the original files for comparison
- **Windows OS** (due to .NET 9.0 Windows compatibility)
- **Required Azure IAM Permissions**:
  - For Blob Storage operations: `Storage Blob Data Contributor` role
  - For File Share operations: One of the following roles:
    - `Storage File Data SMB Share Elevated Contributor` (recommended for most scenarios)
    - `Storage File Data Privileged Contributor` (for scenarios requiring permission override)
  - These permissions are required for the authenticated identity (account, service principal, or managed identity)

## Known Issues and Solutions

### Azure File Share Permissions

When working with Azure File Shares, you might encounter `AuthorizationPermissionMismatch` errors or messages about missing required headers. This is because:

1. Azure File Share operations require specific permissions (roles)
2. As of 2025, requests to Azure File Shares require the `x-ms-file-request-intent` header

The tool includes built-in handling for these requirements:
- A custom HTTP pipeline policy (`FileRequestIntentPolicy`) ensures the required header is included in all requests, with duplicate header protection
- Enhanced existence checking ensures accurate file share detection, even with limited permissions
- Permission diagnostics can identify exactly which permissions you're missing
- The tool will provide recommendations for the appropriate Azure roles to assign
- Support for creating missing file shares during diagnostics for more complete testing

To troubleshoot permission issues:
1. Run option 10 in the main menu "Test File Share Permissions"
2. Review the test results to see which operations are failing
3. If the file share doesn't exist, you'll be prompted to create it for complete testing
4. Follow the recommendations to assign the appropriate Azure roles in Azure Portal

All Azure File Share operations (file integrity verification, uploads, downloads, reporting) use these enhanced permission handling mechanisms to ensure reliable operation.

## Installation

1. **Clone the Repository**:

   ```bash
   git clone https://github.com/yourusername/azure-storage-manager-tool.git
   cd azure-storage-manager-tool
   ```
   
2. **Build and Publish the Application**:

   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
   ```
   
3. **Run the Application**:

   Navigate to the `publish` folder and execute `AzureStorageManager.exe`.

   ```bash
   cd bin\Release\net9.0-windows\win-x64\publish
   AzureStorageManager.exe
   ```

## Azure Setup and Authentication

This tool supports authentication with Azure using service principals (app registrations) with either certificate or client secret authentication.

1. **Required Azure Permissions**
   
   The identity used to access Azure storage requires these IAM roles:
   - `Storage Blob Data Contributor` - For blob storage operations
   - `Storage File Data Privileged Contributor` - For file share operations

2. **Azure Setup Helper**

   We provide a PowerShell utility to help you set up the required Azure resources and permissions:
   
   ```bash
   # Navigate to the Utilities folder
   cd Utilities
   
   # Run the Azure setup helper
   .\AzureSetupHelper.ps1
   ```
   
   The setup helper can:
   - Create Azure app registrations with the required permissions
   - Generate self-signed certificates for authentication
   - Assign the proper IAM roles to your app registration
   - Configure your appsettings.json file automatically
   - Test your Azure permissions to ensure everything works correctly

3. **Manual Setup**
   
   If you prefer to set up Azure resources manually:
   
   - Create an Azure App Registration in Azure Portal
   - Generate a client secret or use certificate authentication (recommended)
   - Assign the necessary IAM roles to your app registration
   - Configure the connection settings in the application

## Usage

1. **Start the Application**:
   
   ```
   AzureStorageManager.exe
   ```

2. **Main Menu Options**:

   - **1**: Copy Files to Azure
   - **2**: Verify File Integrity
   - **3**: Generate MD5 Hashes for Local Files
   - **4**: Check and Update Azure Metadata
   - **5**: Download Files from Azure
   - **6**: Generate Consolidated Report
   - **7**: View Logs
   - **8**: Settings
   - **9**: Help
   - **10**: Test File Share Permissions (diagnose permission issues)
   - **11**: Exit

3. **File Integrity Verification Workflow**:

   - Select option 1 or 2 from the main menu
   - Enter your Azure Storage details when prompted
   - Specify the local directory path or use the file dialog to select it
   - Confirm the operation to begin the verification process   - Review results on the screen and in the generated CSV report

4. **Copy Files to Azure Workflow**:

   - Select option 1 from the main menu
   - Enter your Azure Storage details when prompted
   - Choose between Blob Storage or File Share
   - Select the local directory containing files to upload
   - The tool will upload files, calculate MD5 hashes, and store them as metadata

5. **Download Files from Azure Workflow**:

   - Select option 5 from the main menu
   - Enter your Azure Storage details when prompted
   - Choose between Blob Storage or File Share
   - For File Shares, you can browse and select a specific directory
   - Select a local directory to download files to
   - The tool will download files, preserve directory structure, and verify integrity

6. **Interpreting Results**:

   - **Match**: Local file hash matches the Azure storage hash
   - **Mismatch**: File exists in both locations but hashes differ
   - **Missing in Azure**: File exists locally but not in Azure storage
   - **Missing Locally**: File exists in Azure storage but not locally
   - **No Hash in Azure**: File exists in both locations but no hash in Azure metadata

## Configuration

### Application Settings

The application stores configuration in `appsettings.json`:

```json
{
  "Azure": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "CertificateThumbprint": "your-certificate-thumbprint",
    "ClientSecret": ""
  },
  "Proxy": {
    "UseProxy": false,
    "ProxyUrl": "your-proxy-server",
    "ProxyPort": "your-proxy-port",
    "Username": "proxy-username-if-needed",
    "Password": "proxy-password-if-needed"
  }
}
```

### Proxy Support

For enterprise environments that require proxy configuration:

1. Select option 8 from the main menu
2. Enter your proxy server, port, and credentials if needed
3. Test the connection to ensure it works properly

## Troubleshooting

### Common Issues

1. **Authentication Errors**:
   - Verify your Azure credentials (tenant ID, client ID, secret/certificate)
   - Ensure the certificate is properly installed if using certificate authentication
   - Check that your service principal has the required roles assigned

2. **Permission Errors**:
   - Run the permission diagnostics (option 10) to identify issues
   - Verify role assignments in Azure Portal
   - For file shares, ensure the correct role is assigned (Elevated vs. Privileged)

3. **File Path Issues in File Shares**:
   - Use forward slashes (/) instead of backslashes (\) in file share paths
   - Avoid special characters in file and directory names

4. **Performance Considerations**:
   - For large directories, adjust the parallel processing settings in the Advanced Settings menu
   - Use smaller batch sizes when working with many files
   - For extremely large datasets, consider splitting operations into multiple runs

## Implemented Features

### April 2025 Update

1. ✅ **Enhanced User Interface**  
   - Added professional application icon for better visual identity
   - Implemented ASCII-based spinner animations for improved compatibility with Windows Server environments
   - Redesigned progress indicators with estimated time remaining and processing rates

2. ✅ **Comprehensive Help System**  
   - Added detailed in-app help documentation accessible from the main menu
   - Implemented color-coded help sections for improved readability
   - Included usage tips and best practices for each feature

3. ✅ **Advanced Settings Menu**  
   - Implemented a complete settings configuration system
   - Added customization options for parallel processing, logging levels, and timeouts
   - Created user-friendly dialogs for all configuration options

4. ✅ **Improved File System Handling**  
   - Added robust handling of system-restricted folders (e.g., "System Volume Information")
   - Implemented safe traversal of root directories that prevents permission errors
   - Enhanced error handling for inaccessible files and directories

5. ✅ **Better Windows Server Compatibility**  
   - Optimized UI elements for Windows Server v1809 and newer
   - Fixed thread synchronization issues when displaying dialogs
   - Added fallback mechanisms for directory selection when GUI dialogs fail

6. ✅ **Progress Indicator Improvements**
   - Fixed progress indicator task cancellation to properly terminate after completion
   - Application now correctly returns to the main menu after uploads finish
   - Improved stability during long-running operations

7. ✅ **Enhanced Connection Status Visibility**
   - Added connection status bar to all operations showing storage account, authentication method, and container
   - Consistent visibility of connection state across the entire application
   - Better user awareness of current connection context

8. ✅ **Improved File Integrity Reporting**
   - Enhanced file comparison logic to show matching files on the same report row
   - Files with identical names and hash values now appear together instead of separately
   - More concise and easier-to-understand reports for file integrity verification

## Future Improvements

1. **Multi-platform Support**
   - Extend compatibility to Linux and macOS environments
   - Create platform-specific builds while maintaining a consistent user experience
   - Add Docker container support for cloud-based automation

2. **Enhanced Reporting Features**
   - Add filtering options and search functionality for large reports
   - Implement visualization of report data with charts/graphs
   - Add export options for different formats (JSON, Excel, HTML reports)

3. **Cloud to Cloud Transfer Support**
   - Add ability to copy and verify files between different Azure storage accounts
   - Support checking integrity across different cloud providers (AWS, GCP)
   - Implement delta sync to only transfer changed files

4. **Command-Line Arguments Support**
   - Enable running the tool with command-line arguments for automation scenarios
   - Support CI/CD pipeline integration for automated verification
   - Add batch file processing for unattended operations

## Support and Contributions

For support, please open an issue on the GitHub repository. Contributions are welcome through pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
