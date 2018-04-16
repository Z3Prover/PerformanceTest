using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzurePerformanceTest
{
    public class AzureBenchmarkStorage
    {
        public const string DefaultContainerName = "input";

        // Storage account
        //private CloudStorageAccount storageAccount;
        //private CloudBlobClient blobClient;
        private CloudBlobContainer inputsContainer;
        private string uri = null;
        private string signature = null;


        public AzureBenchmarkStorage(string storageAccountName, string storageAccountKey, string inputsContainerName) : this(String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", storageAccountName, storageAccountKey), inputsContainerName)
        {
        }

        public AzureBenchmarkStorage(string storageConnectionString, string inputsContainerName)
        {
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            inputsContainer = blobClient.GetContainerReference(inputsContainerName);

            inputsContainer.CreateIfNotExists();
        }

        public AzureBenchmarkStorage(string containerUri)
        {
            System.Console.WriteLine("Questionable URI: {0}", containerUri);

            this.uri = containerUri;
            var parts = containerUri.Split('?');
            if (parts.Length != 2)
                throw new ArgumentException("Incorrect uri");

            this.signature = "?" + parts[1];
            inputsContainer = new CloudBlobContainer(new Uri(containerUri));
        }


        public string GetContainerSASUri()
        {
            if (uri != null)
                return uri;

            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List
            };
            string signature = inputsContainer.GetSharedAccessSignature(sasConstraints);
            return inputsContainer.Uri + signature;
        }

        public async Task<BlobResultSegment> ListBlobsSegmentedAsync(string prefix = "", BlobContinuationToken currentToken = null)
        {
            return await inputsContainer.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.All, null, currentToken, null, null);
        }

        public async Task<BlobResultSegment> ListBlobsSegmentedAsync(string directory, string category, BlobContinuationToken currentToken = null)
        {
            string prefix = CombineBlobPath(directory, category);
            return await ListBlobsSegmentedAsync(prefix, currentToken);
        }

        public IEnumerable<IListBlobItem> ListBlobs(string prefix = "")
        {
            return inputsContainer.ListBlobs(prefix, true);
        }

        public IEnumerable<string> ListDirectories(string baseDirectory = "")
        {
            CloudBlobDirectory dir = inputsContainer.GetDirectoryReference(baseDirectory);
            return dir.ListBlobs(false, BlobListingDetails.None,
                options: new BlobRequestOptions
                {
                     RetryPolicy = new Microsoft.WindowsAzure.Storage.RetryPolicies.ExponentialRetry(TimeSpan.FromMilliseconds(50), 5)
                })
                .Where(d => d is CloudBlobDirectory)
                .Select(d =>
                {
                    string prefix;
                    CloudBlobDirectory cd = d as CloudBlobDirectory;
                    if (cd.Parent != null && cd.Parent.Prefix != null)
                    {
                        prefix = cd.Prefix.Substring(cd.Parent.Prefix.Length, cd.Prefix.Length - cd.Parent.Prefix.Length - 1);
                    }
                    else
                    {
                        prefix = cd.Prefix.Substring(0, cd.Prefix.Length - 1);
                    }
                    return prefix;
                })
                .Distinct();

        }

        public IEnumerable<IListBlobItem> ListBlobs(string directory, string category)
        {
            string prefix = CombineBlobPath(directory, category);
            return ListBlobs(prefix);
        }

        private static string CombineBlobPath(string part1, string part2)
        {
            string benchmarksPath;
            if (string.IsNullOrEmpty(part1))
                benchmarksPath = part2;
            else if (string.IsNullOrEmpty(part2))
                benchmarksPath = part1;
            else
            {
                var benchmarksDirClear = part1.TrimEnd('/');
                var benchmarksCatClear = part2.TrimStart('/');
                benchmarksPath = benchmarksDirClear + "/" + benchmarksCatClear;
            }
            benchmarksPath = benchmarksPath.TrimEnd('/');
            if (benchmarksPath.Length > 0)
                benchmarksPath = benchmarksPath + "/";
            return benchmarksPath;
        }

        public string GetBlobSASUri(CloudBlob blob)
        {
            if (this.signature != null)
                return blob.Uri + this.signature;
            else
            {
                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                    Permissions = SharedAccessBlobPermissions.Read
                };
                return blob.Uri + blob.GetSharedAccessSignature(sasConstraints);
            }
        }

        public string GetBlobSASUri(string blobName)
        {
            var blob = inputsContainer.GetBlobReference(blobName);
            return GetBlobSASUri(blob);
        }
    }
}
