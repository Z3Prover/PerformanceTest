using AzurePerformanceTest;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public partial class AzureExperimentStorage
    {
        private const int azureStorageBatchSize = 100;

        /// <summary>
        /// Replaces existing experiments and their results which have same ID.
        /// </summary>
        /// <param name="experiments"></param>
        /// <returns></returns>
        public async Task ImportExperiments(IEnumerable<ExperimentEntity> experiments)
        {
            var nextIdQuery = QueryForNextId();
            var list = (await experimentsTable.ExecuteQuerySegmentedAsync(nextIdQuery, null)).ToList();
            int nextId = 0;
            if (list.Count != 0)
                nextId = list[0].Id;

            var upload =
                GroupExperiments(experiments, azureStorageBatchSize)
                .Select(batch =>
                {
                    TableBatchOperation opsBatch = new TableBatchOperation();
                    int maxID = 0;
                    foreach (var item in batch)
                    {
                        opsBatch.InsertOrReplace(item);
                        int id = int.Parse(item.RowKey, System.Globalization.CultureInfo.InvariantCulture);
                        if (id > maxID) maxID = id;
                    }
                    return Tuple.Create(experimentsTable.ExecuteBatchAsync(opsBatch), maxID);
                })
                .ToArray();

            var maxId = upload.Length > 0 ? upload.Max(t => t.Item2) + 1 : 1;
            var inserts = upload.Select(t => t.Item1);
            await Task.WhenAll(inserts);

            if (maxId > nextId)
            {
                var nextIdEnt = new NextExperimentIDEntity();
                nextIdEnt.Id = maxId;
                await experimentsTable.ExecuteAsync(TableOperation.InsertOrReplace(nextIdEnt));
            }
        }

        private static IEnumerable<IEnumerable<ExperimentEntity>> GroupExperiments(IEnumerable<ExperimentEntity> seq, int n)
        {
            var groupsByCat = new Dictionary<string, List<ExperimentEntity>>();
            string lastCat = null;
            List<ExperimentEntity> lastGroup = null;
            foreach (ExperimentEntity item in seq)
            {
                List<ExperimentEntity> group;
                if (lastCat == item.Category)
                {
                    group = lastGroup;
                }
                else if (!groupsByCat.TryGetValue(item.Category, out group))
                {
                    group = new List<ExperimentEntity>(n);
                    groupsByCat.Add(item.Category, group);
                }

                group.Add(item);

                if (group.Count == n)
                {
                    yield return group;
                    group = groupsByCat[item.Category] = new List<ExperimentEntity>(n);
                }
                lastCat = item.Category;
                lastGroup = group;
            }
            foreach (var group in groupsByCat)
            {
                var items = group.Value;
                if (items.Count > 0)
                    yield return items;
            }
        }

        private static IEnumerable<IEnumerable<T>> Group<T>(IEnumerable<T> seq, int n)
        {
            List<T> group = new List<T>(n);
            foreach (T item in seq)
            {
                group.Add(item);
                if (group.Count == n)
                {
                    yield return group;
                    group = new List<T>(n);
                }
            }
            if (group.Count > 0)
                yield return group;
        }
    }
}
