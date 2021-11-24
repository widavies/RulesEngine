// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentValidation;
using RulesEngine.HelperFunctions;
using RulesEngine.Models;
using System.Collections.Generic;
using System.Linq;

namespace RulesEngine.Validators
{
    internal class WorkflowsValidator : AbstractValidator<Workflow>
    {
        public WorkflowsValidator()
        {
            RuleFor(c => c.WorkflowName).NotEmpty().WithMessage(Constants.WORKFLOW_NAME_NULL_ERRMSG);
            When(c => c.Rules?.Any() != true, () => {
                RuleFor(c => c.WorkflowsToInject).NotEmpty().WithMessage(Constants.INJECT_WORKFLOW_RULES_ERRMSG);
            }).Otherwise(() => {
                var ruleValidator = new RuleValidator();
                RuleForEach(c => c.Rules).SetValidator(ruleValidator);
            });

            // Take the set of all:
            // -Rule.RuleName
            // -Rule.LocalParams.Name
            // -GlobalParams.Name
            // There may be no duplicates.
            RuleFor(c => c).Must(workflow =>
                CheckForDuplicateNames(workflow) == null
            ).WithMessage(workflow =>
                $"The name {CheckForDuplicateNames(workflow)} is duplicated. Within a workflow, the set which contains the names of" +
                " all global parameters, all (including nested) local parameters, and all (including nested)" +
                " rule names must contain NO duplicates."
            );
        }

        private string CheckForDuplicateNames(Workflow workflow)
        {
            var names = new List<string>();

            names.AddRange(workflow.GlobalParams?.Select(param => param.Name) ?? new string[] { });

            foreach (var rule in workflow.Rules)
            {
                var exploring = new Queue<Rule>();
                exploring.Enqueue(rule);

                while (exploring.Count != 0)
                {
                    var node = exploring.Dequeue();

                    if (node.Rules != null)
                    {
                        foreach (var child in node.Rules)
                        {
                            exploring.Enqueue(child);
                        }
                    }

                    names.Add(node.RuleName);
                    names.AddRange(node.LocalParams?.Select(x => x.Name) ?? new string[] { });
                }
            }

            return names.GroupBy(x => x).FirstOrDefault(group => group.Count() > 1)?.Key;
        }
    }
}