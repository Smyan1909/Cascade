using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cascade.UIAutomation.Elements;

namespace Cascade.UIAutomation.Discovery;

public sealed class ElementLocator
{
    private static readonly Regex EqualsFilterRegex = new(@"^@(?<name>[A-Za-z]+)\s*=\s*'(?<value>[^']*)'$", RegexOptions.Compiled);
    private static readonly Regex ContainsFilterRegex = new(@"^contains\(\s*@(?<name>[A-Za-z]+)\s*,\s*'(?<value>[^']*)'\s*\)$", RegexOptions.Compiled);

    private readonly IReadOnlyList<LocatorSegment> _segments;
    private readonly bool _matchAnywhere;

    private ElementLocator(IReadOnlyList<LocatorSegment> segments, bool matchAnywhere)
    {
        _segments = segments;
        _matchAnywhere = matchAnywhere;
    }

    public static ElementLocator Parse(string locator)
    {
        if (string.IsNullOrWhiteSpace(locator))
        {
            throw new ArgumentException("Locator cannot be empty.", nameof(locator));
        }

        var trimmed = locator.Trim();
        var matchAnywhere = trimmed.StartsWith("//", StringComparison.Ordinal);
        var normalized = matchAnywhere ? trimmed[2..] : trimmed;
        normalized = normalized.TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseSegment)
            .ToList();

        if (segments.Count == 0)
        {
            throw new ArgumentException("Locator must contain at least one segment.", nameof(locator));
        }

        return new ElementLocator(segments, matchAnywhere);
    }

    public IUIElement? Find(IUIElement root) => FindAll(root).FirstOrDefault();

    public IReadOnlyList<IUIElement> FindAll(IUIElement root)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));

        IEnumerable<IUIElement> candidates = _matchAnywhere ? Traverse(root) : new[] { root };

        for (var i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            var matches = candidates
                .Where(segment.Matches)
                .ApplyIndex(segment.Index)
                .ToList();

            if (matches.Count == 0)
            {
                return Array.Empty<IUIElement>();
            }

            if (i == _segments.Count - 1)
            {
                return matches;
            }

            candidates = matches.SelectMany(m => m.Children);
        }

        return Array.Empty<IUIElement>();
    }

    private static LocatorSegment ParseSegment(string segmentText)
    {
        if (string.IsNullOrWhiteSpace(segmentText))
        {
            throw new ArgumentException("Locator segment cannot be empty.", nameof(segmentText));
        }

        var trimmed = segmentText.Trim();
        var controlTypePart = new string(trimmed.TakeWhile(c => c != '[').ToArray());
        if (string.IsNullOrWhiteSpace(controlTypePart))
        {
            throw new ArgumentException($"Locator segment '{segmentText}' is missing a control type.", nameof(segmentText));
        }

        var tokens = ExtractTokens(trimmed[controlTypePart.Length..]);
        int? index = null;
        var filters = new List<LocatorFilter>();

        foreach (var token in tokens)
        {
            if (int.TryParse(token, out var idx))
            {
                index = idx;
                continue;
            }

            var filter = ParseFilterToken(token);
            if (filter is null)
            {
                throw new ArgumentException($"Invalid locator filter '{token}'.", nameof(segmentText));
            }

            filters.Add(filter);
        }

        return new LocatorSegment(controlTypePart, filters, index);
    }

    private static IEnumerable<string> ExtractTokens(string input)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(input))
        {
            return tokens;
        }

        var depth = 0;
        var current = new StringBuilder();

        foreach (var ch in input)
        {
            if (ch == '[')
            {
                if (depth++ == 0)
                {
                    continue;
                }
            }
            else if (ch == ']')
            {
                if (--depth == 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    continue;
                }
            }

            if (depth > 0)
            {
                current.Append(ch);
            }
        }

        return tokens;
    }

    private static LocatorFilter? ParseFilterToken(string token)
    {
        var equals = EqualsFilterRegex.Match(token);
        if (equals.Success)
        {
            return new LocatorFilter(equals.Groups["name"].Value, equals.Groups["value"].Value, LocatorFilterOperator.Equals);
        }

        var contains = ContainsFilterRegex.Match(token);
        if (contains.Success)
        {
            return new LocatorFilter(contains.Groups["name"].Value, contains.Groups["value"].Value, LocatorFilterOperator.Contains);
        }

        return null;
    }

    private static IEnumerable<IUIElement> Traverse(IUIElement root)
    {
        var queue = new Queue<IUIElement>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;
            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    private sealed record LocatorSegment(string ControlType, IReadOnlyList<LocatorFilter> Filters, int? Index)
    {
        public bool Matches(IUIElement element)
        {
            if (element is null)
            {
                return false;
            }

            if (!string.Equals(ControlType, "*", StringComparison.OrdinalIgnoreCase))
            {
                var elementType = element.ControlType?.ProgrammaticName?.Replace("ControlType.", string.Empty) ?? string.Empty;
                if (!string.Equals(elementType, ControlType, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return Filters.All(filter => filter.Matches(element));
        }
    }

    private sealed record LocatorFilter(string Property, string Value, LocatorFilterOperator Operator)
    {
        public bool Matches(IUIElement element)
        {
            var propertyValue = Property switch
            {
                "AutomationId" => element.AutomationId,
                "Name" => element.Name,
                "ClassName" => element.ClassName,
                _ => null
            };

            if (propertyValue is null)
            {
                return false;
            }

            return Operator switch
            {
                LocatorFilterOperator.Equals => string.Equals(propertyValue, Value, StringComparison.OrdinalIgnoreCase),
                LocatorFilterOperator.Contains => propertyValue.Contains(Value, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }

    private enum LocatorFilterOperator
    {
        Equals,
        Contains
    }
}

internal static class ElementLocatorEnumerableExtensions
{
    public static IEnumerable<IUIElement> ApplyIndex(this IEnumerable<IUIElement> source, int? index)
    {
        if (index is null)
        {
            return source;
        }

        var targetIndex = Math.Max(0, index.Value - 1);
        return source.Skip(targetIndex).Take(1);
    }
}


