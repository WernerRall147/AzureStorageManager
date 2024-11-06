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
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
   
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

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contributing

If you'd like to contribute, please fork the repository and make a pull request. For major changes, please open an issue first to discuss what you would like to change.
