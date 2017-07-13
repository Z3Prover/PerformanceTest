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

namespace PerformanceTest
{
    public static class RecordsStorage
    {
        public static Table BenchmarksRecordsToTable(IReadOnlyDictionary<string, Record> benchmarkRecords)
        {
            var files = ImmutableArray.CreateBuilder<string>();
            var times = ImmutableArray.CreateBuilder<string>();
            var ids = ImmutableArray.CreateBuilder<string>();

            foreach (var r in benchmarkRecords)
            {
                files.Add(r.Key);
                times.Add(r.Value.Runtime.ToString());
                ids.Add(r.Value.ExperimentId.ToString());
            }


            var cols = new[]
            {
                Column.Create("FileName", files, FSharpOption<int>.None),
                Column.Create("Time", times, FSharpOption<int>.None),
                Column.Create("ExperimentID", ids, FSharpOption<int>.None)
            };

            var table = Table.OfColumns(cols);
            return table;
        }

        public static Dictionary<string, Record> BenchmarksRecordsFromTable(Table table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (table.Count != 3) throw new ArgumentException("Records table must have 3 columns", nameof(table));

            var files = table[0].Rows.AsString;
            var times = table[1].Rows.AsString;
            var ids = table[2].Rows.AsString;

            var dict = new Dictionary<string, Record>(table.RowsCount);
            for (int i = 0; i < files.Length; i++)
            {
                dict.Add(files[i], new Record(int.Parse(ids[i], CultureInfo.InvariantCulture), double.Parse(times[i], CultureInfo.InvariantCulture)));
            }

            return dict;
        }

        public static void SaveBenchmarksRecords(IReadOnlyDictionary<string, Record> benchmarkRecords, Stream stream)
        {
            var finalTable = BenchmarksRecordsToTable(benchmarkRecords);
            Table.Save(finalTable, new StreamWriter(stream, new UTF8Encoding(true)), new WriteSettings(Delimiter.Comma, false, false));
        }

        public static Dictionary<string, Record> LoadBenchmarksRecords(Stream stream)
        {
            var table = Table.Load(new StreamReader(stream), new ReadSettings(Delimiter.Comma, false, false, FSharpOption<int>.None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));
            return BenchmarksRecordsFromTable(table);
        }


        public static Table SummaryRecordsToTable(Dictionary<string, CategoryRecord> records)
        {
            var category = ImmutableArray.CreateBuilder<string>();
            var times = ImmutableArray.CreateBuilder<string>();
            var files = ImmutableArray.CreateBuilder<string>();

            foreach (var r in records)
            {
                category.Add(r.Key);
                times.Add(r.Value.Runtime.ToString());
                files.Add(r.Value.Files.ToString());
            }

            var cols = new[]
            {
                Column.Create("Category", category, FSharpOption<int>.None),
                Column.Create("Time", times, FSharpOption<int>.None),
                Column.Create("Files", files, FSharpOption<int>.None)
            };

            var table = Table.OfColumns(cols);
            return table;
        }

        public static Dictionary<string, CategoryRecord> SummaryRecordsFromTable(Table table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (table.Count != 3) throw new ArgumentException("Records table must have 3 columns", nameof(table));

            var category = table[0].Rows.AsString;
            var times = table[1].Rows.AsString;
            var files = table[2].Rows.AsString;

            var dict = new Dictionary<string, CategoryRecord>(table.RowsCount);
            for (int i = 0; i < category.Length; i++)
            {
                dict.Add(category[i], new CategoryRecord(double.Parse(times[i], CultureInfo.InvariantCulture), int.Parse(files[i], CultureInfo.InvariantCulture)));
            }

            return dict;
        }

        public static void SaveSummaryRecords(Dictionary<string, CategoryRecord> records, Stream stream)
        {
            var finalTable = SummaryRecordsToTable(records);
            Table.Save(finalTable, new StreamWriter(stream, new UTF8Encoding(true)), new WriteSettings(Delimiter.Comma, false, false));
        }

        public static Dictionary<string, CategoryRecord> LoadSummaryRecords(Stream stream)
        {
            var table = Table.Load(new StreamReader(stream), new ReadSettings(Delimiter.Comma, false, false, FSharpOption<int>.None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));
            return SummaryRecordsFromTable(table);
        }
    }

}
