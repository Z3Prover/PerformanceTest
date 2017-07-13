using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{
    public class Tags
    {
        protected readonly Dictionary<string, int> _tags;
        protected readonly Dictionary<int, string> _ids;

        public Tags()
        {
            Dictionary<string, int> _tags = new Dictionary<string, int>();
            Dictionary<int, string> _ids = new Dictionary<int, string>();
        }

        public Tags(Dictionary<int, string> idToName)
        {
            if (idToName == null) throw new ArgumentNullException(nameof(idToName));
            _ids = idToName;
            _tags = new Dictionary<string, int>();
            foreach (var item in _ids)
            {
                _tags[item.Value] = item.Key;
            }
        }

        public void Insert(string name, int id)
        {
            _tags[name] = id;
            _ids[id] = name;
        }

        public Dictionary<string, int>.Enumerator GetEnumerator()
        {
            return _tags.GetEnumerator();
        }

        public bool HasTag(string tag)
        {
            return _tags.ContainsKey(tag);
        }

        public bool HasID(int id)
        {
            return _ids.ContainsKey(id);
        }

        public string GetName(int id)
        {
            if (_ids.ContainsKey(id))
                return _ids[id];
            else
                throw new Exception("ID not in dictionary.");
        }

        public int Get(string name)
        {
            if (_tags.ContainsKey(name))
                return _tags[name];
            else
                throw new Exception("Tag not in dictionary.");
        }

        public string Name(int id)
        {
            if (HasID(id))
                return _ids[id];
            else
                return "#" + id.ToString();
        }
    }
}
