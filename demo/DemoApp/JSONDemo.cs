// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using static RulesEngine.Extensions.ListofRuleResultTreeExtension;

namespace DemoApp
{
    public class JSONDemo
    {
        public void Run()
        {
            Console.WriteLine($"Running {nameof(JSONDemo)}....");

            var converter = new ExpandoObjectConverter();
            
            var inputs = new string[]
                {
                   
                };

            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "uF1.json", SearchOption.AllDirectories);
            if (files == null || files.Length == 0)
                throw new Exception("Rules not found.");

            var fileData = File.ReadAllText(files[0]);
            var workflow = JsonConvert.DeserializeObject<List<Workflow>>(fileData);

            var bre = new RulesEngine.RulesEngine(workflow.ToArray(), null);
            
            var input = new RuleParameter("mn",  "uF1HXRN0-32-P1000-0136");
            
            List<RuleResultTree> resultList = bre.ExecuteAllRulesAsync("ExtractFeatures", input).Result;

            foreach (var rule in resultList)
            {
                Console.WriteLine($"Rule {rule.Rule.RuleName} {rule.Rule.RuleExpressionType}: {rule.IsSuccess} {rule.ExceptionMessage}");

                if (rule.ChildResults == null) continue;
                
                foreach (var child in rule.ChildResults)
                {
                    Console.WriteLine($"\t\t {child.Rule.RuleName} {child.Rule.RuleExpressionType}: {child.IsSuccess} [{child.RegexCapture}] [{child.RegexMatched}] [{child.ExceptionMessage}]");
                }
            }
        }
    }
}
