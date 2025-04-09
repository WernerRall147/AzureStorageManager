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

### Recently Implemented

1. ✅ **Headers for the CSV File**  
   - Added descriptive headers to the CSV report (e.g., `FileName`, `LocalHash`, `RemoteHash`, `Status`).
   - CSV reports now include headers for better readability and data analysis.

2. ✅ **Pop-ups for User Input**  
   - Replaced command-line prompts with GUI pop-ups for directory selection.
   - Implemented using Windows Forms dialogs for a more user-friendly experience.

3. ✅ **Check if MD5 is Written to Azure**  
   - Added functionality to verify if the MD5 hash is present in Azure metadata.
   - Tool can now update metadata with correct hashes when missing or incorrect.

4. ✅ **"Enter to Exit" with Completion Message**  
   - Added clear prompts for user interaction with "Press Enter to return to the main menu..."
   - Improved command-line interface with better feedback for users.

5. ✅ **One Report for the Entire Directory**  
   - Implemented a consolidated report feature for entire directory operations.
   - Reports now include comprehensive details of all files in a single document.

6. ✅ **V2: Copy Functionality**  
   - Added the ability to copy files to Azure and download files from Azure.
   - Integrated verification features with file transfer capabilities.

7. ✅ **Verbose Output and Logging**  
   - Implemented logging functionality with a dedicated "View Logs" menu option.
   - Provided better feedback during operations and comprehensive error reporting.

### Future Improvements

1. **Simplify Backslash/Forward Slash in User Guide**  
   - Update the documentation to clarify path formats for Windows (`\`) and Unix-like systems (`/`).  
   - Add examples for both formats to avoid confusion.
   
2. **Enhanced Reporting Features**
   - Add filtering options and search functionality to large reports.
   - Implement visualization of report data (charts/graphs).

3. **Cloud to Cloud Transfer Support**
   - Add ability to copy and verify files between different Azure storage accounts.
   - Support checking integrity across different cloud providers.

4. **Command-Line Arguments Support**
   - Enable running the tool with command-line arguments for automation scenarios.
   - Support CI/CD pipeline integration for automated verification.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contributing

If you'd like to contribute, please fork the repository and make a pull request. For major changes, please open an issue first to discuss what you would like to change.
