// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace RulesEngine.CustomTypes
{
    public static class BuiltInCustomTypes
    {
        public static bool RegexMatchCaseInsensitive(string input, string pattern)
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
        }

        public static bool RegexMatchCaseSensitive(string input, string pattern)
        {
            return Regex.IsMatch(input, pattern);
        }
    }
}