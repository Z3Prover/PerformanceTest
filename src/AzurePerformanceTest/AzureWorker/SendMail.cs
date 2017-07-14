using AzurePerformanceTest;
using PerformanceTest;
using PerformanceTest.Alerts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace AzureWorker
{
    public class SendMail
    {
        uint retryCount = 3;
        string userMail = "";
        string pwd = "";
        string serverUrl = "";
        public SendMail (string credentials, string serverUrl)
        {
            if (credentials == null) throw new ArgumentNullException("credentials");
            if (serverUrl == null) throw new ArgumentNullException("serverUrl");
            if (credentials != "")
            {
                string[] parsedCred = credentials.Split(':');
                this.userMail = parsedCred[0];
                if (parsedCred.Length > 1) this.pwd = parsedCred[1];
            }
            this.serverUrl = serverUrl;
        }
        public async Task SendReport(AzureSummaryManager manager, ExperimentSummary expSummary, ExperimentSummary refSummary, string recipientsStr, string linkPage)
        {
            var expId = expSummary.Id;
            var refId = refSummary.Id;
            var submissionTime = expSummary.Date;
            var statusSummary = await manager.GetStatusSummary(expId, refId);

            var alerts = new ExperimentAlerts(expSummary, statusSummary, linkPage);


            if (alerts != null && alerts[""].Count > 0 && alerts[""].Level != AlertLevel.None)
            {
                Trace.WriteLine("Building summary HTML report...");
                string new_report = "<body>";
                new_report += "<h1>Z3 Nightly Alert Report</h1>";
                new_report += "<p>This are alerts for <a href=" + linkPage + "?job=" + expId + " style='text-decoration:none'>job #" + expId + "</a> (submitted " + submissionTime + ").</p>";

                if (alerts[""].Count == 0)
                {
                    new_report += "<p>";
                    new_report += "<img src='cid:ok'/> ";
                    new_report += "<font color=Green>All is well everywhere!</font>";
                    new_report += "</p>";
                }
                else
                {
                    new_report += "<h2>Alert Summary</h2>";
                    new_report += createReportTable("", alerts);

                    //detailed report
                    new_report += "<h2>Detailed alerts</h2>";
                    foreach (string cat in alerts.Categories)
                    {
                        if (cat != "" && alerts[cat].Count > 0)
                        {
                            new_report += "<h3><a href='" + linkPage + "?job=" + expId + "&cat=" + cat + "' style='text-decoration:none'>" + cat + "</a></h3>";
                            new_report += createReportTable(cat, alerts);
                        }
                    }
                    new_report += "<p>For more information please see the <a href='" + linkPage + "' style='text-decoration:none'>Z3 Nightly Webpage</a>.</p>";
                }
                new_report += "</body>";


                Trace.WriteLine("Send emails with report...");
                try
                {
                    var recipients = recipientsStr.Split(';');
                    foreach (string recipient in recipients)
                    {
                        Send(recipient, "Z3 Alerts", new_report, true);
                    }
                }
                catch(Exception ex)
                {
                    Trace.WriteLine("Failed to send email: " + ex.Message);
                }
            }
        }
        private string createReportTable(string category, ExperimentAlerts alerts)
        {
            AlertSet alertSet = alerts[category];
            string new_table = "";
            //add table 
            if (alertSet.Count > 0)
            {
                new_table += "<table>";

                foreach (KeyValuePair<AlertLevel, List<string>> kvp in alertSet.Messages)
                {
                    AlertLevel level = kvp.Key;
                    List<string> messages = kvp.Value;

                    new_table += "<tr>";
                    switch (level)
                    {
                        case AlertLevel.None:
                            new_table += "<td align=left valign=top><img src='https://raw.githubusercontent.com/Z3Prover/PerformanceTest/gh-pages/images/ok.png'/></td>";
                            new_table += "<td align=left valign=middle><font color=Green>";
                            break;
                        case AlertLevel.Warning:
                            new_table += "<td align=left valign=top><img src='https://raw.githubusercontent.com/Z3Prover/PerformanceTest/gh-pages/images/warning.png'/></td>";
                            new_table += "<td align=left valign=middle><font color=Orange>";
                            break;
                        case AlertLevel.Critical:
                            new_table += "<td align=left valign=top><img src='https://raw.githubusercontent.com/Z3Prover/PerformanceTest/gh-pages/images/critical.png'/></td>";
                            new_table += "<td align=left valign=middle><font color=Red>";
                            break;
                    }
                    foreach (string m in messages)
                        new_table += m + "<br/>";
                    new_table += "</font></td>";

                    new_table += "<tr>";
                }

                new_table += "</table>";
            }
            return new_table;
        }

        private void Send(string tot, string subject, string msg, bool html = false)
        {
            MailAddress to = new MailAddress(tot);
            MailAddress from = new MailAddress(userMail);
            MailMessage mail = new MailMessage(from, to);
            mail.Subject = subject;
            mail.Body = msg;
            SmtpClient client = new SmtpClient(serverUrl);
            client.EnableSsl = true;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(userMail, pwd);
            mail.IsBodyHtml = html;

            uint retries = 0;
            bool sent = false;
            while (!sent)
            {
                try
                {
                    client.Send(mail);
                    sent = true;
                }
                catch (System.Net.Mail.SmtpException ex)
                {
                    retries++;
                    if (retries == retryCount)
                        Trace.WriteLine("Failed to send email: " + ex.Message);
                }
            }
            client.Dispose();
        }
    }
}
