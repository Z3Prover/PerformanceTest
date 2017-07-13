using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Alerts
{
    public class ExperimentAlerts
    {
        private readonly Dictionary<string, AlertSet> alertSets;


        public ExperimentAlerts(ExperimentSummary summary, ExperimentStatusSummary statusSummary, string _linkPage)
        {
            alertSets = new Dictionary<string, AlertSet>();
            var overall = new AlertSet();

            foreach (var catSum in summary.CategorySummary)
            {
                if (catSum.Key != "")
                {
                    var set = FindCategoryAlerts(summary, statusSummary, catSum.Key);
                    alertSets.Add(catSum.Key, set);

                    if (set.Messages.Count != 0)
                        overall.Add(set.Level,
                            string.Format("{0} alert{1} in <a href='" + _linkPage + "?cat={2}' style='text-decoration:none;'>{2}</a>.", set.Count, set.Count == 1 ? "" : "s", catSum.Key));
                }
            }
            alertSets.Add("", overall);

        }

        public IEnumerable<string> Categories
        {
            get
            {
                return alertSets.Keys.ToArray();
            }
        }

        public AlertSet this[string category]
        {
            get
            {
                if (alertSets.Keys.Contains(category))
                    return alertSets[category];
                else
                    return new AlertSet();
            }
        }



        private static AlertSet FindCategoryAlerts(ExperimentSummary summary, ExperimentStatusSummary statusSummary, string category)
        {
            AlertSet res = new AlertSet();

            // Check for bugs != 0; This is critical.            
            int bugs = category == "" ? summary.Overall.Bugs : summary.CategorySummary[category].Bugs;
            if (bugs != 0)
            {
                res.Add(AlertLevel.Critical,
                    string.Format("There {0} {1} bug{2}.", bugs == 1 ? "is" : "are", bugs, bugs == 1 ? "" : "s"));
            }

            // Infrastructure errors; this is just an information.
            int ierrs = category == "" ? summary.Overall.InfrastructureErrors : summary.CategorySummary[category].InfrastructureErrors;
            if (ierrs != 0)
            {
                res.Add(AlertLevel.None,
                    string.Format("There {0} {1} infrastructure error{2}.", ierrs == 1 ? "is" : "are", ierrs, ierrs == 1 ? "" : "s"));
            }

            // Check for errors != 0; this is just a warning.
            int errors = category == "" ? summary.Overall.Errors : summary.CategorySummary[category].Errors;
            if (errors != 0)
            {
                res.Add(AlertLevel.Warning,
                    string.Format("There {0} {1} error{2}.", errors == 1 ? "is" : "are", errors, errors == 1 ? "" : "s"));
            }

            // See whether something got slower.            
            int dippers = GetStatuses(statusSummary.DippersByCategory, category).Count;
            if (dippers != 0)
            {
                res.Add(AlertLevel.None,
                    string.Format("There {0} {1} benchmark{2} that show{3} a dip in performance.",
                                    dippers == 1 ? "is" : "are", dippers,
                                    dippers == 1 ? "" : "s",
                                    dippers == 1 ? "s" : ""));
            }

            return res;
        }

        private static List<string> GetStatuses(Dictionary<string, List<string>> dict, string cat)
        {
            if (dict == null || !dict.ContainsKey(cat)) return new List<string>();
            return dict[cat];
        }
    }


    public enum AlertLevel { None, Warning, Critical };

    public class AlertSet
    {
        public AlertLevel Level = AlertLevel.None;
        public Dictionary<AlertLevel, List<string>> Messages = new Dictionary<AlertLevel, List<string>>();

        public AlertSet()
        {
        }

        public void Add(AlertLevel al, string message)
        {
            if ((Level == AlertLevel.None && al != Level) ||
                (Level == AlertLevel.Warning && al == AlertLevel.Critical))
                Level = al;

            if (!Messages.ContainsKey(al)) Messages.Add(al, new List<string>());
            Messages[al].Add(message);
        }

        public int Count
        {
            get
            {
                int res = 0;
                foreach (KeyValuePair<AlertLevel, List<string>> kvp in Messages)
                    res += kvp.Value.Count;
                return res;
            }
        }
    }
}
