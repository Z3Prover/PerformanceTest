using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Angara.Data;
using Angara.Data.DelimitedFile;
using Microsoft.FSharp.Core;

namespace PerformanceTest
{
    public static class TagsStorage
    {
        public static Tags Load(Stream stream)
        {
            var table = Table.Load(new StreamReader(stream), new ReadSettings(Delimiter.Comma, false, true, FSharpOption<int>.None,
                 FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));

            Dictionary<int, string> idToName = new Dictionary<int, string>();

            int n = 0;
            for (int i = 0; i < table.Count && n != 3; i++)
            {
                if (table[i].Name == "Id") n = n | 1;
                else if (table[i].Name == "Name") n = n | 2;
            }
            if (n == 3)
            {
                var ids = table["Id"].Rows.AsString;
                var names = table["Name"].Rows.AsString;
                for (int i = 0; i < ids.Length; i++)
                {
                    idToName[int.Parse(ids[i], System.Globalization.CultureInfo.InvariantCulture)] = names[i];
                }
            }
            return new Tags(idToName);
        }
    }
}
