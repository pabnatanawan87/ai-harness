using AiHarness.Context;
using AiHarness.Rendering;

using Xunit;

namespace AiHarness.Tests;

public sealed class PromptRendererTests
{
    [Fact]
    public void Substitute_ReplacesDoubleBracePlaceholders()
    {
        var values = new Dictionary<string, string> { ["name"] = "world", ["kind"] = "test" };

        string result = PromptRenderer.Substitute("Hello {{name}}, this is a {{kind}}.", values);

        Assert.Equal("Hello world, this is a test.", result);
    }

    [Fact]
    public void Substitute_LeavesLiteralSingleBraceJsonUntouched()
    {
        // This is the whole reason for double braces: a JSON schema example in a prompt body
        // must survive rendering unchanged.
        const string template = "Return: {{shape}}\n{\n  \"id\": \"H1\"\n}";
        var values = new Dictionary<string, string> { ["shape"] = "an object" };

        string result = PromptRenderer.Substitute(template, values);

        Assert.Contains("an object", result);
        Assert.Contains("{\n  \"id\": \"H1\"\n}", result);
    }

    [Fact]
    public void Substitute_ThrowsOnUnknownPlaceholder()
    {
        var values = new Dictionary<string, string> { ["known"] = "x" };

        TemplateRenderException ex = Assert.Throws<TemplateRenderException>(
            () => PromptRenderer.Substitute("{{mystery}}", values));

        Assert.Contains("mystery", ex.Message);
        Assert.Contains("known", ex.Message);
    }

    [Fact]
    public void Render_BindsReservedContextPlaceholder()
    {
        var blocks = new[] { new ContextBlock("files: a.cs", "class A {}") };

        string result = PromptRenderer.Render("Before\n{{context}}\nAfter", inputs: null, context: blocks);

        Assert.Contains("## files: a.cs", result);
        Assert.Contains("class A {}", result);
        Assert.Contains("Before", result);
        Assert.Contains("After", result);
    }

    [Fact]
    public void Render_AppendsContextWhenTemplateDoesNotReferenceIt()
    {
        var blocks = new[] { new ContextBlock("diff", "+ added line") };

        string result = PromptRenderer.Render("Just the task.", inputs: null, context: blocks);

        Assert.Contains("Just the task.", result);
        Assert.Contains("# Context", result);
        Assert.Contains("+ added line", result);
    }

    [Fact]
    public void Render_FillsNamedInputs()
    {
        var inputs = new Dictionary<string, string> { ["symptom"] = "crash on login" };

        string result = PromptRenderer.Render("Symptom: {{symptom}}", inputs);

        Assert.Equal("Symptom: crash on login", result);
    }
}
