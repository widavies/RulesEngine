// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RulesEngine.ExpressionBuilders;
using RulesEngine.Models;
using System;

namespace RulesEngine
{
    internal class RuleExpressionBuilderFactory
    {
        private readonly ReSettings _reSettings;
        private readonly LambdaExpressionBuilder _lambdaExpressionBuilder;
        private readonly RegexExpressionBuilder _regexExpressionBuilder;
        private readonly RegexCaptureExpressionBuilder _regexCaptureExpressionBuilder;
        
        public RuleExpressionBuilderFactory(ReSettings reSettings, RuleExpressionParser expressionParser)
        {
            _reSettings = reSettings;
            _lambdaExpressionBuilder = new LambdaExpressionBuilder(_reSettings, expressionParser);
            _regexExpressionBuilder = new RegexExpressionBuilder(_reSettings, expressionParser);
            _regexCaptureExpressionBuilder = new RegexCaptureExpressionBuilder(_reSettings, expressionParser);
        }
        public RuleExpressionBuilderBase RuleGetExpressionBuilder(RuleExpressionType ruleExpressionType)
        {
            switch (ruleExpressionType)
            {
                case RuleExpressionType.LambdaExpression:
                    return _lambdaExpressionBuilder;
                case RuleExpressionType.RegexExpression:
                    return _regexExpressionBuilder;
                case RuleExpressionType.RegexCaptureExpression:
                    return _regexCaptureExpressionBuilder;
                default:
                    throw new InvalidOperationException($"{nameof(ruleExpressionType)} has not been supported yet.");
            }
        }
    }
}
