using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GZipWorker
{
    internal class ConcurrentDictionary<TK, T>
    {
        private readonly Dictionary<TK, T> _dict = new Dictionary<TK, T>();
        private readonly object _lockObg = new object();

        public T this[TK index]
        {
            get { return _dict[index]; }
            set { _dict[index] = value; }
        }

        public void AddToDictionary(TK index, T stream)
        {
            Monitor.Enter(_lockObg);
            if (!_dict.ContainsKey(index))
            {
                _dict.Add(index, stream);
            }
            Monitor.Exit(_lockObg);
        }

        public void RemoveFromDictionary(TK index)
        {
            Monitor.Enter(_lockObg);
            if (_dict.ContainsKey(index))
            {
                _dict.Remove(index);
            }
            Monitor.Exit(_lockObg);
        }

        public bool ContainsKey(TK key)
        {
            Monitor.Enter(_lockObg);
            var res= _dict.ContainsKey(key);
            Monitor.Exit(_lockObg);
            return res;
        }

        public KeyValuePair<TK, T> FirstOrDefault()
        {
            Monitor.Enter(_lockObg);
            var res = _dict.FirstOrDefault();
            Monitor.Exit(_lockObg);
            return res;
        }
    }
}