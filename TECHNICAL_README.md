# Azure Storage Manager Tool - Technical Documentation

## Project Overview
Azure Storage Manager is a .NET-based command-line utility for verifying file integrity between local files and Azure Blob Storage or Azure File Shares. The application is designed to run on Windows OS and provides an interface for file operations, integrity checks, and related services.

## Technical Specifications

### Framework and Runtime
- **Framework**: .NET 9.0 (Windows compatibility)
- **Target Platform**: Windows x64
- **Deployment Method**: Self-contained, single-file executable
- **UI Framework**: Console application with Windows Forms integration for file dialogs

### Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| Azure.Storage.Blobs | Latest* | Azure Blob Storage operations |
| Azure.Storage.Files.Shares | Latest* | Azure File Share operations |
| Azure.Identity | Latest* | Authentication with Azure services |
| System.CommandLine | Latest* | Command-line parsing and interaction |
| Newtonsoft.Json | Latest* | Configuration file handling and JSON serialization |

*Version numbers to be updated with each release

### Compilation and Build Options
- **Release Configuration**: Self-contained, single executable deployment
- **Build Command**: `dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true`
- **Target Runtime**: win-x64

## Architecture

### Core Components

#### Program.cs
Entry point for the application. Handles the main menu, user interaction flow, and orchestrates calls to the various services.

#### Services/
- **BlobStorageService.cs**: Handles all Azure Blob Storage operations, including connection, listing, downloading, uploading, and hash verification.
- **FileShareService.cs**: Manages Azure File Share operations, including authentication, file listing, downloading, uploading, and permission diagnostics.
- **ProxyService.cs**: Controls proxy configuration and management for network connections.

#### Models/
- **ConnectionState.cs**: Manages the state of the Azure connection, including account information and authentication status.
- **FileMetadata.cs**: Data structure for file information including names, paths, hashes, and status.
- **Range.cs**: Handles file ranges for chunk-based operations.
- **ShareDirectoryItem.cs**: Represents items within an Azure File Share directory.

#### Utilities/
- **AzureErrorHandler.cs**: Centralized error handling for Azure-specific exceptions.
- **CertificateAuthHelper.cs**: Helper for certificate-based authentication.
- **ConnectivityTester.cs**: Tests connectivity to Azure services.
- **CsvExporter.cs**: Handles CSV report generation.
- **FileHashUtility.cs**: Calculates and manages MD5 hashes for files.
- **FileRequestIntentPolicy.cs**: Manages file request intent policy for Azure operations.
- **FileShareClientExtensions.cs**: Provides extensions for Azure File Share operations to ensure proper header inclusion.
- **GlobalProxyInitializer.cs**: Initializes proxy settings globally.
- **Logger.cs**: Centralized logging system.
- **ProgressIndicator.cs**: Handles UI progress indications for long-running operations.
- **ProxyConfigHelper.cs**: Helper for proxy configuration.

### Azure File Share Header Requirements and Permission Handling

As of 2025, Azure File Share operations require the inclusion of the `x-ms-file-request-intent` header. The application includes several specialized components to handle this requirement:

1. **FileRequestIntentPolicy.cs**
   - A custom `HttpPipelinePolicy` implementation that adds the required header to all Azure File Share requests
   - Intercepts HTTP requests in the Azure SDK pipeline before they're sent to Azure
   - Adds `x-ms-file-request-intent: backup` header to each request
   - Includes duplicate header checking to prevent `backup, backup` errors

2. **FileShareClientExtensions.cs**
   - Extension methods that ensure the proper header inclusion for specific operations
   - Includes specialized methods for:
     - Share existence checks (`ExistsWithIntentHeaderAsync`)
     - Directory creation (`CreateDirectoryWithIntentHeaderAsync`)
     - File creation (`CreateFileWithIntentHeaderAsync`)
   - Provides fallback mechanisms to handle authorization errors
   - Uses multi-layered verification to accurately determine file share existence

3. **Permission Diagnostics Implementation**
   - The `DiagnosePermissionsAsync` method in `FileShareService.cs` performs comprehensive tests to identify which operations are failing
   - Tests include: share existence, directory creation, file upload/download, and deletion
   - Provides specific role recommendations based on test results
   - Offers to create missing file shares during diagnostics for more complete testing

#### Permission Error Resolution Approach

The application uses a multi-layered approach to resolve permission issues:

1. **Pipeline Policy Level**: Adds the header at the HTTP request pipeline level with duplicate protection
2. **Extension Methods**: Provides specialized methods for operations that might not correctly pick up the pipeline policy
3. **Fallback Mechanisms**: When certain operations fail with permission errors, the application tries alternative approaches
4. **Enhanced Existence Checking**: Uses multiple methods to verify if a file share exists, ensuring accuracy even with limited permissions

### Certificate Authentication

### Configuration
The application uses `appsettings.json` for storing configuration:

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

## Authentication Mechanisms

### Supported Authentication Methods
1. **Azure Storage Account Key**
   - Direct authentication using the storage account name and key
   - Used for simple scenarios where service principal is not required

2. **Service Principal with Certificate**
   - Uses application ID (client ID) and certificate thumbprint
   - Certificate must be installed in the local machine's certificate store
   - More secure than client secret authentication
   - Configuration in `appsettings.json` under the Azure section

3. **Service Principal with Client Secret**
   - Uses application ID (client ID) and client secret
   - Less secure than certificate authentication but simpler to set up
   - Configuration in `appsettings.json` under the Azure section

### Certificate Authentication
- Self-signed certificates can be generated using the `SelfSignedCert.ps1` script
- Certificates need to be uploaded to the app registration in Azure Portal
- The thumbprint must be configured in the `appsettings.json` file

## Azure IAM Permissions

### Required Permissions
- **For Blob Storage operations**: `Storage Blob Data Contributor` role
- **For File Share operations**: One of the following roles:
  - `Storage File Data SMB Share Elevated Contributor` (recommended)
  - `Storage File Data Privileged Contributor` (for permission override scenarios)

### Permission Diagnostics
- The application includes tools for diagnosing permission issues in `RunFileSharePermissionDiagnostics.cs`
- Built-in diagnostics check for required permissions before attempting operations

## Network Configuration

### Proxy Support
- The application supports enterprise proxy environments
- Proxy settings can be configured in `appsettings.json` or through interactive prompts
- Settings include proxy URL, port, username, and password
- All network requests are routed through the proxy if enabled

### Connection Testing
- `ConnectivityTester.cs` provides functionality to test connections to Azure services
- Validates connectivity before attempting operations to provide clear error messages

## File Operations

### Hash Verification
- MD5 hash calculation is performed by `FileHashUtility.cs`
- Compares local file hashes with Azure storage metadata
- Can update metadata when hashes are missing or incorrect

### Chunked File Processing
- Large files are processed in chunks to optimize memory usage
- Chunk size is configurable through the advanced settings menu

### Parallel Processing
- Multi-threaded operations for improved performance
- Thread count is adjustable through the application settings

## Logging System

### Log Levels
- **Error**: Critical issues that require attention
- **Warning**: Potential issues that don't prevent operation
- **Info**: General information about operations
- **Debug**: Detailed information for troubleshooting
- **Verbose**: Maximum detail level including low-level operations

### Log Storage
- Logs are stored in a `Logs` directory in the application folder
- Log files follow the naming convention: `AzureStorageManager_YYYYMMDD.log`
- Log rotation occurs daily to prevent excessive file sizes

## Testing Framework

### Test Scenarios
- Comprehensive test scenarios are documented in `TestScenarios.md`
- Test data is stored in the `TestingFramework/Tests/` directory
- Test results from previous runs are preserved for regression testing

### Log Collection
- `LogCollector.ps1` script collects logs and diagnostic information for troubleshooting

## Known Issues and Limitations

### File Share Specific Issues
- File Share path handling requires special attention due to Azure API limitations
- A patch for this issue is available in `FileSharePathPatch.txt`
- Large directories (>10,000 files) may experience performance degradation

### Blob Storage Specific Issues
- Blob names with certain special characters require careful handling
- Performance may degrade with extremely large containers

### General Limitations
- Limited to Windows OS due to .NET 9.0 Windows compatibility dependencies
- No GUI interface for advanced operations
- No cloud-to-cloud transfer capabilities (yet)

## Security Considerations

### Credential Storage
- Service principal credentials should be protected
- Client secrets are stored in plain text in `appsettings.json` (a future improvement is to use Azure Key Vault)
- Certificate-based authentication is recommended over client secret

### Proxy Authentication
- Proxy credentials are stored in plain text in `appsettings.json`
- Future plans include encrypted storage for sensitive credentials

## Design Decisions

### Single Executable Deployment
- Decision to package as a single executable improves deployment simplicity
- Trade-off: Larger executable size but no dependency management issues

### Platform Limitation
- Decision to target Windows-only initially was made to leverage specific Windows features
- Future cross-platform support is planned as noted in the future improvements section

### Console UI with Windows Forms Dialogs
- Hybrid approach combines the simplicity of console applications with the user-friendliness of GUI file dialogs
- Trade-off: Requires Windows Forms compatibility but improves user experience

### MD5 Hash Verification
- MD5 was chosen for compatibility with Azure Storage which uses MD5 natively
- Trade-off: MD5 is not collision-resistant but serves adequately for file integrity verification in this context

## Future Improvements

### Multi-platform Support
- Extend compatibility to Linux and macOS environments
- Create platform-specific builds while maintaining a consistent user experience
- Add Docker container support for cloud-based automation

### Enhanced Reporting Features
- Add filtering options and search functionality for large reports
- Implement visualization of report data with charts/graphs
- Add export options for different formats (JSON, Excel, HTML reports)

### Cloud to Cloud Transfer Support
- Add ability to copy and verify files between different Azure storage accounts
- Support checking integrity across different cloud providers (AWS, GCP)
- Implement delta sync to only transfer changed files

### Command-Line Arguments Support
- Enable running the tool with command-line arguments for automation scenarios
- Support CI/CD pipeline integration for automated verification
- Add batch file processing for unattended operations

### Performance Optimizations
- Implement chunked file processing for extremely large datasets
- Add resumable operations for interrupted transfers or verifications
- Optimize memory usage for resource-constrained environments

### Enhanced Security Features
- Add support for Azure Key Vault integration for secure credential management
- Implement role-based access control for multi-user environments
- Add encryption options for sensitive local data

### UI Enhancements
- Create a simple graphical user interface option
- Add a web-based dashboard for monitoring long-running operations
- Implement real-time notifications for completed operations

## Troubleshooting Guide

### Common Issues and Solutions

#### Authentication Failures
- **Issue**: "Authentication failed" error messages
- **Solution**: 
  1. Verify tenant ID, client ID, and certificate thumbprint/client secret in `appsettings.json`
  2. Ensure the certificate is installed correctly in the certificate store
  3. Confirm that the service principal has the required roles assigned

#### Proxy Configuration Issues
- **Issue**: "Unable to connect to Azure" with proxy enabled
- **Solution**:
  1. Verify proxy URL and port in settings
  2. Test proxy connectivity with `ConnectivityTestingUtility.cs`
  3. Ensure proxy credentials are correct if authentication is required

#### Permission Denied Errors
- **Issue**: "Access denied" errors when accessing Azure resources
- **Solution**:
  1. Run the permission diagnostics with `RunFileSharePermissionDiagnostics.cs`
  2. Verify that the authenticated identity has the required roles
  3. For file shares, check if the proper role is assigned (Elevated vs. Privileged)

#### File Path Issues in File Shares
- **Issue**: Incorrect file paths when working with nested directories
- **Solution**:
  1. Apply the patch from `FileSharePathPatch.txt`
  2. Use forward slashes (/) instead of backslashes (\) in file share paths
  3. Avoid special characters in file and directory names

#### Performance Issues
- **Issue**: Slow performance with large directories
- **Solution**:
  1. Adjust the parallel processing settings in the Advanced Settings menu
  2. Use smaller batch sizes for large directories
  3. Consider splitting operations into multiple runs for extremely large datasets

## Developer Notes and Setup

### Development Environment Setup
1. Install Visual Studio 2022 or later with .NET 9.0 SDK
2. Clone the repository from GitHub
3. Configure Azure resources as described in the Azure Setup section
4. Set up a test storage account with test containers and file shares

### Build Process
1. **Debug Build**: Use Visual Studio or run `dotnet build` in the project directory
2. **Release Build**: Use the publish command mentioned earlier for production builds

### Testing Guidelines
1. Follow test scenarios in `TestScenarios.md`
2. Test with various file sizes and directory structures
3. Verify proper error handling by intentionally causing errors
4. Test proxy configuration if applicable to your environment

### Release Process
1. Update version information
2. Run all tests and verify functionality
3. Build the release version
4. Package with necessary documentation
5. Update the GitHub repository with release notes

## Feedback from Development Process

### User Experience Improvements
- The addition of Windows Forms dialogs for file selection was well-received
- Progress indicators with estimated time remaining were appreciated
- A consistent menu system improved navigation

### Technical Implementation Feedback
- Certificate authentication was preferred over client secret for production use
- The hybrid console/GUI approach worked well for the target audience
- Parallel processing significantly improved performance for large directories

### Optimization Suggestions
- Further optimization of memory usage for large file operations
- Consider alternative hashing algorithms for non-Azure storage targets
- Explore performance improvements for very deep directory structures

## Version History

### v1.0.0 (Initial Release)
- Basic functionality for file integrity verification
- Support for Azure Blob Storage and File Shares
- CSV report generation

### v1.1.0
- Added proxy support for enterprise environments
- Improved error handling and logging
- Enhanced CSV reports with headers

### v1.2.0
- Added Windows Forms dialogs for directory selection
- Implemented progress indicators with ETA
- Added advanced settings menu

### v1.3.0 (Latest)
- Enhanced user interface with ASCII-based spinners
- Added comprehensive in-app help system
- Improved file system handling for system-restricted folders
- Added connection status visibility
- Enhanced file integrity reporting

## Microsoft Documentation References

### Azure Storage SDK Documentation

#### Azure.Storage.Blobs
- **Package Documentation**: [Azure.Storage.Blobs NuGet Package](https://www.nuget.org/packages/Azure.Storage.Blobs/)
- **Client Library Introduction**: [Azure Storage Blobs client library for .NET](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/storage.blobs-readme)
- **API Reference**: [Azure.Storage.Blobs Namespace](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.blobs)
- **Authentication Methods**: [Authenticate with the Azure Storage client library](https://learn.microsoft.com/en-us/azure/storage/common/storage-auth-aad-msi)
- **Blob Storage Concepts**: [Azure Blob Storage concepts](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blobs-introduction)
- **MD5 Hash Verification**: [Verify Content Integrity](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blob-properties-metadata)

#### Azure.Storage.Files.Shares 
- **Package Documentation**: [Azure.Storage.Files.Shares NuGet Package](https://www.nuget.org/packages/Azure.Storage.Files.Shares/)
- **Client Library Introduction**: [Azure Storage Files client library for .NET](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/storage.files.shares-readme)
- **API Reference**: [Azure.Storage.Files.Shares Namespace](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.files.shares)
- **File Share Concepts**: [Azure Files introduction](https://learn.microsoft.com/en-us/azure/storage/files/storage-files-introduction)
- **SMB Protocol Support**: [SMB protocol support](https://learn.microsoft.com/en-us/azure/storage/files/storage-files-smb-protocol)
- **File Share Permissions**: [Azure Files identity-based authentication](https://learn.microsoft.com/en-us/azure/storage/files/storage-files-identity-auth-active-directory-enable)

#### Azure.Identity
- **Package Documentation**: [Azure.Identity NuGet Package](https://www.nuget.org/packages/Azure.Identity/)
- **Client Library Introduction**: [Azure Identity client library for .NET](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- **API Reference**: [Azure.Identity Namespace](https://learn.microsoft.com/en-us/dotnet/api/azure.identity)
- **Authentication Flows**: [Authentication in Azure Identity](https://learn.microsoft.com/en-us/azure/developer/dotnet/sdk/authentication)
- **Service Principal Authentication**: [Create a service principal](https://learn.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal)
- **Certificate-based Authentication**: [Certificate-based authentication](https://learn.microsoft.com/en-us/azure/active-directory/develop/howto-authenticate-service-principal-certificate)

### Azure Storage Feature References

#### Access Control and Authentication
- **Azure RBAC for Blob Data**: [Authorization paths for using Azure RBAC](https://learn.microsoft.com/en-us/azure/storage/blobs/authorize-data-operations-portal)
- **Role Assignments**: [Grant access to Azure blob and queue data with RBAC](https://learn.microsoft.com/en-us/azure/storage/common/storage-auth-aad-rbac-portal)
- **Built-in Roles**: [Azure built-in roles for blob and queue data operations](https://learn.microsoft.com/en-us/azure/storage/common/storage-auth-aad-rbac-portal#azure-built-in-roles-for-blobs-and-queues)
- **SMB access control**: [Azure Files identity-based authentication support for SMB access](https://learn.microsoft.com/en-us/azure/storage/files/storage-files-identity-ad-ds-enable)

#### Performance Optimization
- **Scalability Targets**: [Azure Storage scalability and performance targets](https://learn.microsoft.com/en-us/azure/storage/common/scalability-targets-standard-account)
- **Performance Optimization**: [Blob storage performance optimization](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-performance-checklist)
- **File Shares Performance**: [Azure Files performance optimization](https://learn.microsoft.com/en-us/azure/storage/files/storage-files-scale-targets)
- **Throughput Handling**: [Managing throughput for Azure Storage](https://learn.microsoft.com/en-us/azure/storage/common/storage-scalability-targets)
- **Concurrent Operations**: [Handling concurrent access in blob storage](https://learn.microsoft.com/en-us/azure/storage/blobs/concurrency-manage)

#### Error Handling
- **Storage Error Codes**: [Azure Storage error codes](https://learn.microsoft.com/en-us/rest/api/storageservices/common-rest-api-error-codes)
- **Exception handling**: [Handle errors in Azure .NET applications](https://learn.microsoft.com/en-us/dotnet/azure/sdk/azure-sdk-get-started-error-handling)
- **Retry policies**: [Retry pattern - Cloud Design Patterns](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry)
- **Diagnosing issues**: [Azure Storage diagnostics](https://learn.microsoft.com/en-us/azure/storage/common/storage-monitoring-diagnosing-troubleshooting)

#### Security Best Practices
- **Storage Security**: [Azure Storage security guide](https://learn.microsoft.com/en-us/azure/storage/blobs/security-recommendations)
- **Data Encryption**: [Azure Storage encryption for data at rest](https://learn.microsoft.com/en-us/azure/storage/common/storage-service-encryption)
- **Secure Transfer**: [Require secure transfer](https://learn.microsoft.com/en-us/azure/storage/common/storage-require-secure-transfer)
- **Data Protection**: [Azure Storage data protection](https://learn.microsoft.com/en-us/azure/storage/common/storage-introduction#protecting-your-data)

#### Networking
- **Network Security**: [Configure Azure Storage firewalls and virtual networks](https://learn.microsoft.com/en-us/azure/storage/common/storage-network-security)
- **Private Endpoints**: [Use private endpoints for Azure Storage](https://learn.microsoft.com/en-us/azure/storage/common/storage-private-endpoints)
- **Routing Preferences**: [Configure routing preference for Azure Storage](https://learn.microsoft.com/en-us/azure/storage/common/configure-routing-preference)
