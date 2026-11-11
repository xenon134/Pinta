// 
// CanvasResizeHandles.cs
//  
// Edge and corner handles that resize the canvas, in the style of Paint.
// Dragging converts the pointer motion into whole image pixels, so the
// resulting canvas size always snaps to an integer pixel grid.
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

using System;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

/// <summary>
/// The eight canvas resize handles. Named after the edge / corner they sit on.
/// </summary>
internal enum ResizeHandle
{
	None,
	NW,
	N,
	NE,
	E,
	SE,
	S,
	SW,
	W,
}

/// <summary>
/// Tracks a canvas resize drag and draws the handles and rubber band.
/// All sizes are snapped to whole image pixels.
/// </summary>
internal sealed class CanvasResizeHandles
{
	private const double HANDLE_SIZE = 9.0;
	private const double HIT_SIZE = HANDLE_SIZE + 4.0;
	private const double MIN_VIEW_SIZE = 32.0;

	private ResizeHandle active;
	private PointD start_point;
	private int start_left;
	private int start_top;
	private int start_right;
	private int start_bottom;

	public ResizeHandle Active => active;
	public bool IsDragging => active != ResizeHandle.None;

	/// <summary>
	/// The canvas size the current drag would produce.
	/// </summary>
	public Size PreviewSize { get; private set; }

	/// <summary>
	/// The anchor that keeps the edge opposite the dragged handle in place.
	/// </summary>
	public Anchor TargetAnchor => active switch {
		ResizeHandle.SE => Anchor.NW,
		ResizeHandle.S => Anchor.N,
		ResizeHandle.SW => Anchor.NE,
		ResizeHandle.E => Anchor.W,
		ResizeHandle.W => Anchor.E,
		ResizeHandle.NE => Anchor.SW,
		ResizeHandle.N => Anchor.S,
		ResizeHandle.NW => Anchor.SE,
		_ => Anchor.NW,
	};

	public void Begin (ResizeHandle handle, PointD view_point, Size image_size)
	{
		active = handle;
		start_point = view_point;

		start_left = 0;
		start_top = 0;
		start_right = image_size.Width;
		start_bottom = image_size.Height;

		PreviewSize = image_size;
	}

	public void Cancel ()
	{
		active = ResizeHandle.None;
	}

	/// <summary>
	/// Recompute the preview size for a new pointer position.
	/// Returns true if the preview size actually changed.
	/// </summary>
	public bool Update (PointD view_point, double scale)
	{
		if (!IsDragging || scale <= 0)
			return false;

		// Snap the pointer motion to whole image pixels.
		int dx = (int) Math.Round ((view_point.X - start_point.X) / scale);
		int dy = (int) Math.Round ((view_point.Y - start_point.Y) / scale);

		int left = start_left;
		int top = start_top;
		int right = start_right;
		int bottom = start_bottom;

		switch (active) {
			case ResizeHandle.W:
			case ResizeHandle.NW:
			case ResizeHandle.SW:
				left += dx;
				break;
			case ResizeHandle.E:
			case ResizeHandle.NE:
			case ResizeHandle.SE:
				right += dx;
				break;
		}

		switch (active) {
			case ResizeHandle.N:
			case ResizeHandle.NW:
			case ResizeHandle.NE:
				top += dy;
				break;
			case ResizeHandle.S:
			case ResizeHandle.SW:
			case ResizeHandle.SE:
				bottom += dy;
				break;
		}

		// Keep at least one pixel in each dimension, and never let the dragged
		// edge cross the opposite one.
		if (right - left < 1) {
			if (left != start_left)
				left = right - 1;
			else
				right = left + 1;
		}

		if (bottom - top < 1) {
			if (top != start_top)
				top = bottom - 1;
			else
				bottom = top + 1;
		}

		Size new_size = new (right - left, bottom - top);

		if (new_size == PreviewSize)
			return false;

		PreviewSize = new_size;
		return true;
	}

	/// <summary>
	/// Position of a handle in view (window) coordinates. The centres sit on
	/// the image border so that the handles are drawn outside the image.
	/// </summary>
	private static PointD GetHandleCenter (Size view_size, ResizeHandle handle)
	{
		double left = 0;
		double top = 0;
		double right = view_size.Width;
		double bottom = view_size.Height;

		double x = handle switch {
			ResizeHandle.NW or ResizeHandle.W or ResizeHandle.SW => left,
			ResizeHandle.NE or ResizeHandle.E or ResizeHandle.SE => right,
			_ => (left + right) / 2.0,
		};

		double y = handle switch {
			ResizeHandle.NW or ResizeHandle.N or ResizeHandle.NE => top,
			ResizeHandle.SW or ResizeHandle.S or ResizeHandle.SE => bottom,
			_ => (top + bottom) / 2.0,
		};

		return new PointD (x, y);
	}

	/// <summary>
	/// Test whether a view point lands on one of the handle squares.
	/// Only the small squares are active, so drawing along the canvas edge
	/// still reaches the current tool.
	/// </summary>
	public static ResizeHandle HitTest (Size view_size, PointD view_point)
	{
		if (view_size.Width < MIN_VIEW_SIZE || view_size.Height < MIN_VIEW_SIZE)
			return ResizeHandle.None;

		foreach (ResizeHandle handle in Enum.GetValues<ResizeHandle> ()) {
			if (handle == ResizeHandle.None)
				continue;

			PointD center = GetHandleCenter (view_size, handle);

			if (Math.Abs (view_point.X - center.X) <= HIT_SIZE / 2.0 &&
			    Math.Abs (view_point.Y - center.Y) <= HIT_SIZE / 2.0)
				return handle;
		}

		return ResizeHandle.None;
	}

	/// <summary>
	/// Cursor name to show while hovering a handle.
	/// </summary>
	public static string? GetCursorName (ResizeHandle handle)
		=> handle switch {
			ResizeHandle.NW => "nw-resize",
			ResizeHandle.N => "n-resize",
			ResizeHandle.NE => "ne-resize",
			ResizeHandle.E => "e-resize",
			ResizeHandle.SE => "se-resize",
			ResizeHandle.S => "s-resize",
			ResizeHandle.SW => "sw-resize",
			ResizeHandle.W => "w-resize",
			_ => null,
		};

	public void Draw (Gtk.Snapshot snapshot, Size view_size, double scale, Gtk.Widget widget)
	{
		if (view_size.Width < MIN_VIEW_SIZE || view_size.Height < MIN_VIEW_SIZE)
			return;

		if (IsDragging)
			DrawRubberBand (snapshot, view_size, scale, widget);

		Gdk.RGBA fill = new () { Red = 1, Green = 1, Blue = 1, Alpha = 1 };
		Gdk.RGBA border = new () { Red = 0.25f, Green = 0.25f, Blue = 0.25f, Alpha = 1 };

		foreach (ResizeHandle handle in Enum.GetValues<ResizeHandle> ()) {
			if (handle == ResizeHandle.None)
				continue;

			PointD center = GetHandleCenter (view_size, handle);
			Graphene.Rect rect = Graphene.Rect.Alloc ();
			rect.Init (
				(float) (center.X - HANDLE_SIZE / 2.0),
				(float) (center.Y - HANDLE_SIZE / 2.0),
				(float) HANDLE_SIZE,
				(float) HANDLE_SIZE);

			Gsk.PathBuilder builder = Gsk.PathBuilder.New ();
			builder.AddRect (rect);
			Gsk.Path path = builder.ToPath ();

			snapshot.AppendFill (path, Gsk.FillRule.Winding, fill);
			snapshot.AppendStroke (path, Gsk.Stroke.New (1.0f), border);
		}
	}

	private void DrawRubberBand (Gtk.Snapshot snapshot, Size view_size, double scale, Gtk.Widget widget)
	{
		float width = (float) (PreviewSize.Width * scale);
		float height = (float) (PreviewSize.Height * scale);
		if (width < 1 || height < 1)
			return;

		// Anchor the preview to the edge or corner that stays fixed, so that
		// dragging the left or top handle previews the new extent on that side
		// instead of always extending towards the right and bottom.
		float x = IsEastAnchored ? (float) view_size.Width - width : 0;
		float y = IsSouthAnchored ? (float) view_size.Height - height : 0;

		Graphene.Rect rect = Graphene.Rect.Alloc ();
		rect.Init (x, y, width, height);

		Gsk.PathBuilder builder = Gsk.PathBuilder.New ();
		builder.AddRect (rect);
		Gsk.Path path = builder.ToPath ();

		Gsk.Stroke stroke = Gsk.Stroke.New (1.0f);
		stroke.SetDash ([4.0f, 4.0f]);
		Gdk.RGBA accent = new () { Red = 0.0f, Green = 0.4f, Blue = 0.75f, Alpha = 1 };
		snapshot.AppendStroke (path, stroke, accent);

		DrawSizeLabel (snapshot, widget, view_size, x, y, width, height);
	}

	private bool IsEastAnchored => TargetAnchor is Anchor.NE or Anchor.E or Anchor.SE;

	private bool IsSouthAnchored => TargetAnchor is Anchor.SW or Anchor.S or Anchor.SE;

	private static void DrawSizeLabel (Gtk.Snapshot snapshot, Gtk.Widget widget, Size view_size, float x, float y, float width, float height)
	{
		Pango.Layout layout = widget.CreatePangoLayout ($"{Math.Round (width)} × {Math.Round (height)}");
		layout.GetPixelSize (out int textWidth, out int textHeight);

		// Keep the label inside the image so that it stays visible even when
		// the preview extends beyond the canvas.
		float label_x = Math.Clamp (x + width - textWidth - 12, 0, Math.Max (0, (float) view_size.Width - textWidth - 8));
		float label_y = Math.Clamp (y + height - textHeight - 12, 0, Math.Max (0, (float) view_size.Height - textHeight - 8));

		Graphene.Rect background = Graphene.Rect.Alloc ();
		background.Init (label_x - 4, label_y - 3, textWidth + 8, textHeight + 6);

		Gsk.PathBuilder builder = Gsk.PathBuilder.New ();
		builder.AddRect (background);
		Gdk.RGBA background_colour = new () { Red = 0, Green = 0, Blue = 0, Alpha = 0.75f };
		snapshot.AppendFill (builder.ToPath (), Gsk.FillRule.Winding, background_colour);

		snapshot.Save ();
		snapshot.Translate (new Graphene.Point { X = label_x, Y = label_y });
		Gdk.RGBA text_colour = new () { Red = 1, Green = 1, Blue = 1, Alpha = 1 };
		snapshot.AppendLayout (layout, text_colour);
		snapshot.Restore ();
	}
}
