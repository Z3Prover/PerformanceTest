using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using AzurePerformanceTest;
using Nightly.Properties;

namespace Nightly
{
    public partial class SiteMaster : System.Web.UI.MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        public static async Task<string> GetConnectionString()
        {
            if (!String.IsNullOrWhiteSpace(Settings.Default.ConnectionString))
            {
                return Settings.Default.ConnectionString;
            }

            var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            return await secretStorage.GetSecret(Settings.Default.ConnectionStringSecretId);
        }
    }
}
