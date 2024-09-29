using System;

namespace DELTation.AAAARP.Utils
{
    internal static class PassUtils
    {
        public static string CreateAutoName(Type passType)
        {
            string name = passType.Name;
            const string passSubstring = "Pass";
            if (name.EndsWith(passSubstring))
            {
                name = name[..^passSubstring.Length];
            }
            return name;
        }
    }
}