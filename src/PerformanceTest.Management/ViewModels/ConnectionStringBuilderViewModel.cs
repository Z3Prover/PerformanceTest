using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class ConnectionStringBuilderViewModel : INotifyPropertyChanged
    {
        private BatchConnectionString cs;

        public event PropertyChangedEventHandler PropertyChanged;

        public ConnectionStringBuilderViewModel(string connectionString)
        {
            cs = new BatchConnectionString(connectionString);   
            if(cs.TryGet("DefaultEndpointsProtocol") == null)
            {
                cs["DefaultEndpointsProtocol"] = "https";
            }
        }


        public string ConnectionString
        {
            get
            {
                return cs.ToString();
            }
        }


        public string StorageAccountName
        {
            get { return cs.TryGet("AccountName"); }
            set {
                cs["AccountName"] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }
        public string StorageAccountKey
        {
            get { return cs.TryGet("AccountKey"); }
            set
            {
                cs["AccountKey"] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }
        public string BatchAccountName
        {
            get { return cs.TryGet(BatchConnectionString.KeyBatchAccount); }
            set
            {
                cs[BatchConnectionString.KeyBatchAccount] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }
        public string BatchURL
        {
            get { return cs.TryGet(BatchConnectionString.KeyBatchURL); }
            set
            {
                cs[BatchConnectionString.KeyBatchURL] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }
        public string BatchKey
        {
            get { return cs.TryGet(BatchConnectionString.KeyBatchAccessKey); }
            set
            {
                cs[BatchConnectionString.KeyBatchAccessKey] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }


        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
