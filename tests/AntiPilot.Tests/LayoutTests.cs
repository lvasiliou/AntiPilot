using AntiPilot.UI.Fluent;
using Xunit;

namespace AntiPilot.Tests;

/// <summary>
/// The measurement the settings window sizes its cards by.
///
/// Cards used to be told their height, so a longer translation was cut off rather than wrapped —
/// visible in every language but English, and worse the further the display was scaled. These
/// pin the property that replaced it: text asks for the room it needs.
/// </summary>
public class LayoutTests
{
    private static readonly Font Body = new("Segoe UI", 9f);

    [Fact]
    public void Text_too_long_for_one_line_asks_for_more_height()
    {
        const string sentence = "A second press within the window below runs the action set at the bottom of this page.";

        int oneLine = FluentPaint.WrappedHeight("Short", Body, 200);
        int wrapped = FluentPaint.WrappedHeight(sentence, Body, 200);

        Assert.True(wrapped > oneLine, $"expected the long sentence to need more than {oneLine}px, got {wrapped}px");
    }

    [Fact]
    public void Narrower_means_taller()
    {
        const string sentence = "A second press within the window below runs the action set at the bottom of this page.";

        Assert.True(FluentPaint.WrappedHeight(sentence, Body, 150) > FluentPaint.WrappedHeight(sentence, Body, 400));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Nothing_to_draw_takes_no_room(string? text)
    {
        Assert.Equal(0, FluentPaint.WrappedHeight(text!, Body, 200));
    }

    [Fact]
    public void A_width_that_has_run_out_does_not_throw()
    {
        // A very narrow window, or a card whose action has eaten the row: measuring must still
        // answer rather than ask GDI to lay text out in nothing.
        Assert.Equal(0, FluentPaint.WrappedHeight("Some text", Body, 0));
        Assert.Equal(0, FluentPaint.WrappedHeight("Some text", Body, -40));
    }
}
