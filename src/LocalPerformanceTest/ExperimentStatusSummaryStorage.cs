using Measurement;
using PerformanceTest;
using PerformanceTest.Records;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PerformanceTest
{
    public static class ExperimentStatusSummaryStorage
    {
        private const string TagBug = "BUGS";
        private const string TagError = "ERRORS";
        private const string TagDippers = "DIPPERS";

        public static void Save(ExperimentStatusSummary summary, Stream stream)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));

            StreamWriter f = new StreamWriter(stream);

            SaveDict(f, TagError, summary.ErrorsByCategory);
            SaveDict(f, TagBug, summary.BugsByCategory);
            SaveDict(f, TagDippers, summary.DippersByCategory);

            foreach (var tag in summary.TagsByCategory)
            {
                SaveDict(f, tag.Key, tag.Value);
            }

            f.Flush();
        }

        private static void SaveDict(StreamWriter f, string tag, Dictionary<string, List<string>> dict)
        {
            f.WriteLine("[" + tag + "]");
            bool takeEmpty = dict.Count == 1;
            foreach (var kvp in dict)
            {
                if (takeEmpty || kvp.Key != "")
                    foreach (string s in kvp.Value)
                        f.WriteLine(kvp.Key + "," + s);
            }
        }

        public static ExperimentStatusSummary Load(int expId, int? refExpId, Stream stream)
        {
            Dictionary<string, List<string>> errorsByCategory = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> bugsByCategory = new Dictionary<string, List<string>>();
            Dictionary<string, Dictionary<string, List<string>>> tagsByCategory = new Dictionary<string, Dictionary<string, List<string>>>();
            Dictionary<string, List<string>> dippersByCategory = new Dictionary<string, List<string>>();

            StreamReader f = new StreamReader(stream);
            ParserType t = ParserType.NONE;
            string customTag = null;

            while (!f.EndOfStream)
            {
                string s = f.ReadLine();
                string[] tokens = s.Split(new[] { ',' }, 2);

                if (tokens.Length == 1)
                {
                    string line = tokens[0].Trim();
                    if (String.IsNullOrEmpty(line)) continue;
                    if (!line.StartsWith("[") || !line.EndsWith("]")) throw new FormatException("Unexpected token");

                    string parsingTag = line.Substring(1, line.Length - 2);
                    switch (parsingTag)
                    {
                        case TagError: t = ParserType.ERRORS; break;
                        case TagBug: t = ParserType.BUGS; break;
                        case TagDippers: t = ParserType.DIPPERS; break;
                        default: t = ParserType.CUSTOM; customTag = parsingTag; break;
                    }
                }
                else
                {
                    switch (t)
                    {
                        case ParserType.ERRORS:
                            AddToList(errorsByCategory, tokens[0], tokens[1]);
                            break;

                        case ParserType.BUGS:
                            AddToList(bugsByCategory, tokens[0], tokens[1]);
                            break;

                        case ParserType.DIPPERS:
                            AddToList(dippersByCategory, tokens[0], tokens[1]);
                            break;

                        case ParserType.CUSTOM:
                            Dictionary<string, List<string>> byCat;
                            if (!tagsByCategory.TryGetValue(customTag, out byCat))
                                tagsByCategory.Add(customTag, byCat = new Dictionary<string, List<string>>());

                            AddToList(byCat, tokens[0], tokens[1]);
                            break;

                        case ParserType.NONE:
                        default:
                            throw new FormatException("Missing line with tag");
                    }
                }
            }
            return new ExperimentStatusSummary(expId, refExpId, errorsByCategory, bugsByCategory, tagsByCategory, dippersByCategory);
        }


        private static void AddToList(Dictionary<string, List<string>> dict, string cat, string value)
        {
            List<string> list;
            if (!dict.TryGetValue(cat, out list))
                dict.Add(cat, list = new List<string>());
            list.Add(value);

            if (!dict.TryGetValue("", out list))
                dict.Add("", list = new List<string>());
            list.Add(value);
        }


        protected enum ParserType { NONE, ERRORS, BUGS, CUSTOM, DIPPERS };
    }
}
