using System;
using System.Text;
using Python.Runtime;

namespace RSBot.Python.API.Handler
{
    public static class PythonErrorHandler
    {
        public static string FormatException(Exception ex)
        {
            if (ex is not PythonException py)
                return ex.ToString();

            try
            {
                return $"{py.Type}: {py.Message}\n{py.StackTrace}";
            }
            catch
            {
                return py.ToString();
            }
        }
    }
}
