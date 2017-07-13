using AzurePerformanceTest;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace LaunchExperiment
{
    class Program
    {
        static void Main(string[] args)
        {
            Keys keys = JsonConvert.DeserializeObject<Keys>(File.ReadAllText("..\\..\\keys.json"));
            var storage = new AzureExperimentStorage(keys.storageName, keys.storageKey);
            var manager = AzureExperimentManager.Open(storage, keys.batchUri, keys.batchName, keys.batchKey);

            //var refExp = new ReferenceExperiment(ExperimentDefinition.Create("referencez3.zip", ExperimentDefinition.DefaultContainerUri, "reference", "smt2", "model_validate=true -smt2 -file:{0}", TimeSpan.FromSeconds(1200), "Z3", null, 2048), 20, 16.34375);

            // storage.SaveReferenceExperiment(refExp).Wait();

            var id = manager.StartExperiment(ExperimentDefinition.Create("z3.zip", ExperimentDefinition.DefaultContainerUri, "QF_BV", "smt2", "model_validate=true -smt2 -file:{0}", TimeSpan.FromSeconds(1200), TimeSpan.FromSeconds(0), "Z3", "asp", 2048, 1, 0), "Dmitry K").Result;

            Console.WriteLine("Experiment id:" + id);

            //manager.RestartBenchmarks(159, new string[] {
            //    "15Puzzle/15-puzzle.init10.smt2",
            //    "15Puzzle/15-puzzle.init11.smt2",
            //    "15Puzzle/15-puzzle.init12.smt2",
            //    "15Puzzle/15-puzzle.init13.smt2",
            //    "15Puzzle/15-puzzle.init14.smt2",
            //    "15Puzzle/15-puzzle.init15.smt2",
            //    "15Puzzle/15-puzzle.init2.smt2",
            //    "15Puzzle/15-puzzle.init3.smt2",
            //    "15Puzzle/15-puzzle.init4.smt2",
            //    "15Puzzle/15-puzzle.init5.smt2",
            //    "15Puzzle/15-puzzle.init6.smt2",
            //    "15Puzzle/15-puzzle.init7.smt2",
            //    "15Puzzle/15-puzzle.init8.smt2",
            //    "15Puzzle/15-puzzle.init9.smt2",
            //    "15Puzzle/15puzzle_ins.lp.smt2"
            //}).Wait();

            Console.WriteLine("Done.");

            Console.ReadLine();
        }
    }

    struct Keys
    {
        public string storageName { get; set; }
        public string storageKey { get; set; }
        public string batchUri { get; set; }
        public string batchName { get; set; }
        public string batchKey { get; set; }
    }
}
