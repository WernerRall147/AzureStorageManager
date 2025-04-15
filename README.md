# Azure Storage Manager Tool

**Azure Storage Manager Tool** is a command-line utility for verifying file integrity between local files and Azure Blob Storage or Azure File Shares. This tool compares MD5 hashes of local files with those stored in Azure, ensuring data accuracy for backups, migrations, and routine integrity checks.

## Features

- **Verify file integrity**: Ensures that files stored in Azure match local copies by comparing MD5 hashes.
- **Supports Azure Blob Storage and Azure File Shares**: Use the tool for different Azure storage types.
- **CSV Report Generation**: Produces a detailed report showing processed files, MD5 hashes, and match statuses.
- **Self-contained executable**: Distribute the tool as a single executable for easy deployment.

## Requirements

- Azure Storage Account Name and Key
- Name of the Blob Container or File Share to be verified
- Local directory path containing the original files for comparison
- **Windows OS** (due to .NET 9.0 Windows compatibility)

## Installation

1. **Clone the Repository**:

   ```bash
   git clone https://github.com/yourusername/azure-storage-manager-tool.git
   cd azure-storage-manager-tool
   
2. **Build and Publish the Application**:

   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
   
3. **Run the Application**:

   Navigate to the `publish` folder and execute `AzureStorageManager.exe`.

   ```bash
   cd publish
   AzureStorageManager.exe
   
## Usage

1. **Start the Application**  
   Run `AzureStorageManager.exe` from the command line.

2. **Follow the Prompts**:
   - **Enter Azure Storage Account Name and Key**: The tool will prompt you for these details to connect to your Azure storage.
   - **Select Storage Type**: Choose between Azure Blob Storage and Azure File Share.
   - **Provide the Container or File Share Name**: Enter the name of the Blob container or File Share you want to verify.
   - **Enter Local Directory Path**: Input the path to the directory containing your original files.

3. **Review the Output**:
   - The tool will process each file, display the MD5 hash comparison, and indicate if the hashes match.
   - A CSV report named `BlobStorageReport.csv` or `FileShareReport.csv` will be generated in the same directory, summarizing the verification results.

## Scenarios for Use

- **After file uploads** to Azure, to confirm all files uploaded correctly.
- **Routine integrity checks** to ensure ongoing data accuracy.
- **Post-migration** validation, for example when moving data within Azure.
- **Disaster recovery or backup validation** to ensure backup files are intact.

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

### Previous Updates

9. ✅ **CSV Report Enhancement**  
   - Added descriptive headers to the CSV report (e.g., `FileName`, `LocalHash`, `RemoteHash`, `Status`)
   - CSV reports now include headers for better readability and data analysis

10. ✅ **User Interface Improvements**  
    - Replaced command-line prompts with GUI pop-ups for directory selection
    - Implemented using Windows Forms dialogs for a more user-friendly experience
    - Added clear prompts for user interaction with "Press Enter to return to the main menu..."

11. ✅ **Enhanced Azure Integration**  
    - Added functionality to verify if the MD5 hash is present in Azure metadata
    - Tool can now update metadata with correct hashes when missing or incorrect
    - Implemented a consolidated report feature for entire directory operations

12. ✅ **File Transfer Capabilities**  
    - Added the ability to copy files to Azure and download files from Azure
    - Integrated verification features with file transfer capabilities
    - Reports now include comprehensive details of all files in a single document

13. ✅ **Comprehensive Logging System**  
    - Implemented logging functionality with a dedicated "View Logs" menu option
    - Provided better feedback during operations and comprehensive error reporting
    - Improved command-line interface with better contextual feedback for users

### Future Improvements

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

5. **Performance Optimizations**
   - Implement chunked file processing for extremely large datasets
   - Add resumable operations for interrupted transfers or verifications
   - Optimize memory usage for resource-constrained environments

6. **Enhanced Security Features**
   - Add support for Azure Key Vault integration for secure credential management
   - Implement role-based access control for multi-user environments
   - Add encryption options for sensitive local data

7. **UI Enhancements**
   - Create a simple graphical user interface option
   - Add a web-based dashboard for monitoring long-running operations
   - Implement real-time notifications for completed operations

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contributing

If you'd like to contribute, please fork the repository and make a pull request. For major changes, please open an issue first to discuss what you would like to change.
