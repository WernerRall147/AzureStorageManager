<#Step 1: Generate a Certificate
Generate a Self-Signed Certificate:

1. You can generate a self-signed certificate using PowerShell:

New-SelfSignedCertificate -CertStoreLocation "Cert:\CurrentUser\My" -Subject "CN=AzureAuthCertificate"

2. Export this certificate as a .pfx file for use in your application.
3. Upload the Certificate to Azure AD:
4. In the Azure Portal, navigate to Azure Active Directory > App registrations.
5. Select your App Registration and go to Certificates & Secrets.
6. Upload the public key of the certificate (the .cer file).#>

New-SelfSignedCertificate -CertStoreLocation "Cert:\CurrentUser\My" -Subject "CN=AzureAuthCertificate"
