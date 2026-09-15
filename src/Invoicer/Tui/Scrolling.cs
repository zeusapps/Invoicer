using System.Drawing;
using Terminal.Gui;

namespace Invoicer.Tui;

/// <summary>
/// Keeps views usable when the console window is smaller than their content.
/// </summary>
public static class Scrolling
{
    /// <summary>
    /// Lets a container of fixed-position controls scroll vertically instead of clipping its bottom
    /// rows. The content height follows the lowest subview, a scrollbar appears only when the content
    /// does not fit, and moving focus to a control out of view scrolls it into view so the form stays
    /// usable from the keyboard. Call after all subviews have been added.
    /// </summary>
    public static void EnableVertical(View container)
    {
        void SyncContentSize()
        {
            // Subviews that fill the container would otherwise grow with the content they define.
            var contentHeight = container.Subviews
                .Where(v => v.Visible && v.Height is not DimFill)
                .Select(v => v.Frame.Bottom)
                .DefaultIfEmpty(0)
                .Max();

            // Width tracks the viewport so Dim.Fill fields still stretch to the visible width.
            var size = new Size(container.Viewport.Width, Math.Max(contentHeight, container.Viewport.Height));
            if (container.GetContentSize() != size)
                container.SetContentSize(size);
        }

        container.VerticalScrollBar.AutoShow = true;
        container.ViewportChanged += (_, _) => SyncContentSize();
        container.SubviewsLaidOut += (_, _) => SyncContentSize();

        foreach (var child in container.Subviews.Where(v => v.CanFocus))
        {
            child.HasFocusChanged += (_, e) =>
            {
                if (e.NewValue)
                    ScrollIntoView(container, child);
            };
        }
    }

    /// <summary>Shows the scrollbar of a view that scrolls its own content (lists, text views) when it overflows.</summary>
    public static void ShowScrollBarWhenNeeded(View scrollingView)
    {
        scrollingView.VerticalScrollBar.AutoShow = true;
    }

    private static void ScrollIntoView(View container, View child)
    {
        var viewport = container.Viewport;
        if (child.Frame.Y < viewport.Y)
            container.Viewport = viewport with { Y = child.Frame.Y };
        else if (child.Frame.Bottom > viewport.Bottom)
            container.Viewport = viewport with { Y = Math.Max(0, child.Frame.Bottom - viewport.Height) };
    }
}
