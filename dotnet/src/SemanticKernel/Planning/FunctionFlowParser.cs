// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning;

/// <summary>
/// Parse XML plans created by the Function Flow semantic function.
/// </summary>
internal static class FunctionFlowParser
{
    /// <summary>
    /// The tag name used in the plan xml for the user's goal/ask.
    /// </summary>
    internal const string GoalTag = "goal";

    /// <summary>
    /// The tag name used in the plan xml for the solution.
    /// </summary>
    internal const string SolutionTag = "plan";

    /// <summary>
    /// The tag name used in the plan xml for a step that calls a skill function.
    /// </summary>
    internal const string FunctionTag = "function.";

    /// <summary>
    /// The attribute tag used in the plan xml for setting the context variable name to set the output of a function to.
    /// </summary>
    internal const string SetContextVariableTag = "setContextVariable";

    /// <summary>
    /// The attribute tag used in the plan xml for appending the output of a function to the final result for a plan.
    /// </summary>
    internal const string AppendToResultTag = "appendToResult";

    internal static Plan ToPlanFromXml(this string xmlString, SKContext context)
    {
        try
        {
            XmlDocument xmlDoc = new();
            try
            {
                xmlDoc.LoadXml("<xml>" + xmlString + "</xml>");
            }
            catch (XmlException e)
            {
                throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, "Failed to parse plan xml.", e);
            }

            // Get the Goal
            var (goalTxt, goalXmlString) = GatherGoal(xmlDoc);

            // Get the Solution
            XmlNodeList solution = xmlDoc.GetElementsByTagName(SolutionTag);

            var plan = new Plan(goalTxt);

            // loop through solution node and add to Steps
            foreach (XmlNode o in solution)
            {
                var parentNodeName = o.Name;

                foreach (XmlNode o2 in o.ChildNodes)
                {
                    if (o2.Name == "#text")
                    {
                        if (o2.Value != null)
                        {
                            plan.Steps.Add(new Plan(o2.Value.Trim())); // todo debug this
                        }

                        continue;
                    }

                    if (o2.Name.StartsWith(FunctionTag, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var skillFunctionName = o2.Name.Split(FunctionTag)?[1] ?? string.Empty;
                        GetSkillFunctionNames(skillFunctionName, out var skillName, out var functionName);

                        // TODO I think we can remove this.
                        if (!string.IsNullOrEmpty(functionName) && context.IsFunctionRegistered(skillName, functionName, out var skillFunction))
                        {
                            Verify.NotNull(functionName, nameof(functionName));
                            Verify.NotNull(skillFunction, nameof(skillFunction));

                            var planStep = new Plan(skillFunction);

                            var functionVariables = new ContextVariables();

                            var view = skillFunction.Describe();
                            foreach (var p in view.Parameters)
                            {
                                functionVariables.Set(p.Name, p.DefaultValue);
                            }

                            var variableTargetName = string.Empty;
                            var appendToResultName = string.Empty;
                            if (o2.Attributes is not null)
                            {
                                foreach (XmlAttribute attr in o2.Attributes)
                                {
                                    context.Log.LogTrace("{0}: processing attribute {1}", parentNodeName, attr.ToString());
                                    if (attr.InnerText.StartsWith("$", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        functionVariables.Set(attr.Name, attr.InnerText);
                                    }
                                    else if (attr.Name.Equals(SetContextVariableTag, StringComparison.OrdinalIgnoreCase))
                                    {
                                        variableTargetName = attr.InnerText;
                                    }
                                    else if (attr.Name.Equals(AppendToResultTag, StringComparison.OrdinalIgnoreCase))
                                    {
                                        appendToResultName = attr.InnerText;
                                    }
                                    else
                                    {
                                        functionVariables.Set(attr.Name, attr.InnerText);
                                    }
                                }
                            }

                            // Plan properties
                            // planStep.OutputKey = variableTargetName;
                            // planStep.ResultKey = appendToResultName;
                            planStep.NamedParameters = functionVariables;
                            plan.Steps.Add(planStep);

                            if (!string.IsNullOrEmpty(variableTargetName))
                            {
                                context.Variables.Set(variableTargetName,
                                    "<NOT_YET_SET>"); // TODO - this is a hack to make sure the variable is set (even if it is empty)
                            }
                        }
                        else
                        {
                            context.Log.LogTrace("{0}: appending function node {1}", parentNodeName, skillFunctionName);
                            plan.Steps.Add(new Plan(o2.InnerText)); // TODO DEBUG THIS
                        }

                        continue;
                    }

                    plan.Steps.Add(new Plan(o2.InnerText)); // TODO DEBUG THIS
                }
            }

            return plan;
        }
        catch (Exception e) when (!e.IsCriticalException())
        {
            context.Log.LogError(e, "Plan parsing failed: {0}", e.Message);
            throw;
        }
    }

    private static (string goalTxt, string goalXmlString) GatherGoal(XmlDocument xmlDoc)
    {
        XmlNodeList goal = xmlDoc.GetElementsByTagName(GoalTag);
        if (goal.Count == 0)
        {
            throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, "No goal found.");
        }

        string goalTxt = goal[0]!.FirstChild!.Value ?? string.Empty;
        var goalContent = new StringBuilder();
        _ = goalContent.Append($"<{GoalTag}>")
            .Append(goalTxt)
            .AppendLine($"</{GoalTag}>");
        return (goalTxt.Trim(), goalContent.Replace("\r\n", "\n").ToString().Trim());
    }

    private static void GetSkillFunctionNames(string skillFunctionName, out string skillName, out string functionName)
    {
        var skillFunctionNameParts = skillFunctionName.Split(".");
        skillName = skillFunctionNameParts?.Length > 0 ? skillFunctionNameParts[0] : string.Empty;
        functionName = skillFunctionNameParts?.Length > 1 ? skillFunctionNameParts[1] : skillFunctionName;
    }
}
