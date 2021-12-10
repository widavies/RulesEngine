// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RulesEngine.Actions;
using RulesEngine.CustomTypes;
using RulesEngine.Exceptions;
using RulesEngine.ExpressionBuilders;
using RulesEngine.HelperFunctions;
using RulesEngine.Interfaces;
using RulesEngine.Models;
using RulesEngine.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RulesEngine
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="IRulesEngine" />
    public class RulesEngine : IRulesEngine
    {
        #region Variables
        private readonly ILogger _logger;
        private readonly ReSettings _reSettings;
        private readonly RulesCache _rulesCache = new RulesCache();
        private readonly RuleExpressionParser _ruleExpressionParser;
        private readonly RuleCompiler _ruleCompiler;
        private readonly ActionFactory _actionFactory;
        private const string ParamParseRegex = "(\\$\\(.*?\\))";
        #endregion

        #region Constructor
        public RulesEngine(string[] jsonConfig, ILogger logger = null, ReSettings reSettings = null) : this(logger, reSettings)
        {
            var workflow = jsonConfig.Select(item => JsonConvert.DeserializeObject<Workflow>(item)).ToArray();
            AddWorkflow(workflow);
        }

        public RulesEngine(Workflow[] Workflows, ILogger logger = null, ReSettings reSettings = null) : this(logger, reSettings)
        {
            AddWorkflow(Workflows);
        }

        public RulesEngine(ILogger logger = null, ReSettings reSettings = null)
        {
            _logger = logger ?? new NullLogger<RulesEngine>();
            _reSettings = reSettings ?? new ReSettings();
            _ruleExpressionParser = new RuleExpressionParser(_reSettings);
            _ruleCompiler = new RuleCompiler(new RuleExpressionBuilderFactory(_reSettings, _ruleExpressionParser),_reSettings, _logger);
            _actionFactory = new ActionFactory(GetActionRegistry(_reSettings));
            
            var builtInTypes = new[] {typeof(BuiltInCustomTypes), typeof(string), typeof(Regex)};
            
            _reSettings.CustomTypes = _reSettings.CustomTypes?.Concat(builtInTypes).ToArray() ?? builtInTypes;
        }

        private IDictionary<string, Func<ActionBase>> GetActionRegistry(ReSettings reSettings)
        {
            var actionDictionary = GetDefaultActionRegistry();
            var customActions = reSettings.CustomActions ?? new Dictionary<string, Func<ActionBase>>();
            foreach (var customAction in customActions)
            {
                actionDictionary.Add(customAction);
            }
            return actionDictionary;

        }
        #endregion

        #region Public Methods

        /// <summary>
        /// This will execute all the rules of the specified workflow
        /// </summary>
        /// <param name="workflowName">The name of the workflow with rules to execute against the inputs</param>
        /// <param name="inputs">A variable number of inputs</param>
        /// <returns>List of rule results</returns>
        public async ValueTask<List<RuleResultTree>> ExecuteAllRulesAsync(string workflowName, params object[] inputs)
        {
            _logger.LogTrace($"Called {nameof(ExecuteAllRulesAsync)} for workflow {workflowName} and count of input {inputs.Count()}");

            var ruleParams = new List<RuleParameter>();

            for (var i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                ruleParams.Add(new RuleParameter($"input{i + 1}", input));
            }

            return await ExecuteAllRulesAsync(workflowName, ruleParams.ToArray());
        }

        /// <summary>
        /// This will execute all the rules of the specified workflow
        /// </summary>
        /// <param name="workflowName">The name of the workflow with rules to execute against the inputs</param>
        /// <param name="ruleParams">A variable number of rule parameters</param>
        /// <returns>List of rule results</returns>
        public async ValueTask<List<RuleResultTree>> ExecuteAllRulesAsync(string workflowName, params RuleParameter[] ruleParams)
        {
            var ruleResultList = ValidateWorkflowAndExecuteRule(workflowName, ruleParams);
            await ExecuteActionAsync(ruleResultList);
            return ruleResultList;
        }

        private async ValueTask ExecuteActionAsync(IEnumerable<RuleResultTree> ruleResultList)
        {
            foreach (var ruleResult in ruleResultList)
            {
                if(ruleResult.ChildResults !=  null)
                {
                    await ExecuteActionAsync(ruleResult.ChildResults);
                }
                var actionResult = await ExecuteActionForRuleResult(ruleResult, false);
                ruleResult.ActionResult = new ActionResult {
                    Output = actionResult.Output,
                    Exception = actionResult.Exception
                };
            }
        }

        public async ValueTask<ActionRuleResult> ExecuteActionWorkflowAsync(string workflowName, string ruleName, RuleParameter[] ruleParameters)
        {
            var compiledRule = CompileRule(workflowName, ruleName, ruleParameters);
            var resultTree = compiledRule(ruleParameters);
            return await ExecuteActionForRuleResult(resultTree, true);
        }

        private async ValueTask<ActionRuleResult> ExecuteActionForRuleResult(RuleResultTree resultTree, bool includeRuleResults = false)
        {
            var ruleActions = resultTree?.Rule?.Actions;
            var actionInfo = resultTree?.IsSuccess == true ? ruleActions?.OnSuccess : ruleActions?.OnFailure;

            if (actionInfo != null)
            {
                var action = _actionFactory.Get(actionInfo.Name);
                var ruleParameters = resultTree.Inputs.Select(kv => new RuleParameter(kv.Key, kv.Value)).ToArray();
                return await action.ExecuteAndReturnResultAsync(new ActionContext(actionInfo.Context, resultTree), ruleParameters, includeRuleResults);
            }
            else
            {
                //If there is no action,return output as null and return the result for rule
                return new ActionRuleResult {
                    Output = null,
                    Results = includeRuleResults ? new List<RuleResultTree>() { resultTree } : null
                };
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Adds the workflow if the workflow name is not already added. Ignores the rest.
        /// </summary>
        /// <param name="workflows">The workflow rules.</param>
        /// <exception cref="RuleValidationException"></exception>
        public void AddWorkflow(params Workflow[] workflows)
        {
            // try
            // {
                foreach (var workflow in workflows)
                {                    
                    var validator = new WorkflowsValidator();
                    validator.ValidateAndThrow(workflow);
                    if (!_rulesCache.ContainsWorkflows(workflow.WorkflowName))
                    {
                        _rulesCache.AddOrUpdateWorkflows(workflow.WorkflowName, workflow);
                    }
                    else
                    {
                        throw new ValidationException($"Cannot add workflow `{workflow.WorkflowName}` as it already exists. Use `AddOrUpdateWorkflow` to update existing workflow");
                    }
                }
            // }
            // catch (ValidationException ex)
            // {
            //     throw new RuleValidationException(ex.Message, ex.Errors);
            // }
        }

        /// <summary>
        /// Adds new workflow rules if not previously added.
        /// Or updates the rules for an existing workflow.
        /// </summary>
        /// <param name="workflows">The workflow rules.</param>
        /// <exception cref="RuleValidationException"></exception>
        public void AddOrUpdateWorkflow(params Workflow[] workflows)
        {
            try
            {
                foreach (var workflow in workflows)
                {
                    var validator = new WorkflowsValidator();
                    validator.ValidateAndThrow(workflow);
                    _rulesCache.AddOrUpdateWorkflows(workflow.WorkflowName, workflow);
                }
            }
            catch (ValidationException ex)
            {
                throw new RuleValidationException(ex.Message, ex.Errors);
            }
        }

        public List<string> GetAllRegisteredWorkflowNames()
        {
            return _rulesCache.GetAllWorkflowNames();
        }

        /// <summary>
        /// Checks is workflow exist.
        /// </summary>
        /// <param name="workflowName">The workflow name.</param>
        /// <returns> <c>true</c> if contains the specified workflow name; otherwise, <c>false</c>.</returns>
        public bool ContainsWorkflow(string workflowName)
        {
            return _rulesCache.ContainsWorkflows(workflowName);
        }

        /// <summary>
        /// Clears the workflow.
        /// </summary>
        public void ClearWorkflows()
        {
            _rulesCache.Clear();
        }

        /// <summary>
        /// Removes the workflows.
        /// </summary>
        /// <param name="workflowNames">The workflow names.</param>
        public void RemoveWorkflow(params string[] workflowNames)
        {
            foreach (var workflowName in workflowNames)
            {
                _rulesCache.Remove(workflowName);
            }
        }
 /// <summary>
        /// Some rules reference other rules. Rules are executed one by one.
        /// This function determines what that order is. If there is
        /// no possible execution plan (a circular dependency exists),
        /// an exception is thrown.
        ///
        /// There is one primary restriction here - only top level rules may be referenced.
        /// This is because all the sub ruled are executed immediately and there isn't yet a feature
        /// to execute them one by one, as would be needed to do otherwise.
        /// </summary>
        /// <param name="rules">A list of user provided rules that may be inter-depended and need to be executed.</param>
        /// <returns></returns>
        private List<Rule> CreateExecutionPlan(IEnumerable<Rule> rules)
        {
            var enumerable = rules as Rule[] ?? rules.ToArray();
            var graph = new Graph<string, Rule>();
            
            var keys = enumerable.Select(x => x.RuleName).ToList();
            
            foreach (var rule in enumerable)
            {
                // Determine the dependencies of a node by what nodes it references.
                // A node is considered to reference another if its Expression or any of its children's Expressions
                // contain the unescaped rule name of another

                var dependencies = new List<string>();

                var exploring = new Queue<Rule>();
                exploring.Enqueue(rule);

                while (exploring.Count != 0)
                {
                    var node = exploring.Dequeue();

                    if (node.Expression != null)
                    {
                        dependencies.AddRange(keys.Where(key =>
                            // Regex checks for unescaped (not-quoted) variable names referencing the rule
                            Utils.References(node.Expression, key)));
                    }
                    
                    if (node.Rules != null)
                    {
                        foreach (var child in node.Rules)
                        {
                            exploring.Enqueue(child);
                        }
                    }
                }
                
                graph.AddNode(rule.RuleName, rule, dependencies);
            }

            var executionOrder = new List<Rule>();
            
            // From here, use the graph to decide execution order
            var result = graph.TopologicalSort();
            foreach (var layer in result.layers)
            {
                executionOrder.AddRange(layer.Select(x => graph[x]));
            }
            
            return executionOrder;
        }
        
        /// <summary>
        /// This will validate workflow rules then call execute method
        /// </summary>
        /// <typeparam name="T">type of entity</typeparam>
        /// <param name="input">input</param>
        /// <param name="workflowName">workflow name</param>
        /// <returns>list of rule result set</returns>
        private List<RuleResultTree> ValidateWorkflowAndExecuteRule(string workflowName, RuleParameter[] ruleParams)
        {
            var workflow = _rulesCache.GetWorkflow(workflowName);
            if (workflow != null)
            {
                _logger.LogTrace($"Compiled rules found for {workflowName} workflow and executed");

                // Decide on execution order
                var result = new List<RuleResultTree>();
                var rules = CreateExecutionPlan(workflow.Rules.Where(r => r.Enabled));

                var intermediateParams = new List<ScopedParam>();

                foreach (var rule in rules)
                {
                    if (RegisterRule(workflowName, rule, intermediateParams.ToArray(), ruleParams))
                    {
                        var compiledRulesCacheKey = GetCompiledRulesKey(workflowName, ruleParams);

                        var compiledRule = _rulesCache.GetCompiledRules(compiledRulesCacheKey)[rule.RuleName];

                        if (compiledRule == null)
                        {
                            _logger.LogTrace("Workflow {Workflow}, Rule {RuleName} was not compiled", workflowName,
                                rule.RuleName);
                            // if rules are not registered with Rules Engine
                            throw new ArgumentException(
                                $"Workflow {workflowName}, Rule {rule.RuleName} was not compiled");
                        }
                        
                        var res = compiledRule(ruleParams);
                        result.Add(res);
                        
                        if (res.IsSuccess && Enum.TryParse(rule.Operator, out ExpressionType nestedOperator) &&
                            nestedOperator == ExpressionType.ExclusiveOr)
                        {
                            // If the rule executed was an ExclusiveOr rule, add 

                            // Get the child rule that executed successfully. We'll call this the "winner"
                            // Because this an ExclusiveOr, there is exactly one winner or the rule fails.
                            var child = res.ChildResults.FirstOrDefault(x => x.IsSuccess);

                            if (child == null)
                            {
                                throw new Exception("ExclusiveOr: No child evaluated to true.");
                            }

                            // Make the rule name of the winner available
                            if (child.RegexCapture != null)
                            {
                                intermediateParams.Add(new ScopedParam {
                                    Name = $"{res.Rule.RuleName}_Name", Expression = $"\"{child.RegexCapture}\""
                                });
                            }
                            else
                            {
                                intermediateParams.Add(new ScopedParam {
                                    Name = $"{res.Rule.RuleName}_Name", Expression = $"\"{child.Rule.RuleName}\""
                                });
                            }
                            
                            // If the winner was regex related, make the string it matched available
                            if (child.RegexMatched != null)
                            {
                                intermediateParams.Add(new ScopedParam {
                                    Name = $"{res.Rule.RuleName}_MatchedRegex", Expression = $"\"{res.RegexMatched}\""
                                });
                            }
                        }
                    }
                    else
                    {
                        _logger.LogTrace($"Rule config file is not present for the {workflowName} workflow");
                        // if rules are not registered with Rules Engine
                        throw new ArgumentException($"Rule config file is not present for the {workflowName} workflow");
                    }
                }

                FormatErrorMessages(result);
                return result;
            }
            else
            {
                _logger.LogTrace($"Workflow {workflowName} not found");
                // if rules are not registered with Rules Engine
                throw new ArgumentException($"Workflow {workflowName} not found");
            }
        }

        /// <summary>
        /// This will compile the rules and store them to dictionary
        /// </summary>
        /// <param name="workflowName">workflow name</param>
        /// <param name="ruleParams">The rule parameters.</param>
        /// <returns>
        /// bool result
        /// </returns>
        private bool RegisterRule(string workflowName, Rule rule, ScopedParam[] intermediateParams,
            params RuleParameter[] ruleParams)
        {
            var compileRulesKey = GetCompiledRulesKey(workflowName, ruleParams);

            var workflow = _rulesCache.GetWorkflow(workflowName);
            if (workflow != null)
            {
                var dictFunc = new Dictionary<string, RuleFunc<RuleResultTree>>();


                var combined = workflow.GlobalParams?.Concat(intermediateParams).ToArray() ??
                                   intermediateParams.ToArray();
                
                dictFunc.Add(rule.RuleName, CompileRule(rule, ruleParams, combined));
                
                _rulesCache.AddOrUpdateCompiledRule(compileRulesKey, dictFunc);
                _logger.LogTrace($"Rules has been compiled for the {workflowName} workflow and added to dictionary");
                return true;
            }
            else
            {
                return false;
            }
        }


        private RuleFunc<RuleResultTree> CompileRule(string workflowName, string ruleName, RuleParameter[] ruleParameters)
        {
            var workflow = _rulesCache.GetWorkflow(workflowName);
            if(workflow == null)
            {
                throw new ArgumentException($"Workflow `{workflowName}` is not found");
            }
            var currentRule = workflow.Rules?.SingleOrDefault(c => c.RuleName == ruleName && c.Enabled);
            if (currentRule == null)
            {
                throw new ArgumentException($"Workflow `{workflowName}` does not contain any rule named `{ruleName}`");
            }
            return CompileRule(currentRule, ruleParameters, workflow.GlobalParams?.ToArray());
        }

        private RuleFunc<RuleResultTree> CompileRule(Rule rule, RuleParameter[] ruleParams, ScopedParam[] scopedParams)
        {
            return _ruleCompiler.CompileRule(rule, ruleParams, scopedParams);
        }


        // /// <summary>
        // /// This will execute the compiled rules 
        // /// </summary>
        // /// <param name="workflowName"></param>
        // /// <param name="ruleParams"></param>
        // /// <returns>list of rule result set</returns>
        // private List<RuleResultTree> ExecuteAllRuleByWorkflow(string workflowName, RuleParameter[] ruleParameters)
        // {
        //     _logger.LogTrace($"Compiled rules found for {workflowName} workflow and executed");
        //
        //     var result = new List<RuleResultTree>();
        //     var compiledRulesCacheKey = GetCompiledRulesKey(workflowName, ruleParameters);
        //     foreach (var compiledRule in _rulesCache.GetCompiledRules(compiledRulesCacheKey)?.Values)
        //     {
        //         var resultTree = compiledRule(ruleParameters);
        //         result.Add(resultTree);
        //     }
        //
        //     FormatErrorMessages(result);
        //     return result;
        // }

        private string GetCompiledRulesKey(string workflowName, RuleParameter[] ruleParams)
        {
            var key = $"{workflowName}-" + string.Join("-", ruleParams.Select(c => c.Type.Name));
            return key;
        }

        private IDictionary<string, Func<ActionBase>> GetDefaultActionRegistry()
        {
            return new Dictionary<string, Func<ActionBase>>{
                {"OutputExpression",() => new OutputExpressionAction(_ruleExpressionParser) },
                {"EvaluateRule", () => new EvaluateRuleAction(this,_ruleExpressionParser) }
            };
        }

        /// <summary>
        /// The result
        /// </summary>
        /// <param name="ruleResultList">The result.</param>
        /// <returns>Updated error message.</returns>
        private IEnumerable<RuleResultTree> FormatErrorMessages(IEnumerable<RuleResultTree> ruleResultList)
        {
            if (_reSettings.EnableFormattedErrorMessage)
            {
                foreach (var ruleResult in ruleResultList?.Where(r => !r.IsSuccess))
                {
                    var errorMessage = ruleResult?.Rule?.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(ruleResult.ExceptionMessage) && errorMessage != null)
                    {
                        var errorParameters = Regex.Matches(errorMessage, ParamParseRegex);

                        var inputs = ruleResult.Inputs;
                        foreach (var param in errorParameters)
                        {
                            var paramVal = param?.ToString();
                            var property = paramVal?.Substring(2, paramVal.Length - 3);
                            if (property?.Split('.')?.Count() > 1)
                            {
                                var typeName = property?.Split('.')?[0];
                                var propertyName = property?.Split('.')?[1];
                                errorMessage = UpdateErrorMessage(errorMessage, inputs, property, typeName, propertyName);
                            }
                            else
                            {
                                var arrParams = inputs?.Select(c => new { Name = c.Key, c.Value });
                                var model = arrParams?.Where(a => string.Equals(a.Name, property))?.FirstOrDefault();
                                var value = model?.Value != null ? JsonConvert.SerializeObject(model?.Value) : null;
                                errorMessage = errorMessage?.Replace($"$({property})", value ?? $"$({property})");
                            }
                        }
                        ruleResult.ExceptionMessage = errorMessage;
                    }

                }
            }
            return ruleResultList;
        }

        /// <summary>
        /// Updates the error message.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="evaluatedParams">The evaluated parameters.</param>
        /// <param name="property">The property.</param>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>Updated error message.</returns>
        private static string UpdateErrorMessage(string errorMessage, IDictionary<string, object> inputs, string property, string typeName, string propertyName)
        {
            var arrParams = inputs?.Select(c => new { Name = c.Key, c.Value });
            var model = arrParams?.Where(a => string.Equals(a.Name, typeName))?.FirstOrDefault();
            if (model != null)
            {
                var modelJson = JsonConvert.SerializeObject(model?.Value);
                var jObj = JObject.Parse(modelJson);
                JToken jToken = null;
                var val = jObj?.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out jToken);
                errorMessage = errorMessage.Replace($"$({property})", jToken != null ? jToken?.ToString() : $"({property})");
            }

            return errorMessage;
        }
        #endregion
    }
}
