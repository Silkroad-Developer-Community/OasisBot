using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using Python.Runtime;

namespace RSBot.Python.API
{
    internal static class PythonData
    {
        public static PyDict Dict(params (string Key, object Value)[] values)
        {
            using (Py.GIL())
            {
                var result = new PyDict();
                foreach (var (key, value) in values)
                {
                    using var pyKey = key.ToPython();
                    using var pyValue = ToPython(value);
                    result.SetItem(pyKey, pyValue);
                }
                return result;
            }
        }

        public static PyList List(IEnumerable values, Func<object, PyObject> converter = null)
        {
            using (Py.GIL())
            {
                var result = new PyList();
                if (values == null)
                    return result;
                foreach (var value in values)
                {
                    using var py = converter != null ? converter(value) : ToPython(value);
                    result.Append(py);
                }
                return result;
            }
        }

        public static PyObject ToPython(object value)
        {
            using (Py.GIL())
            {
                if (value == null)
                    return string.Empty.ToPython();
                if (value is PyObject py)
                    return py;
                return value.ToPython();
            }
        }

        public static object Get(object source, string name, object fallback = null)
        {
            if (source == null || string.IsNullOrWhiteSpace(name))
                return fallback;
            try
            {
                var p = source
                    .GetType()
                    .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                return p?.GetValue(source) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public static object GetAny(object source, object fallback, params string[] names)
        {
            foreach (var name in names)
            {
                var value = Get(source, name, null);
                if (value != null)
                    return value;
            }
            return fallback;
        }

        public static string Str(object source, string fallback, params string[] names) =>
            Convert.ToString(GetAny(source, fallback, names), CultureInfo.InvariantCulture) ?? fallback;

        public static int Int(object source, int fallback, params string[] names)
        {
            try
            {
                return Convert.ToInt32(GetAny(source, fallback, names), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static long Long(object source, long fallback, params string[] names)
        {
            try
            {
                return Convert.ToInt64(GetAny(source, fallback, names), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static double Double(object source, double fallback, params string[] names)
        {
            try
            {
                return Convert.ToDouble(GetAny(source, fallback, names), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static bool Bool(object source, bool fallback, params string[] names)
        {
            try
            {
                return Convert.ToBoolean(GetAny(source, fallback, names), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static IEnumerable AsEnumerable(object value)
        {
            if (value is IEnumerable enumerable && value is not string)
                return enumerable;
            return Array.Empty<object>();
        }
    }
}
