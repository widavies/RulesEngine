// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace RulesEngine.CustomTypes
{
    public static class BuiltInCustomTypes
    {
        public static (bool, string) RegexMatchCaseInsensitive(string input, string pattern, bool additional)
        {
            var match = Regex.Match(input, pattern);
            
            return (match.Success && additional, match.Success ? match.Value : null);
        }
        
        public static (bool, string, string) RegexCaptureCaseInsensitive(string input, string pattern, bool additional)
        {
            var match = Regex.Match(input, pattern);

            var success = match.Success && match.Groups.Count > 0;
            
            return (success && additional, success ? match.Value : null, success ? match.Groups[1].Value : null);
        }
    }
}