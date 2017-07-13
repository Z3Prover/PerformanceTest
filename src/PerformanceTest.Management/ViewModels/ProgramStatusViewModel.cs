using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class ProgramStatusViewModel : INotifyPropertyChanged
    {
        private string status;

        public event PropertyChangedEventHandler PropertyChanged;

        public ProgramStatusViewModel()
        {
            status = "Ready.";
        }

        public string Status
        {
            get
            {
                return status;
            }
            set
            {
                if (status == value) return;
                status = value;
                NotifyPropertyChanged();
            }
        }


        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
