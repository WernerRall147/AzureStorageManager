using System;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AzureStorageManager.Models;
using System.IO;

namespace AzureStorageManager.Utilities
{
    /// <summary>
    /// Helper class for certificate-based authentication with failover capabilities
    /// </summary>
    public class CertificateAuthHelper
    {
        private readonly List<CertificateConfig> _certificates = new();
        private int _currentCertificateIndex = 0;
        private readonly string _tenantId;
        private readonly string _clientId;

        /// <summary>
        /// Creates a new certificate authentication helper
        /// </summary>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <param name="clientId">Client/Application ID</param>
        public CertificateAuthHelper(string tenantId, string clientId)
        {
            _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        }

        /// <summary>
        /// Adds a certificate to the failover chain
        /// </summary>
        /// <param name="certPath">Path to .pfx file</param>
        /// <param name="certPassword">Certificate password</param>
        /// <param name="certName">Display name for the certificate</param>
        public void AddCertificate(string certPath, string certPassword, string certName = "")
        {
            if (string.IsNullOrWhiteSpace(certPath))
                throw new ArgumentException("Certificate path cannot be empty", nameof(certPath));

            if (!File.Exists(certPath))
                throw new FileNotFoundException($"Certificate file not found: {certPath}");

            // Use filename as name if no name provided
            if (string.IsNullOrWhiteSpace(certName))
            {
                certName = Path.GetFileNameWithoutExtension(certPath);
            }

            _certificates.Add(new CertificateConfig
            {
                Path = certPath,
                Password = certPassword,
                Name = certName
            });

            Logger.LogInfo($"Added certificate '{certName}' to authentication chain");
        }

        /// <summary>
        /// Creates a token credential using the current certificate in the failover chain
        /// </summary>
        /// <returns>TokenCredential for Azure authentication</returns>
        public TokenCredential GetCredential()
        {
            if (_certificates.Count == 0)
                throw new InvalidOperationException("No certificates configured for authentication");

            var certConfig = _certificates[_currentCertificateIndex];
            
            try
            {
                X509Certificate2 cert = new X509Certificate2(certConfig.Path, certConfig.Password);
                Logger.LogInfo($"Using certificate: {certConfig.Name} (Index: {_currentCertificateIndex + 1}/{_certificates.Count})");
                
                ClientCertificateCredential credential = new ClientCertificateCredential(
                    _tenantId,
                    _clientId,
                    cert
                );
                
                return credential;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading certificate '{certConfig.Name}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Moves to the next certificate in the failover chain
        /// </summary>
        /// <returns>True if moved to another certificate, False if no more certificates available</returns>
        public bool TryMoveToNextCertificate()
        {
            if (_currentCertificateIndex < _certificates.Count - 1)
            {
                _currentCertificateIndex++;
                Logger.LogInfo($"Switching to next certificate: {_certificates[_currentCertificateIndex].Name}");
                return true;
            }
            
            Logger.LogWarning("No more certificates available in the failover chain");
            return false;
        }

        /// <summary>
        /// Gets the current certificate name
        /// </summary>
        public string GetCurrentCertificateName()
        {
            if (_certificates.Count == 0)
                return "No certificate";

            return _certificates[_currentCertificateIndex].Name;
        }

        /// <summary>
        /// Gets the total number of certificates in the failover chain
        /// </summary>
        public int CertificateCount => _certificates.Count;

        /// <summary>
        /// Configuration for a certificate
        /// </summary>
        private class CertificateConfig
        {
            public string Path { get; set; } = "";
            public string Password { get; set; } = "";
            public string Name { get; set; } = "";
        }
    }
}
