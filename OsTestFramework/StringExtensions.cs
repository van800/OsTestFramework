using System;
using System.Collections.Generic;

namespace JetBrains.OsTestFramework
{
    public static class StringExtensions
    {
        public static string Join(this string[] strings, string separator = " ")
        {
            if (strings == null) return String.Empty;
            return String.Join(separator, strings);
        }
    }
}