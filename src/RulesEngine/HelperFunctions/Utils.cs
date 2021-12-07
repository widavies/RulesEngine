// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Xsl;

namespace RulesEngine.HelperFunctions
{
    public static class Utils
    {
        public static object GetTypedObject(dynamic input)
        {
            if (input is ExpandoObject)
            {
                Type type = CreateAbstractClassType(input);
                return CreateObject(type, input);
            }
            else
            {
                return input;
            }
        }

        public static Type CreateAbstractClassType(dynamic input)
        {
            List<DynamicProperty> props = new List<DynamicProperty>();

            if (input == null)
            {
                return typeof(object);
            }

            if (!(input is ExpandoObject))
            {
                return input.GetType();
            }

            else
            {
                foreach (var expando in (IDictionary<string, object>)input)
                {
                    Type value;
                    if (expando.Value is IList)
                    {
                        if (((IList)expando.Value).Count == 0)
                            value = typeof(List<object>);
                        else
                        {
                            var internalType = CreateAbstractClassType(((IList)expando.Value)[0]);
                            value = new List<object>().Cast(internalType).ToList(internalType).GetType();
                        }
                    }
                    else
                    {
                        value = CreateAbstractClassType(expando.Value);
                    }

                    props.Add(new DynamicProperty(expando.Key, value));
                }
            }

            var type = DynamicClassFactory.CreateType(props);
            return type;
        }

        public static object CreateObject(Type type, dynamic input)
        {
            if (!(input is ExpandoObject))
            {
                return Convert.ChangeType(input, type);
            }

            object obj = Activator.CreateInstance(type);

            var typeProps = type.GetProperties().ToDictionary(c => c.Name);

            foreach (var expando in (IDictionary<string, object>)input)
            {
                if (typeProps.ContainsKey(expando.Key) &&
                    expando.Value != null &&
                    (expando.Value.GetType().Name != "DBNull" || expando.Value != DBNull.Value))
                {
                    object val;
                    var propInfo = typeProps[expando.Key];
                    if (expando.Value is ExpandoObject)
                    {
                        var propType = propInfo.PropertyType;
                        val = CreateObject(propType, expando.Value);
                    }
                    else if (expando.Value is IList)
                    {
                        var internalType = propInfo.PropertyType.GenericTypeArguments.FirstOrDefault() ??
                                           typeof(object);
                        var temp = (IList)expando.Value;
                        var newList = new List<object>().Cast(internalType).ToList(internalType);
                        for (int i = 0; i < temp.Count; i++)
                        {
                            var child = CreateObject(internalType, temp[i]);
                            newList.Add(child);
                        }

                        ;
                        val = newList;
                    }
                    else
                    {
                        val = expando.Value;
                    }

                    propInfo.SetValue(obj, val, null);
                }
            }

            return obj;
        }

        private static IEnumerable Cast(this IEnumerable self, Type innerType)
        {
            var methodInfo = typeof(Enumerable).GetMethod("Cast");
            var genericMethod = methodInfo.MakeGenericMethod(innerType);
            return genericMethod.Invoke(null, new[] {self}) as IEnumerable;
        }

        private static IList ToList(this IEnumerable self, Type innerType)
        {
            var methodInfo = typeof(Enumerable).GetMethod("ToList");
            var genericMethod = methodInfo.MakeGenericMethod(innerType);
            return genericMethod.Invoke(null, new[] {self}) as IList;
        }

        private static string PatternUnescaped(string str)
        {
            return $@"(?<!\\)(?:\\\\)*{str}";
        }

        private static string PatternEscaped(string str)
        {
            return $@"(?<!\\)\\(?:\\\\)*{str}";
        }

        private static string PatternEscaped(string left, string center, string right)
        {
            return $"{PatternEscaped(left)}{center}{PatternEscaped(right)}";
        }

        private static string PatternUnescaped(string left, string center, string right)
        {
            return $"{PatternUnescaped(left)}{center}{PatternUnescaped(right)}";
        }

        private static string ParseStringConstant(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return "";
            }

            str = Regex.Replace(str, PatternEscaped("<"), "<");
            str = Regex.Replace(str, PatternEscaped(">"), ">");
            str = Regex.Replace(str, PatternEscaped("%"), "%");
            
            return $"\"{str}\"";
        }

        private static string ExpandReferenceRecursively(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return "";
            }
            
            var patternRefRegex = PatternUnescaped("<", @"\w+", ">");
            var patternRefName = PatternUnescaped("%", @"\w+", "%");

            var match = Regex.Match(expression, patternRefRegex);

            if (match.Success)
            {
                var rest = expression.Substring(match.Index + match.Length);
                
                return ParseStringConstant(expression.Substring(0, match.Index)) +
                       Regex.Replace(match.Value, $"({PatternUnescaped("<")}|{PatternUnescaped(">")})", "")
                       + ".MatchedRegex" + (rest.Length > 0 ? "," : "")
                       + ExpandReferenceRecursively(expression.Substring(match.Index + match.Length));
            }

            match = Regex.Match(expression, patternRefName);

            if (match.Success)
            {
                var rest = expression.Substring(match.Index + match.Length);
                
                return ParseStringConstant(expression.Substring(0, match.Index)) +
                       Regex.Replace(match.Value, $"({PatternUnescaped("%")}|{PatternUnescaped("%")})", "")
                       + ".Name" + (rest.Length > 0 ? "," : "")
                       + ExpandReferenceRecursively(rest);
            }
            
            return ParseStringConstant(expression);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression">A regex string that may include refs to other rules.</param>
        /// <returns>Expands the expression into an actual expression (not just a regex string)</returns>
        public static string ExpandReferences(string expression)
        {
            // Last bit here assumes there are always at least two arguments to the string concat
            return string.IsNullOrEmpty(expression) ? expression : $"string.Concat({ExpandReferenceRecursively(expression)},\"\")";
        }
    }
}