// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace RulesEngine.Models
{
    /// <summary>
    /// This is rule expression type which will use in rule config files 
    /// </summary>
    public enum RuleExpressionType
    {
        LambdaExpression = 0,
        RegexExpression = 1,
        RegexCasedExpression = 2
    }
}
