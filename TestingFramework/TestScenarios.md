# Azure Storage Manager Test Scenarios

## Testing Framework Overview
This document outlines test scenarios for verifying Azure Storage Manager functionality on Windows Server 2016.

## Test Environment
- **Server OS**: Windows Server 2016
- **Server Specs**: Standard_B2s running in SouthAfricaNorth Azure
- **Azure Storage Account Type**: Locally-redundant storage (LRS), StorageV2 (general purpose v2) Files and Blob
- **Network Conditions**: Direct Internet with NSG

## Test Execution Guide

### Before Each Test
1. Record start time 10:
2. Note system resource usage (memory, CPU) before test
3. Clear previous logs (if applicable)
4. Document specific test parameters (file sizes, counts, etc.)

### After Each Test
1. Record end time and duration
2. Save log files with descriptive names (include scenario ID)
3. Take screenshots of any errors or unexpected behaviors
4. Note system resource usage after test
5. Record observations and results in this document

## Test Scenarios

### Category 1: Basic Functionality

#### Scenario 1.1: File Integrity Verification - Small Set
- **Description**: Verify integrity of a small set of files (50-100 files, <10MB each)
- **Steps**:
  1. Select option 1 (Verify File Integrity)
  2. Choose Blob Storage
  3. Select a directory with 50-100 small files
  4. Complete verification
- **Expected Result**: All files verified, report generated correctly
- **Observations**: Run 1: There are still two report files being created called 
[WORKING] / Verifying files (00:01)      CSV report saved to: C:\Users\labadmin\Desktop\FullReport_20250410103456.csv
CSV report saved to: C:\Users\labadmin\Desktop\MismatchesOnlyReport_20250410103456.csv
Verification complete.
Full report saved to FullReport_20250410103456.csv
Mismatches-only report saved to MismatchesOnlyReport_20250410103456.csv

Example:
FileName,LocalHash,RemoteHash,Status
1phfjmkufez.AF,,adb19faf568536c2dbfc743fd76fca49,LocalFileMissing

Which is fine but maybe we can create a better single report with more descriptive headers that shows which files are localcomputer and which are cloud. One single better report than multiple small ones. 

Other feedback: 
Maybe it show green text when the command is complete. Easier to see if the process is done. The spinner is working well thanks!

Run 2: After test Number 2 for Scenario 1, all of the above seems to be addressed. The app flows much better. Maybe a small change it doesnt have to return the entire blob storage path, it can maybe say AzureName and LocalFileName or a similar or better way to distinguish in the report. Everything else felt great

Run 3: Great. The app is working as expected. We only need a small fine tuning done on the report section AzureStorageReport. I can see it lists the Azure Files very well. I see its not listing the files that are local. It should show the cloud files as well as the local files

Run 4: I am very happy with the testing we just need to make one final change. The current AzureStorageReport shows 'Local' even when a file actually exists in azure. This column should reflect local or cloud. 

- **Log Files**: 
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

#### Scenario 1.2: File Integrity Verification - Large Set
- **Description**: Verify integrity of a large set of files (1000+ files)
- **Steps**:
  1. Select option 1 (Verify File Integrity)
  2. Choose Blob Storage
  3. Select a directory with 1000+ files
  4. Complete verification
- **Expected Result**: All files verified, report generated correctly
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

#### Scenario 1.3: File Integrity Verification - Large Files
- **Description**: Verify integrity of very large files (>1GB each)
- **Steps**:
  1. Select option 1 (Verify File Integrity)
  2. Choose Blob Storage
  3. Select a directory with files >1GB each
  4. Complete verification
- **Expected Result**: All files verified, report generated correctly
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

### Category 2: System Folder Handling

#### Scenario 2.1: Root Directory Scan
- **Description**: Test scanning a root directory (e.g., E:\)
- **Steps**:
  1. Select option 1 (Verify File Integrity)
  2. Choose Blob Storage
  3. Select a root directory (E:\)
  4. Complete verification
- **Expected Result**: System folders skipped, accessible files processed
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

### Category 3: File Transfer Operations

#### Scenario 3.1: Upload to Azure - Mixed Content
- **Description**: Upload a directory with mixed file types to Azure
- **Steps**:
  1. Select option 4 (Copy Files to Azure)
  2. Choose Blob Storage
  3. Select a directory with mixed file types
  4. Complete upload
- **Expected Result**: All files uploaded with proper MD5 hashes
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

#### Scenario 3.2: Download from Azure
- **Description**: Download files from Azure to local directory
- **Steps**:
  1. Select option 5 (Download Files from Azure)
  2. Choose Blob Storage
  3. Specify container and local directory
  4. Complete download
- **Expected Result**: All files downloaded and verified
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

### Category 4: MD5 Hash Operations

#### Scenario 4.1: Generate MD5 Hashes - Large Directory
- **Description**: Generate MD5 hashes for a large directory
- **Steps**:
  1. Select option 2 (Generate MD5 Hashes)
  2. Select a directory with many files
  3. Complete hash generation
- **Expected Result**: MD5 hashes generated for all files
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

#### Scenario 4.2: Update Azure Metadata
- **Description**: Check and update Azure metadata with MD5 hashes
- **Steps**:
  1. Select option 3 (Check and Update Azure Metadata)
  2. Choose Blob Storage
  3. Specify container
  4. Complete metadata update
- **Expected Result**: All blob metadata updated with correct MD5 hashes
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

### Category 5: Error Handling

#### Scenario 5.1: Network Interruption
- **Description**: Test application behavior during network interruption
- **Steps**:
  1. Start a large file verification or upload
  2. Temporarily disable network connectivity
  3. Restore connectivity
  4. Observe how application handles the interruption
- **Expected Result**: Graceful error handling, clear error messages
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

#### Scenario 5.2: Invalid Credentials
- **Description**: Test application behavior with invalid Azure credentials
- **Steps**:
  1. Modify appsettings.json with invalid credentials
  2. Attempt to connect to Azure
- **Expected Result**: Clear error message, no application crash
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

### Category 6: User Interface

#### Scenario 6.1: Progress Indicator Consistency
- **Description**: Verify spinner animation displays correctly on Server 2016
- **Steps**:
  1. Perform operations that show the spinner animation
  2. Observe spinner appearance
- **Expected Result**: ASCII spinner (|, /, -, \) displays properly
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

#### Scenario 6.2: Help and Settings Menu
- **Description**: Verify help and settings menus display correctly
- **Steps**:
  1. Navigate to Help menu
  2. Navigate to Settings menu
- **Expected Result**: Menus display correctly with all options
- **Observations**: [To be filled after testing]
- **Log Files**: [List relevant log files]
- **Status**: ⬜ Not Started / ✅ Passed / ❌ Failed

## Test Results Summary
[To be filled after completing testing]

### Passed Scenarios: 0
### Failed Scenarios: 0
### Not Tested: 12

## Key Issues Identified
[To be filled after completing testing]

## Recommendations
[To be filled after completing testing]
