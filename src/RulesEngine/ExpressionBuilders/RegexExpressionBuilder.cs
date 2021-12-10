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
    internal sealed class RegexExpressionBuilder : RuleExpressionBuilderBase
    {
        private readonly ReSettings _reSettings;
        private readonly RuleExpressionParser _ruleExpressionParser;

        internal RegexExpressionBuilder(ReSettings reSettings, RuleExpressionParser ruleExpressionParser)
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

                // Loosely speaking, when RuleExpressionType is RegexExpression, "Expression" is not _actually_
                // an expression, it is a (regex) string. The reason for "loosely" here is that there are two exceptions:
                // 1) Any (unescaped) instances of <ref> will be replaced with the text matched by ref's regex expression.
                //    <ref> must be of type "RegexExpression" or "RegexCaptureExpression"
                // 2) Any (unescaped) instances of %ref% will be replaced with the (matched) RuleName of ref.
                //
                // Technically, <ref> typically references a ExclusiveOr rule with a list of sub rules, and will
                // be set to whichever rule evaluated to true.

                string ApplyRegexMatch(string expression)
                {
                    // return rule.CaseSensitiveRegex ?
                    //     $"BuiltInCustomTypes.RegexMatchSensitive({first.Name}, {expression})" :
                    return
                        $"BuiltInCustomTypes.RegexMatchCaseInsensitive({first.Name}, {expression}, {Utils.RequiresToExpression(rule.Requires, null)})";
                }
                
                var ruleDelegate = _ruleExpressionParser.Compile<ValueTuple<bool, string>>(
                    ApplyRegexMatch(Utils.ExpandReferences(rule.Expression)), ruleParams);
                return Helpers.ToResultTree1(_reSettings, rule, null, ruleDelegate);
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

                if (first.Type != typeof(string))
                {
                    throw new ArgumentException(
                        "First (scoped) parameter must be string in order to use RegexExpression");
                }

                string ApplyRegexMatch(string expr)
                {
                    // return rule.CaseSensitiveRegex ?
                    //     $"BuiltInCustomTypes.RegexMatchSensitive({first.Name}, {expression})" :
                    return $"BuiltInCustomTypes.RegexMatchCaseInsensitive({first.Name}, {expr}, true)";
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