using Azure.Core;
using Azure.Identity;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AzureStorageManager.Utilities
{
    public static class CertificateAuthHelper
    {
        /// <summary>
        /// Creates a TokenCredential using a certificate for authentication
        /// </summary>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <param name="clientId">Application (client) ID</param>
        /// <param name="certificateThumbprint">Thumbprint of the certificate to use</param>
        /// <returns>TokenCredential for Azure authentication</returns>
        public static TokenCredential CreateCertificateCredential(string tenantId, string clientId, string certificateThumbprint)
        {
            try
            {
                // Find certificate by thumbprint in the certificate store
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                
                // Find certificate by thumbprint
                var certificateCollection = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    certificateThumbprint,
                    false);
                
                store.Close();
                
                if (certificateCollection.Count == 0)
                {
                    throw new InvalidOperationException($"Certificate with thumbprint {certificateThumbprint} not found in the CurrentUser\\My store.");
                }
                
                var certificate = certificateCollection[0];
                
                // Create credential using the certificate
                var credential = new ClientCertificateCredential(
                    tenantId,
                    clientId,
                    certificate);
                
                return credential;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to create certificate credential: {ex.Message}");
                Console.ResetColor();
                
                throw;
            }
        }
        
        /// <summary>
        /// Creates a TokenCredential using a certificate file for authentication
        /// </summary>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <param name="clientId">Application (client) ID</param>
        /// <param name="certificatePath">Path to the certificate file (.pfx)</param>
        /// <param name="certificatePassword">Password for the certificate file</param>
        /// <returns>TokenCredential for Azure authentication</returns>
        public static TokenCredential CreateCertificateCredentialFromFile(string tenantId, string clientId, string certificatePath, string certificatePassword)
        {
            try
            {
                if (!File.Exists(certificatePath))
                {
                    throw new FileNotFoundException($"Certificate file not found: {certificatePath}");
                }
                  // Load certificate for authentication
                X509Certificate2 certificate;
                
                try {
                    // Modern approach for .NET 9.0 using X509Certificate2 constructor directly with byte array
                    byte[] certBytes = File.ReadAllBytes(certificatePath);
                    certificate = new X509Certificate2(certBytes, certificatePassword, 
                        X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                }
                catch (Exception certEx)
                {
                    throw new InvalidOperationException($"Failed to load certificate from file: {certEx.Message}", certEx);
                }
                
                // Create credential using the certificate
                var credential = new ClientCertificateCredential(
                    tenantId,
                    clientId,
                    certificate);
                
                return credential;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to create certificate credential from file: {ex.Message}");
                Console.ResetColor();
                
                throw;
            }
        }
    }
}
