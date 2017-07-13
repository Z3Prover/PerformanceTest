using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    internal class AzureUtils
    {
        private static string invalidChars = System.Text.RegularExpressions.Regex.Escape("." + new string(Path.GetInvalidFileNameChars()));
        private static string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

        public  static string ToBinaryPackBlobName(string name)
        {
            string name2 = System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
            if (name2.Length > 1024) name2 = name2.Substring(0, 1024);
            else if (name2.Length == 0) name2 = "_";
            return name2;
        }
    }
}
