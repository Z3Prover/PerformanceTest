using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Angara.Data;
using Angara.Data.DelimitedFile;
using System.Diagnostics;
using Microsoft.FSharp.Core;
using Measurement;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PerformanceTest
{
    public class FileStorage
    {
        private static FSharpOption<int> None = FSharpOption<int>.None;


        public static FileStorage Open(string storageName)
        {
            return new FileStorage(storageName);
        }



        private readonly DirectoryInfo dir;
        private readonly DirectoryInfo dirBenchmarks;

        private Table<ExperimentEntity> experimentsTable;


        private FileStorage(string storageName)
        {
            dir = Directory.CreateDirectory(storageName);
            dirBenchmarks = dir.CreateSubdirectory("data");

            string tableFile = Path.Combine(dir.FullName, "experiments.csv");
            if (File.Exists(tableFile))
            {
                experimentsTable =
                    Table.OfRows(
                        Table.Load(tableFile, new ReadSettings(Delimiter.Comma, true, true, None,
                            FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple =>
                            {
                                var colName = tuple.Item2;
                                switch (colName)
                                {
                                    case "ID": return FSharpOption<Type>.Some(typeof(int));
                                    case "MemoryLimit": return FSharpOption<Type>.Some(typeof(int));
                                }
                                return FSharpOption<Type>.None;
                            }))))
                        .ToRows<ExperimentEntity>());
            }
            else
            {
                experimentsTable = Table.OfRows<ExperimentEntity>(new ExperimentEntity[0]);
            }
        }

        public string Location
        {
            get { return dir.FullName; }
        }

        public int MaxExperimentId
        {
            get
            {
                if (experimentsTable.RowsCount > 0)
                    return experimentsTable["ID"].Rows.AsInt.Max();
                else return 0;
            }
        }

        public void Clear()
        {
            experimentsTable = Table.OfRows<ExperimentEntity>(new ExperimentEntity[0]);
            dir.Delete(true);
            dir.Create();
            dirBenchmarks.Create();
        }

        public static void Clear(string path)
        {
            Directory.Delete(path, true);
        }

        public Dictionary<int, ExperimentEntity> GetExperiments()
        {
            var dict = new Dictionary<int, ExperimentEntity>();
            foreach (var row in experimentsTable.Rows)
            {
                dict[row.ID] = row;
            }
            return dict;
        }

        public void SaveReferenceExperiment(ReferenceExperiment reference)
        {
            string json = JsonConvert.SerializeObject(reference, Formatting.Indented);
            File.WriteAllText(Path.Combine(dir.FullName, "reference.json"), json);
        }

        public ReferenceExperiment GetReferenceExperiment()
        {
            string content = File.ReadAllText(Path.Combine(dir.FullName, "reference.json"));
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new PrivatePropertiesResolver();
            ReferenceExperiment reference = JsonConvert.DeserializeObject<ReferenceExperiment>(content, settings);
            return reference;
        }

        public bool HasResults(int experimentId)
        {
            return File.Exists(IdToPath(experimentId));
        }

        public BenchmarkResult[] GetResults(int experimentId)
        {
            using (Stream stream = new FileStream(IdToPath(experimentId), FileMode.Open, FileAccess.Read))
            {
                return BenchmarkResultsStorage.LoadBenchmarks(experimentId, stream);
            }
        }

        public void AddExperiment(int id, ExperimentDefinition experiment, DateTime submitted, string creator, string note)
        {
            experimentsTable = experimentsTable.AddRow(new ExperimentEntity
            {
                ID = id,
                Submitted = submitted,
                Executable = experiment.Executable,
                Parameters = experiment.Parameters,
                BenchmarkDirectory = experiment.BenchmarkDirectory,
                BenchmarkFileExtension = experiment.BenchmarkFileExtension,
                Category = experiment.Category,
                BenchmarkTimeout = experiment.BenchmarkTimeout.TotalSeconds,
                ExperimentTimeout = experiment.ExperimentTimeout.TotalSeconds,
                MemoryLimitMB = experiment.MemoryLimitMB,
                GroupName = experiment.GroupName,
                Note = note,
                Creator = creator
            });
            experimentsTable.SaveUTF8Bom(Path.Combine(dir.FullName, "experiments.csv"), new WriteSettings(Delimiter.Comma, true, true));
        }

        public void AddResults(int id, BenchmarkResult[] benchmarks)
        {
            SaveBenchmarks(benchmarks, IdToPath(id));
        }

        public void RemoveExperimentRow(ExperimentEntity deleteRow)
        {
            experimentsTable = Table.OfRows(experimentsTable.Rows.Where(r => r.ID != deleteRow.ID));
            experimentsTable.SaveUTF8Bom(Path.Combine(dir.FullName, "experiments.csv"), new WriteSettings(Delimiter.Comma, true, true));
        }
        public void ReplaceExperimentRow(ExperimentEntity newRow)
        {
            experimentsTable = Table.OfRows(experimentsTable.Rows.Select(r =>
            {
                return r.ID == newRow.ID ? newRow : r;
            }));
            experimentsTable.SaveUTF8Bom(Path.Combine(dir.FullName, "experiments.csv"), new WriteSettings(Delimiter.Comma, true, true));
        }

        /// <summary>
        /// Returns a full path to a file containing results for the given experiment ID.
        /// Doesn't check existence of the path.
        /// </summary>
        /// <param name="id">Experiment ID.</param>
        /// <returns>Full path.</returns>
        public string IdToPath(int id)
        {
            return Path.Combine(dirBenchmarks.FullName, id.ToString("000000") + ".csv");
        }

        private void SaveBenchmarks(BenchmarkResult[] benchmarks, string fileName)
        {
            using (Stream s = File.Create(fileName))
            {
                BenchmarkResultsStorage.SaveBenchmarks(benchmarks, s);
            }
        }

        internal class PrivatePropertiesResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty prop = base.CreateProperty(member, memberSerialization);
                prop.Writable = true;
                return prop;
            }
        }
    }
}
