using Angara.Data;
using Angara.Data.DelimitedFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{
    public static class TableExtensions
    {
        public static void SaveUTF8Bom(this Table table, string fileName, WriteSettings settings)
        {
            using (TextWriter w = new StreamWriter(fileName, false, new UTF8Encoding(true)))
            {
                Table.Save(table, w, settings);
            }
        }

        public static void SaveUTF8Bom(this Table table, Stream s, WriteSettings settings)
        {
            using (TextWriter w = new StreamWriter(s, new UTF8Encoding(true)))
            {
                Table.Save(table, w, settings);
            }
        }
    }
}
