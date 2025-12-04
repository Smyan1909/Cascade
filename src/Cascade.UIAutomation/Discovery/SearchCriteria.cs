using System.Drawing;
using System.Windows.Automation;

namespace Cascade.UIAutomation.Discovery;

public class SearchCriteria
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? NameContains { get; set; }
    public string? ClassName { get; set; }
    public ControlType? ControlType { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsOffscreen { get; set; }
    public Rectangle? BoundingRectangle { get; set; }

    private Func<AutomationElement, bool>? _predicateOverride;

    public static SearchCriteria ByAutomationId(string id) => new() { AutomationId = id };
    public static SearchCriteria ByName(string name) => new() { Name = name };
    public static SearchCriteria ByClassName(string className) => new() { ClassName = className };
    public static SearchCriteria ByControlType(ControlType type) => new() { ControlType = type };

    public SearchCriteria WithPredicate(Func<AutomationElement, bool> predicate)
    {
        _predicateOverride = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public SearchCriteria And(SearchCriteria other)
    {
        if (other is null) return this;
        return new SearchCriteria
        {
            AutomationId = other.AutomationId ?? AutomationId,
            Name = other.Name ?? Name,
            NameContains = other.NameContains ?? NameContains,
            ClassName = other.ClassName ?? ClassName,
            ControlType = other.ControlType ?? ControlType,
            IsEnabled = other.IsEnabled ?? IsEnabled,
            IsOffscreen = other.IsOffscreen ?? IsOffscreen,
            BoundingRectangle = other.BoundingRectangle ?? BoundingRectangle,
            _predicateOverride = element => Match(element) && other.Match(element)
        };
    }

    public SearchCriteria Or(SearchCriteria other)
    {
        if (other is null) return this;
        return new SearchCriteria
        {
            _predicateOverride = element => Match(element) || other.Match(element)
        };
    }

    public SearchCriteria Not()
    {
        return new SearchCriteria
        {
            _predicateOverride = element => !Match(element)
        };
    }

    public bool Match(AutomationElement element)
    {
        if (element is null)
        {
            return false;
        }

        if (_predicateOverride is not null)
        {
            return _predicateOverride(element);
        }

        if (!string.IsNullOrWhiteSpace(AutomationId))
        {
            var value = element.Current.AutomationId;
            if (!string.Equals(value, AutomationId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(Name))
        {
            if (!string.Equals(element.Current.Name, Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(NameContains))
        {
            if (element.Current.Name?.Contains(NameContains, StringComparison.OrdinalIgnoreCase) != true)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(ClassName))
        {
            if (!string.Equals(element.Current.ClassName, ClassName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (ControlType is not null && element.Current.ControlType != ControlType)
        {
            return false;
        }

        if (IsEnabled is not null && element.Current.IsEnabled != IsEnabled)
        {
            return false;
        }

        if (IsOffscreen is not null && element.Current.IsOffscreen != IsOffscreen)
        {
            return false;
        }

        if (BoundingRectangle is not null && !BoundingRectangle.Value.Contains(ToRectangle(element.Current.BoundingRectangle)))
        {
            return false;
        }

        return true;
    }

    public Condition ToAutomationCondition()
    {
        var conditions = new List<Condition>();

        if (!string.IsNullOrWhiteSpace(AutomationId))
        {
            conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, AutomationId));
        }

        if (!string.IsNullOrWhiteSpace(Name))
        {
            conditions.Add(new PropertyCondition(AutomationElement.NameProperty, Name));
        }

        if (!string.IsNullOrWhiteSpace(ClassName))
        {
            conditions.Add(new PropertyCondition(AutomationElement.ClassNameProperty, ClassName));
        }

        if (ControlType is not null)
        {
            conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType));
        }

        if (IsEnabled is not null)
        {
            conditions.Add(new PropertyCondition(AutomationElement.IsEnabledProperty, IsEnabled));
        }

        if (IsOffscreen is not null)
        {
            conditions.Add(new PropertyCondition(AutomationElement.IsOffscreenProperty, IsOffscreen));
        }

        if (conditions.Count == 0)
        {
            return Condition.TrueCondition;
        }

        if (conditions.Count == 1)
        {
            return conditions[0];
        }

        return new AndCondition(conditions.ToArray());
    }

    private static Rectangle ToRectangle(System.Windows.Rect rect)
    {
        return Rectangle.FromLTRB(
            (int)rect.Left,
            (int)rect.Top,
            (int)rect.Right,
            (int)rect.Bottom);
    }
}


