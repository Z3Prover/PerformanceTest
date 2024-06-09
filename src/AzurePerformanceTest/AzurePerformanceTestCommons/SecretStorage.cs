
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public class SecretStorage
    {
        SecretClient secretClient;
        string keyVaultUrl;
        Dictionary<string, string> cache = new Dictionary<string, string>();

        public SecretStorage(string AADAppId, string certificateThumprint, string keyVaultUrl)
        {
            this.keyVaultUrl = keyVaultUrl;

            System.Console.WriteLine("App ID: " + AADAppId);
            System.Console.WriteLine("Thumbprint: " + certificateThumprint);
            var certificate = FindCertificateByThumbprint(certificateThumprint);
            System.Console.WriteLine("Certificate: " + certificate.ToString());
            var builder =  ConfidentialClientApplicationBuilder.Create(AADAppId);
            var assertionCert = builder.WithCertificate(certificate);


            secretClient = new SecretClient(new Uri(keyVaultUrl), new Azure.Identity.DefaultAzureCredential());
        }

        /// <summary>
        ///  Retrieve secret from vault for processing.
        ///  TODO: this can't be a good design. It passes a secret in plaintext. 
        ///  Callers should not rely on secrets but on managed identities.
        /// </summary>
        /// <param name="secretName"></param>
        /// <returns></returns>
        public async Task<string> GetSecret(string secretName)
        {
            if (cache.ContainsKey(secretName))
                return cache[secretName];

            var secret = await secretClient.GetSecretAsync(secretName);
            var secretValue = secret.Value.Value;
            cache.Add(secretName, secretValue);
            return secretValue;
        }

        static X509Certificate2 FindCertificateByThumbprint(string thumbprint)
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection col = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false); // Don't validate certs, since ours is self-signed.
                if (col == null || col.Count == 0)
                    return null;
                return col[0];
            }
            finally
            {
                store.Close();
            }
        }

        static async Task<string> GetAccessToken(string authority, string resource, string scope, ConfidentialClientApplicationBuilder assertionCert)
        {
            var pca = assertionCert.Build();

            // authority/scope?
            var r = await pca.AcquireTokenForClient(new[] { $"{resource}/.default" })
                .ExecuteAsync()
                .ConfigureAwait(false);

            return r.AccessToken;

            // Variants:
            // var accounts = await pca.GetAccountsAsync(authority);
            // var authResult = await pca.AcquireTokenSilent(resource).ExecuteAsync();

            // old code:
            // var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            // var result = await context.AcquireTokenAsync(resource, assertionCert).ConfigureAwait(false);
            // return result.AccessToken;

            throw null;
        }
    }
}
