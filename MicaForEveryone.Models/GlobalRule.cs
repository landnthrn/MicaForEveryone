using CommunityToolkit.Mvvm.ComponentModel;
using TerraFX.Interop.Windows;

namespace MicaForEveryone.Models;

public sealed partial class GlobalRule : Rule
{
    [ObservableProperty]
    public partial string? ExcludedClassNames { get; set; }

    public override int Priority => 0;

    public override bool Equals(Rule? other)
    {
        return other is not null
            && other is GlobalRule globalRule
            && string.Equals(ExcludedClassNames, globalRule.ExcludedClassNames, StringComparison.CurrentCultureIgnoreCase)
            && base.Equals(other);
    }

    public override bool IsRuleApplicable(HWND hWnd)
    {
        return !IsWindowClassExcluded(hWnd);
    }

    public bool IsWindowClassExcluded(HWND hWnd)
    {
        return RuleWindowClassMatcher.IsWindowClassListed(hWnd, ExcludedClassNames);
    }
}
