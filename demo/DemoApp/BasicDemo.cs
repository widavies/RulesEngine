// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using static RulesEngine.Extensions.ListofRuleResultTreeExtension;

namespace DemoApp
{
    public class BasicDemo
    {
        public void Run()
        {
            Console.WriteLine($"Running {nameof(BasicDemo)}....");
            List<Workflow> workflows = new List<Workflow>();
            Workflow workflow = new Workflow();
            workflow.WorkflowName = "Test Workflow Rule 1";

            List<Rule> rules = new List<Rule>();

            Rule rule = new Rule();
            rule.RuleName = "TestRule";
            rule.SuccessEvent = "Count is within tolerance.";
            rule.ErrorMessage = "Over expected.";
//            rule.DefaultValue = "19";
            //rule.Value = "Something";
            rule.Operator = "ExclusiveOr";
            rule.Expression = " 1 == 1";
            
            //rule.Operator = "ExclusiveOr";
            
            rule.Rules = new [] {
                new Rule {
                    RuleName = "TestRule",
                    Operator = "ExclusiveOr",
                    Rules = new [] {
                        new Rule {
                            RuleName = "Grandchild",
                            Expression = "2 == 1",
                            Value = "61"
                        }
                    },
                    LocalParams = new[] {
                        new ScopedParam() {
                            Name = "TestRule",
                            Expression = "1==4"
                        }
                    }
                },
                new Rule {
                    RuleName = "TestRule",
                    Expression = "4 == 4",
                    Value = "13"
                },
            };
            
            rule.RuleExpressionType = RuleExpressionType.LambdaExpression;

            rules.Add(rule);

            Rule rule2 = new Rule();
            rule2.RuleName = "TestRule2";
            rule2.SuccessEvent = "Count is within tolerance.";
            rule2.ErrorMessage = "Over expected.";
            rule2.Expression = "TestRule == 61";
            rule2.Value = "4";

            rule2.RuleExpressionType = RuleExpressionType.LambdaExpression;

            rules.Add(rule2);
            
            workflow.Rules = rules;

            workflows.Add(workflow);

            var bre = new RulesEngine.RulesEngine(workflows.ToArray(), null);

            dynamic datas = new ExpandoObject();
            datas.count = 1;
            var inputs = new[]
              {
                    datas
              };

            List<RuleResultTree> resultList = bre.ExecuteAllRulesAsync("Test Workflow Rule 1", inputs).Result;

            foreach (var res in resultList)
            {
                Console.WriteLine($"{res.Rule.RuleName} {res.IsSuccess} {res.PromotedValue} {res.ExceptionMessage}");

                if (res.ChildResults == null) continue;
                
                
                foreach (var child in res.ChildResults)
                {
                    Console.WriteLine($"\t\t{child.Rule.RuleName} {res.Rule.Value} {child.IsSuccess}");
                }
            }
        }
    }
}
