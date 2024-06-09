using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public class ConnectionString
    {
        protected Dictionary<string, string> dict;

        public ConnectionString(string connectionString)
        {
            if (connectionString == null) throw new ArgumentNullException("connectionString");

            string[] items = connectionString.Split(';');
            dict = new Dictionary<string, string>(items.Length);
            foreach (var item in items)
            {
                if (!item.Contains("=")) continue;
                var keyValue = item.Split(new[] { '=' }, 2);
                dict.Add(keyValue[0].Trim(), keyValue[1].Trim());
            }
        }

        public ConnectionString(IDictionary<string, string> parts)
        {
            dict = new Dictionary<string, string>(parts);
        }

        public string this[string key]
        {
            get
            {
                AssertExistence(key);
                return dict[key];
            }
            set { dict[key] = value; }
        }

        /// <summary>
        /// Returns null, if key is missing.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string TryGet(string key)
        {
            string value;
            if (dict.TryGetValue(key, out value)) return value;
            return null;
        }

        public void RemoveKeys(params string[] keys)
        {
            foreach (string key in keys)
            {
                dict.Remove(key);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in dict)
            {
                sb.AppendFormat("{0}={1};", item.Key, item.Value);
            }
            return sb.ToString();
        }

        protected void AssertExistence(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!dict.ContainsKey(key)) throw new KeyNotFoundException("Connection string has no value for the given key '" + key + "'");
        }
    }

    public class BatchConnectionString : ConnectionString
    {
        public const string KeyBatchAccount = "BatchAccount";
        public const string KeyBatchURL = "BatchURL";
        public const string KeyBatchAccessKey = "BatchAccessKey";
        public const string KeyBatchIdentity = "BatchIdentity";

        public BatchConnectionString(string connectionString) : base(connectionString)
        {
        }

        public string BatchAccountName
        {
            get { return this[KeyBatchAccount]; }
            set { this[KeyBatchAccount] = value; }
        }

        public string BatchURL
        {
            get { return this[KeyBatchURL]; }
            set { this[KeyBatchURL] = value; }
        }

        public string BatchAccessKey
        {
            get { return TryGet(KeyBatchAccessKey); }
            set { this[KeyBatchAccessKey] = value; }
        }

        public string BatchIdentity
        {
            get { return TryGet(KeyBatchIdentity); }
            set { this[KeyBatchIdentity] = value; }
        }

        public ConnectionString WithoutBatchData()
        {
            var copy = new ConnectionString(this.dict);
            copy.RemoveKeys(KeyBatchAccount, KeyBatchAccessKey, KeyBatchURL, KeyBatchIdentity);
            return copy;
        }        
    }

    public class StorageAccountConnectionString : ConnectionString
    {
        public const string KeyStorageAccountName = "AccountName";
        public const string KeyStorageAccountKey = "AccountKey";
        public const string KeyStorageIdentity = "AccountIdentity";

        public StorageAccountConnectionString(string connectionString) : base(connectionString)
        {
            foreach (var item in dict.ToArray())
            {
                if (item.Key != KeyStorageAccountKey && item.Key != KeyStorageAccountName && item.Key != "DefaultEndpointsProtocol")
                    dict.Remove(item.Key);
            }
        }

        public string AccountName
        {
            get { return this[KeyStorageAccountName]; }
            set { this[KeyStorageAccountName] = value; }
        }

        public string AccountKey
        {
            get { return TryGet(KeyStorageAccountKey); }
            set { this[KeyStorageAccountKey] = value; }
        }

        public string AccountIdentity
        {
            get { return TryGet(KeyStorageIdentity); }
            set { this[KeyStorageIdentity] = value; }
        }
    }
}
