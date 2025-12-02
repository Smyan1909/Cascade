using System.Text.RegularExpressions;
using Cascade.UIAutomation.Elements;
using Cascade.UIAutomation.Enums;

namespace Cascade.UIAutomation.Discovery;

/// <summary>
/// Provides XPath-like element location using a simple locator syntax.
/// </summary>
/// <remarks>
/// Supports syntax like:
/// - /Window[@Name='Calculator']/Button[@AutomationId='num1Button']
/// - //Button[contains(@Name, 'Submit')]
/// - /Window/Pane/Edit[@ClassName='TextBox'][1]
/// </remarks>
public class ElementLocator
{
    private readonly List<LocatorStep> _steps;

    private ElementLocator(List<LocatorStep> steps)
    {
        _steps = steps;
    }

    /// <summary>
    /// Parses a locator string into an ElementLocator.
    /// </summary>
    /// <param name="locator">The locator string.</param>
    /// <returns>An ElementLocator instance.</returns>
    public static ElementLocator Parse(string locator)
    {
        if (string.IsNullOrWhiteSpace(locator))
            throw new ArgumentException("Locator string cannot be null or empty", nameof(locator));

        var steps = new List<LocatorStep>();
        var remaining = locator.Trim();

        while (!string.IsNullOrEmpty(remaining))
        {
            var step = ParseStep(ref remaining);
            steps.Add(step);
        }

        return new ElementLocator(steps);
    }

    /// <summary>
    /// Finds the first element matching the locator.
    /// </summary>
    /// <param name="root">The root element to start the search from.</param>
    /// <returns>The matching element, or null if not found.</returns>
    public IUIElement? Find(IUIElement root)
    {
        var candidates = new List<IUIElement> { root };

        foreach (var step in _steps)
        {
            var newCandidates = new List<IUIElement>();

            foreach (var candidate in candidates)
            {
                var matches = ApplyStep(candidate, step);
                newCandidates.AddRange(matches);
            }

            if (newCandidates.Count == 0)
                return null;

            candidates = newCandidates;
        }

        return candidates.FirstOrDefault();
    }

    /// <summary>
    /// Finds all elements matching the locator.
    /// </summary>
    /// <param name="root">The root element to start the search from.</param>
    /// <returns>A list of matching elements.</returns>
    public IReadOnlyList<IUIElement> FindAll(IUIElement root)
    {
        var candidates = new List<IUIElement> { root };

        foreach (var step in _steps)
        {
            var newCandidates = new List<IUIElement>();

            foreach (var candidate in candidates)
            {
                var matches = ApplyStep(candidate, step);
                newCandidates.AddRange(matches);
            }

            if (newCandidates.Count == 0)
                return Array.Empty<IUIElement>();

            candidates = newCandidates;
        }

        return candidates;
    }

    private static LocatorStep ParseStep(ref string remaining)
    {
        var step = new LocatorStep();

        // Check for descendant search (//)
        if (remaining.StartsWith("//"))
        {
            step.SearchDescendants = true;
            remaining = remaining.Substring(2);
        }
        else if (remaining.StartsWith("/"))
        {
            step.SearchDescendants = false;
            remaining = remaining.Substring(1);
        }
        else if (remaining.Length > 0 && !remaining.StartsWith("/"))
        {
            // Continue with current position
            step.SearchDescendants = false;
        }
        else
        {
            throw new FormatException($"Invalid locator syntax at: {remaining}");
        }

        // Parse control type (e.g., Button, Window, Edit)
        var controlTypeMatch = Regex.Match(remaining, @"^([A-Za-z]+)");
        if (controlTypeMatch.Success)
        {
            var controlTypeName = controlTypeMatch.Groups[1].Value;
            if (Enum.TryParse<ControlType>(controlTypeName, true, out var controlType))
            {
                step.ControlType = controlType;
            }
            else if (controlTypeName != "*")
            {
                step.ControlType = null; // Unknown type, will match any
            }
            remaining = remaining.Substring(controlTypeMatch.Length);
        }

        // Parse predicates (e.g., [@Name='Calculator'][1])
        while (remaining.StartsWith("["))
        {
            var predicateEnd = FindMatchingBracket(remaining);
            if (predicateEnd < 0)
                throw new FormatException("Unmatched bracket in locator");

            var predicate = remaining.Substring(1, predicateEnd - 1);
            ParsePredicate(step, predicate);
            remaining = remaining.Substring(predicateEnd + 1);
        }

        return step;
    }

    private static void ParsePredicate(LocatorStep step, string predicate)
    {
        predicate = predicate.Trim();

        // Check for numeric index (e.g., [1])
        if (int.TryParse(predicate, out var index))
        {
            step.Index = index;
            return;
        }

        // Check for contains function (e.g., contains(@Name, 'Submit'))
        var containsMatch = Regex.Match(predicate, @"contains\s*\(\s*@(\w+)\s*,\s*'([^']*)'\s*\)");
        if (containsMatch.Success)
        {
            var attrName = containsMatch.Groups[1].Value;
            var value = containsMatch.Groups[2].Value;
            step.Predicates.Add(new LocatorPredicate(attrName, value, PredicateOperator.Contains));
            return;
        }

        // Check for starts-with function
        var startsWithMatch = Regex.Match(predicate, @"starts-with\s*\(\s*@(\w+)\s*,\s*'([^']*)'\s*\)");
        if (startsWithMatch.Success)
        {
            var attrName = startsWithMatch.Groups[1].Value;
            var value = startsWithMatch.Groups[2].Value;
            step.Predicates.Add(new LocatorPredicate(attrName, value, PredicateOperator.StartsWith));
            return;
        }

        // Check for attribute equals (e.g., @Name='Calculator')
        var attrMatch = Regex.Match(predicate, @"@(\w+)\s*=\s*'([^']*)'");
        if (attrMatch.Success)
        {
            var attrName = attrMatch.Groups[1].Value;
            var value = attrMatch.Groups[2].Value;
            step.Predicates.Add(new LocatorPredicate(attrName, value, PredicateOperator.Equals));
            return;
        }

        throw new FormatException($"Invalid predicate: {predicate}");
    }

    private static int FindMatchingBracket(string str)
    {
        if (!str.StartsWith("["))
            return -1;

        int depth = 0;
        bool inString = false;

        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];

            if (c == '\'' && (i == 0 || str[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (!inString)
            {
                if (c == '[')
                    depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
        }

        return -1;
    }

    private static IEnumerable<IUIElement> ApplyStep(IUIElement element, LocatorStep step)
    {
        IEnumerable<IUIElement> candidates;

        if (step.SearchDescendants)
        {
            // Search all descendants
            candidates = GetAllDescendants(element);
        }
        else
        {
            // Search only children
            candidates = element.Children;
        }

        // Filter by control type
        if (step.ControlType.HasValue)
        {
            candidates = candidates.Where(e => e.ControlType == step.ControlType.Value);
        }

        // Filter by predicates
        foreach (var predicate in step.Predicates)
        {
            candidates = candidates.Where(e => MatchesPredicate(e, predicate));
        }

        // Apply index if specified
        if (step.Index.HasValue)
        {
            var indexed = candidates.ElementAtOrDefault(step.Index.Value - 1); // 1-based index
            if (indexed != null)
                return new[] { indexed };
            return Enumerable.Empty<IUIElement>();
        }

        return candidates;
    }

    private static IEnumerable<IUIElement> GetAllDescendants(IUIElement element)
    {
        foreach (var child in element.Children)
        {
            yield return child;
            foreach (var descendant in GetAllDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool MatchesPredicate(IUIElement element, LocatorPredicate predicate)
    {
        var value = GetAttributeValue(element, predicate.AttributeName);
        if (value == null)
            return false;

        return predicate.Operator switch
        {
            PredicateOperator.Equals => string.Equals(value, predicate.Value, StringComparison.OrdinalIgnoreCase),
            PredicateOperator.Contains => value.Contains(predicate.Value, StringComparison.OrdinalIgnoreCase),
            PredicateOperator.StartsWith => value.StartsWith(predicate.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string? GetAttributeValue(IUIElement element, string attributeName)
    {
        return attributeName.ToLowerInvariant() switch
        {
            "name" => element.Name,
            "automationid" => element.AutomationId,
            "classname" => element.ClassName,
            "controltype" => element.ControlType.ToString(),
            "runtimeid" => element.RuntimeId,
            "isenabled" => element.IsEnabled.ToString(),
            "isoffscreen" => element.IsOffscreen.ToString(),
            "hasKeyboardfocus" => element.HasKeyboardFocus.ToString(),
            _ => null
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Join("", _steps.Select(s => s.ToString()));
    }

    private class LocatorStep
    {
        public bool SearchDescendants { get; set; }
        public ControlType? ControlType { get; set; }
        public List<LocatorPredicate> Predicates { get; } = new();
        public int? Index { get; set; }

        public override string ToString()
        {
            var prefix = SearchDescendants ? "//" : "/";
            var typeName = ControlType?.ToString() ?? "*";
            var predicates = string.Join("", Predicates.Select(p => $"[{p}]"));
            var index = Index.HasValue ? $"[{Index}]" : "";
            return $"{prefix}{typeName}{predicates}{index}";
        }
    }

    private class LocatorPredicate
    {
        public string AttributeName { get; }
        public string Value { get; }
        public PredicateOperator Operator { get; }

        public LocatorPredicate(string attributeName, string value, PredicateOperator op)
        {
            AttributeName = attributeName;
            Value = value;
            Operator = op;
        }

        public override string ToString()
        {
            return Operator switch
            {
                PredicateOperator.Equals => $"@{AttributeName}='{Value}'",
                PredicateOperator.Contains => $"contains(@{AttributeName}, '{Value}')",
                PredicateOperator.StartsWith => $"starts-with(@{AttributeName}, '{Value}')",
                _ => $"@{AttributeName}='{Value}'"
            };
        }
    }

    private enum PredicateOperator
    {
        Equals,
        Contains,
        StartsWith
    }
}

