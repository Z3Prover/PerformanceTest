using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Packaging;
using System.Data;

namespace Z3Data
{
    public class CSVRow
    {
        string[] _data = null;
        public CSVRow(string[] s) { _data = new string[12]; s.CopyTo(_data, 0); }
        public string Filename { get { return _data[0]; } }
        public int ReturnValue { get { return Convert.ToInt32(_data[1]); } }
        public double Runtime { get { return Convert.ToDouble(_data[2]); } }
        public uint ResultCode { get { return Convert.ToUInt32(_data[3]); } }
        public uint SAT { get { return Convert.ToUInt32(_data[4]); } }
        public uint UNSAT { get { return Convert.ToUInt32(_data[5]); } }
        public uint UNKNOWN { get { return Convert.ToUInt32(_data[6]); } }
        public uint TargetSAT { get { return Convert.ToUInt32(_data[7]); } }
        public uint TargetUNSAT { get { return Convert.ToUInt32(_data[8]); } }
        public uint TargetUNKNOWN { get { return Convert.ToUInt32(_data[9]); } }
        public string StdOut { get { return _data[10]; } }
        public string StdErr { get { return _data[11]; } }

        public string[] Data { get { return _data; } }
    };

    public class CSVRowList : List<CSVRow> { };

    public class CSVData
    {
        Uri _zipUri = null;
        string _zipFilename = null;
        uint _id = 0;
        string[] _columnNames = null;
        CSVRowList _rows = null;

        public CSVData(string zipFilename, uint id)
        {
            _zipUri = PackUriHelper.CreatePartUri(new Uri(id.ToString() + ".csv", UriKind.Relative));
            _zipFilename = zipFilename;
            _id = id;

            LoadRows();
        }

        public string[] ColumnNames { get { return _columnNames; } }

        public CSVRowList Rows { get { return _rows; } }

        private void LoadRows()
        {
            using (Package pkg = Package.Open(_zipFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                PackagePart part = pkg.GetPart(_zipUri);
                using (Stream s = part.GetStream(FileMode.Open, FileAccess.Read))
                using (StreamReader r = new StreamReader(s))
                {
                    if (r.EndOfStream)
                        throw new Exception("CSV header missing");

                    string line = r.ReadLine();
                    _columnNames = line.Split(',');

                    int i, j, c;
                    bool inString = false, haveString = false;
                    _rows = new CSVRowList();
                    string[] objects = new string[12];
                    while (!r.EndOfStream)
                    {
                        line = r.ReadLine();
                        j = 0; c = 0; haveString = false;
                        for (i = 0; i < line.Length; i++)
                        {
                            if (!inString)
                            {
                                if (line[i] == ',')
                                {
                                    if (haveString)
                                        objects[c] = line.Substring(j + 1, i - j - 2);
                                    else
                                        objects[c] = line.Substring(j, i - j);
                                    c++;
                                    j = i + 1;
                                    haveString = false;
                                }
                                else if (line[i] == '\"')
                                {
                                    inString = true;
                                    haveString = true;
                                }
                            }
                            else
                            {
                                if (line[i] == '\\')
                                    i++;
                                else if (line[i] == '\"')
                                    inString = false;
                            }
                        }
                        _rows.Add(new CSVRow(objects));
                    }
                }
            }
        }
    }
}
