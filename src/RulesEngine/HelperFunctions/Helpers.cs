﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RulesEngine.Exceptions;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RulesEngine.HelperFunctions
{
    /// <summary>
    /// Helpers
    /// </summary>
    internal static class Helpers
    {
        internal static RuleFunc<RuleResultTree> ToResultTree(ReSettings reSettings, Rule rule,
            IEnumerable<RuleResultTree> childRuleResults, Func<object[], bool> isSuccessFunc,
            string exceptionMessage = "")
        {
            return (inputs) => {
                var isSuccess = false;
                var inputsDict = new Dictionary<string, object>();
                try
                {
                    inputsDict = inputs.ToDictionary(c => c.Name, c => c.Value);
                    isSuccess = isSuccessFunc(inputs.Select(c => c.Value).ToArray());
                }
                catch (Exception ex)
                {
                    exceptionMessage =
                        GetExceptionMessage($"Error while executing rule : {rule?.RuleName} - {ex.Message}",
                            reSettings);
                    HandleRuleException(new RuleException(exceptionMessage, ex), rule, reSettings);
                    isSuccess = false;
                }

                return new RuleResultTree {
                    Rule = rule,
                    Inputs = inputsDict,
                    IsSuccess = isSuccess,
                    ChildResults = childRuleResults,
                    ExceptionMessage = exceptionMessage
                };
            };
        }

        internal static RuleFunc<RuleResultTree> ToResultTree1(ReSettings reSettings, Rule rule,
            IEnumerable<RuleResultTree> childRuleResults, Func<object[], ValueTuple<bool, string, ValueTuple<int, int>>> isSuccessFunc,
            string exceptionMessage = "")
        {
            return (inputs) => {
                bool isSuccess;
                string regexMatched;
                (int, int) regexRange;
                var inputsDict = new Dictionary<string, object>();
                try
                {
                    inputsDict = inputs.ToDictionary(c => c.Name, c => c.Value);

                    var res = isSuccessFunc(inputs.Select(c => c.Value).ToArray());

                    isSuccess = res.Item1;
                    regexMatched = res.Item2;
                    regexRange = res.Item3;
                }
                catch (Exception ex)
                {
                    exceptionMessage =
                        GetExceptionMessage($"Error while executing rule : {rule?.RuleName} - {ex.Message}",
                            reSettings);
                    HandleRuleException(new RuleException(exceptionMessage, ex), rule, reSettings);
                    isSuccess = false;
                    regexMatched = null;
                    regexRange = (-1, -1);
                }

                return new RuleResultTree {
                    Rule = rule,
                    Inputs = inputsDict,
                    IsSuccess = isSuccess,
                    RegexMatched = regexMatched,
                    RegexMatchedRange = regexRange,
                    ChildResults = childRuleResults,
                    ExceptionMessage = exceptionMessage
                };
            };
        }
        
        internal static RuleFunc<RuleResultTree> ToResultTree3(ReSettings reSettings, Rule rule,
            IEnumerable<RuleResultTree> childRuleResults,
            Func<object[], ValueTuple<bool, string, ValueTuple<int, int>, string>> isSuccessFunc, Func<object[], bool> requiresDelegate,
            string exceptionMessage = "")
        {
            return (inputs) => {
                bool isSuccess;
                var requiresSuccess = true;
                string regexMatched;
                ValueTuple<int, int> regexRange;
                string regexCapture;
                var inputsDict = new Dictionary<string, object>();
                try
                {
                    inputsDict = inputs.ToDictionary(c => c.Name, c => c.Value);

                    var res = isSuccessFunc(inputs.Select(c => c.Value).ToArray());
                    
                    isSuccess = res.Item1;
                    regexMatched = res.Item2;
                    regexRange = res.Item3;
                    regexCapture = res.Item4;

                    // TODO fail if no capture

                    if (requiresDelegate != null)
                    {
                        var requires = requiresDelegate(inputs.Select(c => c.Value).Concat(new []{regexCapture}).ToArray());
                        
                       requiresSuccess = requires;
                    }
                }
                catch (Exception ex)
                {
                    exceptionMessage =
                        GetExceptionMessage($"Error while executing rule : {rule?.RuleName} - {ex.Message}",
                            reSettings);
                    HandleRuleException(new RuleException(exceptionMessage, ex), rule, reSettings);
                    isSuccess = false;
                    regexMatched = null;
                    regexCapture = null;
                    regexRange = (-1, -1);
                }

                return new RuleResultTree {
                    Rule = rule,
                    Inputs = inputsDict,
                    IsSuccess = isSuccess && requiresSuccess,
                    RegexMatched = regexMatched,
                    RegexCapture = regexCapture,
                    RegexMatchedRange = regexRange,
                    ChildResults = childRuleResults,
                    ExceptionMessage = exceptionMessage
                };
            };
        }

        internal static RuleFunc<RuleResultTree> ToRuleExceptionResult(ReSettings reSettings, Rule rule, Exception ex)
        {
            HandleRuleException(ex, rule, reSettings);
            return ToResultTree(reSettings, rule, null, (args) => false, ex.Message);
        }

        internal static void HandleRuleException(Exception ex, Rule rule, ReSettings reSettings)
        {
            ex.Data.Add(nameof(rule.RuleName), rule.RuleName);
            ex.Data.Add(nameof(rule.Expression), rule.Expression);

            if (!reSettings.EnableExceptionAsErrorMessage)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="message"></param>
        /// <param name="rule"></param>
        /// <param name="reSettings"></param>
        /// <returns></returns>
        internal static string GetExceptionMessage(string message, ReSettings reSettings)
        {
            return reSettings.IgnoreException ? "" : message;
        }

        /// <summary>
        /// To the result tree error messages
        /// </summary>
        /// <param name="ruleResultTree">ruleResultTree</param>
        /// <param name="ruleResultMessage">ruleResultMessage</param>
        [Obsolete]
        internal static void ToResultTreeMessages(RuleResultTree ruleResultTree,
            ref RuleResultMessage ruleResultMessage)
        {
            if (ruleResultTree.ChildResults != null)
            {
                GetChildRuleMessages(ruleResultTree.ChildResults, ref ruleResultMessage);
            }
            else
            {
                if (!ruleResultTree.IsSuccess)
                {
                    string errMsg = ruleResultTree.Rule.ErrorMessage;
                    errMsg = string.IsNullOrEmpty(errMsg)
                        ? $"Error message is not configured for {ruleResultTree.Rule.RuleName}"
                        : errMsg;

                    if (ruleResultTree.Rule.ErrorType == ErrorType.Error &&
                        !ruleResultMessage.ErrorMessages.Contains(errMsg))
                    {
                        ruleResultMessage.ErrorMessages.Add(errMsg);
                    }
                    else if (ruleResultTree.Rule.ErrorType == ErrorType.Warning &&
                             !ruleResultMessage.WarningMessages.Contains(errMsg))
                    {
                        ruleResultMessage.WarningMessages.Add(errMsg);
                    }
                }
            }
        }

        /// <summary>
        /// To get the child error message recursively
        /// </summary>
        /// <param name="childResultTree">childResultTree</param>
        /// <param name="ruleResultMessage">ruleResultMessage</param>
        [Obsolete]
        private static void GetChildRuleMessages(IEnumerable<RuleResultTree> childResultTree,
            ref RuleResultMessage ruleResultMessage)
        {
            foreach (var item in childResultTree)
            {
                ToResultTreeMessages(item, ref ruleResultMessage);
            }
        }
    }
}