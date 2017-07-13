using Angara.Data;
using Angara.Data.DelimitedFile;
using Measurement;
using Microsoft.FSharp.Core;
using PerformanceTest;
using PerformanceTest.Records;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static Microsoft.FSharp.Core.OptimizedClosures;

namespace PerformanceTest
{
    public static class ExperimentSummaryStorage
    {
        private const string dateFormat = "yyyy-MM-dd HH:mm:ss";
        private const string KeyBug = "BUG";
        private const string KeyError = "ERROR";
        private const string KeyInferr = "INFERR";
        private const string KeyMemoryOut = "MEMORY";
        private const string KeyTimeOut = "TIMEOUT";
        private const string KeyFiles = "FILES";

        public static Table EmptyTable()
        {
            return Table.OfColumns(
                new[]
                {
                    Column.Create("ID", new string[0], FSharpOption<int>.None),
                    Column.Create("Date", new string[0], FSharpOption<int>.None)
                });
        }

        public static Table AppendOrReplace(Table table, ExperimentSummary newSummary)
        {
            Dictionary<string, string> newColumns = new Dictionary<string, string>
            {
                { "ID", newSummary.Id.ToString() },
                { "Date", newSummary.Date.ToUniversalTime().ToString(dateFormat) }
            };
            foreach (var catSummary in newSummary.CategorySummary)
            {
                string cat = catSummary.Key;
                var expSum = catSummary.Value;
                newColumns.Add(string.Join("|", cat, KeyBug), expSum.Bugs.ToString());
                newColumns.Add(string.Join("|", cat, KeyError), expSum.Errors.ToString());
                newColumns.Add(string.Join("|", cat, KeyInferr), expSum.InfrastructureErrors.ToString());
                newColumns.Add(string.Join("|", cat, KeyMemoryOut), expSum.MemoryOuts.ToString());
                newColumns.Add(string.Join("|", cat, KeyTimeOut), expSum.Timeouts.ToString());
                newColumns.Add(string.Join("|", cat, KeyFiles), expSum.Files.ToString());

                foreach (var prop in expSum.Properties)
                {
                    string propName = prop.Key;
                    string propVal = prop.Value;
                    newColumns.Add(string.Join("|", cat, propName), propVal);
                }
            }

            // If the table already has summary for the experiment, it will be replaced
            if (table.Count > 0) // if not empty then must contain ID
                table = Table.Filter(new[] { "ID" }, FSharpFunc<string, bool>.FromConverter(id => int.Parse(id, CultureInfo.InvariantCulture) != newSummary.Id), table);

            List<Column> finalColumns = new List<Column>();
            foreach (var existingColumn in table)
            {
                string newVal;
                ImmutableArray<string> newColArray;
                if (newColumns.TryGetValue(existingColumn.Name, out newVal))
                {
                    newColArray = existingColumn.Rows.AsString.Add(newVal);
                    newColumns.Remove(existingColumn.Name);
                }
                else
                    newColArray = existingColumn.Rows.AsString.Add(string.Empty);

                finalColumns.Add(Column.Create(existingColumn.Name, newColArray, FSharpOption<int>.None));
            }
            foreach (var newColumn in newColumns)
            {
                var array = ImmutableArray.CreateBuilder<string>(table.RowsCount + 1);
                for (int i = table.RowsCount; --i >= 0;)
                    array.Add(string.Empty);
                array.Add(newColumn.Value);

                finalColumns.Add(Column.Create(newColumn.Key, array, FSharpOption<int>.None));
            }

            var finalTable = Table.OfColumns(finalColumns);
            return finalTable;
        }

        public static Table Remove(Table table, int id)
        {
            // If the table already has summary for the experiment, it will be replaced
            if (table.Count > 0) // if not empty then must contain ID
                table = Table.Filter(new[] { "ID" }, FSharpFunc<string, bool>.FromConverter(_id => int.Parse(_id, CultureInfo.InvariantCulture) != id), table);

            return table;
        }

        public static void AppendOrReplace(Stream source, ExperimentSummary newSummary, Stream dest)
        {
            var table = Table.Load(new StreamReader(source), new ReadSettings(Delimiter.Comma, false, true, FSharpOption<int>.None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));
            var finalTable = AppendOrReplace(table, newSummary);
            Table.Save(finalTable, new StreamWriter(dest, new UTF8Encoding(true)));
        }

        public static void Remove(Stream source, int id, Stream dest)
        {
            var table = Table.Load(new StreamReader(source), new ReadSettings(Delimiter.Comma, false, true, FSharpOption<int>.None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));
            var finalTable = Remove(table, id);
            Table.Save(finalTable, new StreamWriter(dest, new UTF8Encoding(true)));
        }

        public static void SaveTable(Table summary, Stream stream)
        {
            Table.Save(summary, new StreamWriter(stream, new UTF8Encoding(true)));
        }

        public static Table LoadTable(Stream stream)
        {
            var table = Table.Load(new StreamReader(stream), new ReadSettings(Delimiter.Comma, false, true, FSharpOption<int>.None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));
            return table;
        }

        public static ExperimentSummary[] LoadFromTable(Table table)
        {
            var date = table["Date"].Rows.AsString;
            var id = table["ID"].Rows.AsString;
            var content = // category -> (parameter, value)
                table
                    .Where(c => c.Name.Contains("|"))
                    .Select(c =>
                    {
                        string[] parts = c.Name.Split(new[] { '|' }, 2);
                        string category = parts[0];
                        string property = parts[1];
                        return Tuple.Create(category, property, c.Rows);
                    })
                    .GroupBy(t => t.Item1)
                    .ToDictionary(g => g.Key, g => g.Select(t => Tuple.Create(t.Item2, t.Item3.AsString)).ToArray());

            int rowsCount = table.RowsCount;
            var results = new ExperimentSummary[table.RowsCount];
            for (int row = 0; row < rowsCount; row++)
            {
                int expId = int.Parse(id[row], CultureInfo.InvariantCulture);
                DateTimeOffset expDate = DateTimeOffset.ParseExact(date[row], dateFormat, System.Globalization.CultureInfo.InvariantCulture);

                Dictionary<string, AggregatedAnalysis> catSum = new Dictionary<string, AggregatedAnalysis>();
                foreach (var cat in content)
                {
                    string category = cat.Key;
                    var catParameters = cat.Value;

                    int bugs = 0;
                    int errors = 0;
                    int infrastructureErrors = 0;
                    int timeouts = 0;
                    int memouts = 0;
                    int files = 0;
                    var props = new Dictionary<string, string>(catParameters.Length);

                    for (int i = 0; i < catParameters.Length; i++)
                    {
                        var p = catParameters[i];
                        var val = p.Item2[row];
                        if (!string.IsNullOrEmpty(val))
                            switch (p.Item1)
                            {
                                case KeyBug: bugs = int.Parse(val, CultureInfo.InvariantCulture); break;
                                case KeyError: errors = int.Parse(val, CultureInfo.InvariantCulture); break;
                                case KeyInferr: infrastructureErrors = int.Parse(val, CultureInfo.InvariantCulture); break;
                                case KeyMemoryOut: memouts = int.Parse(val, CultureInfo.InvariantCulture); break;
                                case KeyTimeOut: timeouts = int.Parse(val, CultureInfo.InvariantCulture); break;
                                case KeyFiles: files = int.Parse(val, CultureInfo.InvariantCulture); break;
                                default: props[p.Item1] = val; break;
                            }
                    }

                    catSum.Add(category, new AggregatedAnalysis(bugs, errors, infrastructureErrors, timeouts, memouts, props, files));
                    results[row] = new ExperimentSummary(expId, expDate, catSum);
                }
            }
            return results;
        }
    }
}
