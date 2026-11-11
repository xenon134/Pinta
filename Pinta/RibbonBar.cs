// 
// RibbonBar.cs
//  
// Helper for building Paint-style ribbon groups: a horizontal run of controls
// with a small caption underneath, separated by vertical rules.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using Pinta.Core;

namespace Pinta;

/// <summary>
/// Builds the grouped, captioned sections of a ribbon-style toolbar.
/// </summary>
internal static class RibbonBar
{
	/// <summary>
	/// Wraps a set of controls in a vertical stack with a caption underneath,
	/// matching the grouping used by Paint ("Selection", "Image", "Tools", ...).
	/// </summary>
	public static Gtk.Widget CreateGroup (string caption, Gtk.Widget content)
	{
		Gtk.Box group = Gtk.Box.New (Gtk.Orientation.Vertical, 0);
		group.AddCssClass ("ribbon-group");
		group.MarginStart = 6;
		group.MarginEnd = 6;
		group.MarginTop = 3;
		group.MarginBottom = 3;

		content.Halign = Gtk.Align.Center;
		content.Valign = Gtk.Align.Center;
		group.Append (content);

		Gtk.Label label = Gtk.Label.New (caption);
		label.AddCssClass (AdwaitaStyles.DimLabel);
		label.AddCssClass ("caption");
		label.Halign = Gtk.Align.Center;
		group.Append (label);

		return group;
	}

	/// <summary>
	/// Vertical rule between two ribbon groups.
	/// </summary>
	public static Gtk.Widget CreateSeparator ()
	{
		Gtk.Separator separator = Gtk.Separator.New (Gtk.Orientation.Vertical);
		separator.MarginTop = 6;
		separator.MarginBottom = 6;
		separator.MarginStart = 4;
		separator.MarginEnd = 4;
		return separator;
	}

	/// <summary>
	/// A horizontal container for a group's controls.
	/// </summary>
	public static Gtk.Box CreateContent ()
	{
		Gtk.Box content = Gtk.Box.New (Gtk.Orientation.Horizontal, 0);
		content.Valign = Gtk.Align.Center;
		return content;
	}
}
