using BKeeper.Application.Notifications;
using Xunit;

namespace BKeeper.Tests.Unit.Notifications;

public class TemplateCatalogTests
{
    private static readonly Dictionary<string, string> Vars = new()
    {
        ["first_name"] = "Ana",
        ["box_name"] = "CrossFit Lisboa",
        ["usual_class"] = "6pm WOD",
        ["coach"] = "Coach Rui",
    };

    [Fact]
    public void Render_SubstitutesVariables_InPortuguese()
    {
        var ok = TemplateCatalog.TryRender(TemplateCatalog.MissYouSoft, "pt-PT", Vars, out var body);
        Assert.True(ok);
        Assert.Contains("Ana", body);
        Assert.Contains("CrossFit Lisboa", body);
        Assert.DoesNotContain("{first_name}", body);
    }

    [Fact]
    public void Render_FallsBackToEnglish_ForNonPtLanguage()
    {
        var ok = TemplateCatalog.TryRender(TemplateCatalog.MissYouSoft, "en", Vars, out var body);
        Assert.True(ok);
        Assert.Contains("we miss you", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_UnknownKey_ReturnsFalse()
    {
        var ok = TemplateCatalog.TryRender("NOT_A_REAL_KEY", "en", Vars, out var body);
        Assert.False(ok);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public void Render_MissingVariable_LeavesPlaceholderLiteral()
    {
        var ok = TemplateCatalog.TryRender(TemplateCatalog.EvalRequest, "en", new Dictionary<string, string> { ["first_name"] = "Ana" }, out var body);
        Assert.True(ok);
        Assert.Contains("{form_link}", body); // not substituted, since it wasn't provided
    }
}
