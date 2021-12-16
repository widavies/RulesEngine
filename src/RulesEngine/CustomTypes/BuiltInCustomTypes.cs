// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.RegularExpressions;

namespace RulesEngine.CustomTypes
{
    public static class BuiltInCustomTypes
    {
        public static (bool, string, ValueTuple<int, int>) RegexMatchCaseInsensitive(string input, string pattern,
            bool additional)
        {
            var match = Regex.Match(input, pattern);

            return (match.Success && additional, match.Success ? match.Value : null,
                match.Success ? (match.Index, match.Length) : (-1, -1));
        }

        public static (bool, string, ValueTuple<int, int>, string) RegexCaptureCaseInsensitive(string input,
            string pattern, bool additional)
        {
            var match = Regex.Match(input, pattern);

            var success = match.Success && match.Groups.Count > 0;

            return (success && additional, success ? match.Value : null,
                match.Success ? (match.Index, match.Length) : (-1, -1),
                success ? match.Groups[1].Value : null);
        }
    }
}