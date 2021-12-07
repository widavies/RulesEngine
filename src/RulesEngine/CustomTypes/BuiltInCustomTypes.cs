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

        public static (bool, string) RegexCaptureCaseInsensitive(string input, string pattern)
        {
            var match = Regex.Match(input, pattern);

            var success = match.Success && match.Groups.Count > 0;
            
            return (success, success ? match.Groups[1].Value : null);
        }
    }
}