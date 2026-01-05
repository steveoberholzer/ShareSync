using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Tecala.SMO.ShareSync.Services
{
    internal class ErrorNumberResolver
    {
        private Dictionary<string, int> _values;

        internal ErrorNumberResolver(object o, int errorNumber = 50000)
        {
            _values = new Dictionary<string, int>();
            HashSet<string> excludedMethods = new HashSet<string>
            {
                "Equals",
                "GetHashCode",
                "GetType",
                "ToString"
            };

            foreach (var method in o.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => !m.IsSpecialName && !excludedMethods.Contains(m.Name))
                .OrderBy(m => m.Name))
            {
                _values.Add(method.Name, errorNumber);
                errorNumber++;
            }
        }

        internal int GetErrorNumber()
        {
            var method = new StackTrace().GetFrame(1).GetMethod();
            if (method != null)
            {
                string name = method.Name;
                if (_values.ContainsKey(name))
                    return _values[name];
            }
            return -1;
        }
    }
}
