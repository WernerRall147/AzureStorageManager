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
