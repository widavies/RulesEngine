// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RulesEngine.Exceptions;
using RulesEngine.HelperFunctions;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core.Exceptions;
using System.Linq.Expressions;

namespace RulesEngine.ExpressionBuilders
{
    
    // The RegexCapture expression expects at least one capture group in the input regex
    // that will be matched and set as the rule name when evaluated.
    internal sealed class RegexCaptureExpressionBuilder : RuleExpressionBuilderBase
    {
        private readonly ReSettings _reSettings;
        private readonly RuleExpressionParser _ruleExpressionParser;

        internal RegexCaptureExpressionBuilder(ReSettings reSettings, RuleExpressionParser ruleExpressionParser)
        {
            _reSettings = reSettings;
            _ruleExpressionParser = ruleExpressionParser;
        }

        internal override RuleFunc<RuleResultTree> BuildDelegateForRule(Rule rule, RuleParameter[] ruleParams)
        {
            try
            {
                if (ruleParams.Length == 0)
                {
                    throw new ArgumentException(
                        "At least one parameter must be provided in order to use RegexExpression");
                }

                var first = ruleParams.First();

                if (first.Type != typeof(string))
                {
                    throw new ArgumentException("First parameter must be a string in order to use RegexExpression");
                }
                
                string ApplyRegexMatch(string expression)
                {
                    // return rule.CaseSensitiveRegex ?
                    //     $"BuiltInCustomTypes.RegexMatchSensitive({first.Name}, {expression})" :
                    return $"BuiltInCustomTypes.RegexCaptureCaseInsensitive({first.Name}, {expression})";
                }

                var ruleDelegate = _ruleExpressionParser.Compile<Tuple<bool, string, string>>(
                    ApplyRegexMatch(Utils.ExpandReferences(rule.Expression)), ruleParams);
                return Helpers.ToResultTree(_reSettings, rule, null, ruleDelegate);
            }
            catch (Exception ex)
            {
                Helpers.HandleRuleException(ex, rule, _reSettings);

                var exceptionMessage = Helpers.GetExceptionMessage(
                    $"Exception while parsing expression `{rule?.Expression}` - {ex.Message}",
                    _reSettings);

                bool func(object[] param) => false;

                return Helpers.ToResultTree(_reSettings, rule, null, func, exceptionMessage);
            }
        }

        internal override LambdaExpression Parse(string expression, ParameterExpression[] parameters, Type returnType)
        {
            try
            {
                if (parameters.Length == 0)
                {
                    throw new ArgumentException(
                        "At least one (scoped) parameter must be provided in order to use RegexExpression");
                }

                var first = parameters.First();

                string ApplyRegexMatch(string expr)
                {
                    // return rule.CaseSensitiveRegex ?
                    //     $"BuiltInCustomTypes.RegexMatchSensitive({first.Name}, {expression})" :
                    return $"BuiltInCustomTypes.RegexCaptureCaseInsensitive({first.Name}, {expr})";
                }
                
                return _ruleExpressionParser.Parse(
                    ApplyRegexMatch(Utils.ExpandReferences(expression)),
                    parameters,
                    returnType);
            }
            catch (ParseException ex)
            {
                throw new ExpressionParserException(ex.Message, expression);
            }
        }

        internal override Func<object[], Dictionary<string, object>> CompileScopedParams(RuleParameter[] ruleParameters,
            RuleExpressionParameter[] scopedParameters)
        {
            return _ruleExpressionParser.CompileRuleExpressionParameters(ruleParameters, scopedParameters);
        }
    }
}