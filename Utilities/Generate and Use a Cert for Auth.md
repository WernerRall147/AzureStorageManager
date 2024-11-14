# Certificate Authentication Setup Guide

## Step 1: Generate the Certificate

### Generate a Self-Signed Certificate
You can generate a self-signed certificate using PowerShell:

```powershell
New-SelfSignedCertificate -CertStoreLocation "Cert:\CurrentUser\My" -Subject "CN=AzureAuthCertificate"
```
This command will create a certificate and place it in the **My** store under **CurrentUser**.

### Export the Certificate
After generating the certificate, you will need to export it to use it in Azure and in your application.

1. Open **Certificate Manager** (`certmgr.msc`) on your machine.
2. Navigate to **Personal > Certificates**, locate the newly created certificate (`AzureAuthCertificate`), and **export it twice**:
   - Export **public key** as a `.cer` file (no password).
   - Export **private key** as a `.pfx` file (with a password). This will be used in your application.

## Step 2: Upload the Certificate to Azure Active Directory

### Go to Azure Active Directory
In the Azure Portal, navigate to **Azure Active Directory**.

### Register Your Application
If you haven't already, **register an application** in AAD that represents your service.

1. Go to **App registrations** and click **New registration**.
2. Provide a name, select the appropriate **supported account types**, and register the app.

### Upload the Public Key (.cer) to Your Application
1. Once your app is registered, navigate to **Certificates & secrets** in your app registration.
2. Click **Upload certificate**, then upload the `.cer` file that you exported earlier.
3. This allows Azure to use the certificate to authenticate requests from your application.

## Step 3: Set Up the Application for Certificate Authentication

### Update Your Application
Make sure the `Program.cs` is updated to load the certificate for authentication. Use the `ClientCertificateCredential`:

```csharp
var certificate = X509Certificate2.CreateFromEncryptedPemFile(certificatePath, certificatePassword);
var clientCertificateCredential = new ClientCertificateCredential(tenantId, clientId, certificate);
```

Ensure the following values are correctly set in your application:
- **`tenantId`**: Directory (tenant) ID of your Azure AD.
- **`clientId`**: Application (client) ID of your registered app.
- **`certificatePath`**: Path to the `.pfx` file (private key).
- **`certificatePassword`**: Password used to protect the `.pfx` file.

### Store the Certificate Securely on the Machine Running the App
The `.pfx` file (which contains the private key) must be available to the machine running the application.
Store it in a secure location, such as a secure directory with restricted access.

## Step 4: Assign API Permissions

### API Permissions
1. In the Azure Portal, navigate to your **app registration**.
2. Under **API permissions**, ensure that your app has the correct permissions to access **Azure Storage**.
3. Add **delegated** or **application** permissions as needed. Typically, for accessing Azure resources like Blob or File Share, you need to add **Azure Storage > user_impersonation** permissions.

### Grant Admin Consent
If your application requires it, you may need to **grant admin consent** for the required permissions.
This can be done directly from the **API permissions** page.

## Step 5: Assign Role to Managed Identity in Azure Storage

### Assign Azure Role to Your Application
Your Azure application needs to have the correct **RBAC role** assigned for accessing Azure Storage.

1. Navigate to your **Storage Account** in Azure.
2. Click on **Access Control (IAM)**, then **Add role assignment**.
3. Assign the **Storage Blob Data Contributor** or **Storage File Data Contributor** role to the **App Registration**.
4. Ensure that this is done for the correct **scope** (e.g., specific containers or file shares if you want to limit access).

## Step 6: Test the Application

### Run the Application on Your Local Machine
- Ensure the `.pfx` certificate is accessible to the application and the **certificate path** and **password** are configured correctly.
- **Run the application** and verify that it can authenticate to Azure and access the storage.

### Common Issues to Watch For
- **Permission Errors**: If your application receives `403 Forbidden` errors, verify the **permissions** assigned to your Azure AD app and ensure that **admin consent** is granted.
- **Certificate Loading Errors**: Ensure the **certificate path** is correct and that the password matches the one used during the `.pfx` export.

## Summary
1. **Generate and Export the Certificate** (`.cer` and `.pfx`).
2. **Upload the Public Certificate** (`.cer`) to **Azure AD**.
3. **Update the Application Code** to use the certificate.
4. **Assign API Permissions** to the app registration and grant admin consent.
5. **Assign Role-Based Access** in the Azure Storage Account.
6. **Run and Test** the application.

Following these steps should help you successfully set up the Certificate Authentication version of your application. Let me know if you have any issues during the setup!