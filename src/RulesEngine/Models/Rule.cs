﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RulesEngine.Models
{
    /// <summary>
    /// Rule class
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Rule
    {
        /// <summary>
        /// Rule name for the Rule
        /// </summary>
        public string RuleName { get; set; } = $"__DEFAULT__-{Guid.NewGuid().ToString()}";
        /// <summary>	
        /// Gets or sets the custom property or tags of the rule.	
        /// </summary>	
        /// <value>	
        /// The properties of the rule.	
        /// </value>	
        public Dictionary<string, object> Properties { get; set; }
        public string Operator { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets whether the rule is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        [Obsolete("will be removed in next major version")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ErrorType ErrorType { get; set; } = ErrorType.Warning;

        [JsonConverter(typeof(StringEnumConverter))]
        public RuleExpressionType RuleExpressionType { get; set; } = RuleExpressionType.LambdaExpression;

        [Obsolete("WorkflowRulesToInject is deprecated. Use WorkflowsToInject instead.")]
        public IEnumerable<string> WorkflowRulesToInject {
          get { return WorkflowsToInject; }
          set { WorkflowsToInject = value; }
        }
        public IEnumerable<string> WorkflowsToInject { get; set; }
        
        public IEnumerable<Rule> Rules { get; set; }
        public IEnumerable<ScopedParam> LocalParams { get; set; }
        public string Expression { get; set; }

        public string RawExpression => Expression ?? (Requires != null ? "true" : null);

        public RuleActions Actions { get; set; }
        public string SuccessEvent { get; set; }

        public string Default { get; set; } = null;
        
        /// <summary>
        /// Applies when <see cref="RuleExpressionType"/> is <c>RuleExpressionType.RegexExpression</c>
        /// or <c>RuleExpressionType.RegexCaptureExpression</c>
        /// </summary>
        public bool CaseSensitiveRegex = false;
        
        public bool Required = true;
        
        /// <summary>
        /// Set when <see cref="RuleExpressionType"/> is NOT <c>RuleExpressionType.CaptureExpression</c>
        /// </summary>
        public string Description;
        /// <summary>
        /// Set when <see cref="RuleExpressionType"/> is <c>RuleExpressionType.CaptureExpression</c>
        /// </summary>
        public Dictionary<string, string> Descriptions;

        public Dictionary<string, string> Requires;

        public Dictionary<string, Dictionary<string, string>> EachRequires;


    }
}
