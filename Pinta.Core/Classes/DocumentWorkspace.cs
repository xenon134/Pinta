//
// DocumentWorkspace.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
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

namespace Pinta.Core;

public sealed class DocumentWorkspace
{
	private readonly Document document;

	private readonly ActionManager actions;

	private enum ZoomType
	{
		ZoomIn,
		ZoomOut,
		ZoomManually,
	}

	internal DocumentWorkspace (
		ActionManager actions,
		Document document)
	{
		this.actions = actions;

		this.document = document;

		History = new DocumentHistory (document);
	}

	#region Public Events
	public event EventHandler<CanvasInvalidatedEventArgs>? CanvasInvalidated;
	public event EventHandler? ViewSizeChanged;
	#endregion

	#region Public Properties
	public Gtk.Widget Canvas { get; set; } = null!; // NRT - This is set soon after creation
	public Gtk.Widget CanvasWindow { get; set; } = null!; // NRT - This is set soon after creation

	/// <summary>
	/// Returns whether the zoomed image fits in the window without requiring scrolling.
	/// </summary>
	public bool ImageViewFitsInWindow {
		get {
			Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
			int window_x = view.GetAllocatedWidth ();
			int window_y = view.GetAllocatedHeight ();
			return ViewSize.Width <= window_x && ViewSize.Height <= window_y;
		}
	}

	/// <summary>
	/// Size of the zoomed image.
	/// </summary>
	public Size ViewSize {
		get => view_size;
		set {
			if (view_size == value) return;
			view_size = value;

			// The margin depends on the view size, and must be updated before
			// anything recalculates the scroll position.
			UpdateViewPadding ();

			OnViewSizeChanged ();
		}
	}
	private Size view_size;

	/// <summary>
	/// Blank space added around the canvas on each side, in view pixels.
	/// </summary>
	/// <remarks>
	/// This is applied by <see cref="UpdateViewPadding"/> so that the view can be
	/// scrolled a little past the image edges, and must be taken into account when
	/// converting between scroll positions and view coordinates.
	/// </remarks>
	public PointD ViewPadding => new (Canvas.MarginStart, Canvas.MarginTop);

	/// <summary>
	/// Extra scroll movement that still needs to be applied, in view pixels.
	/// </summary>
	private PointD pending_scroll_shift;

	/// <summary>
	/// Keep the canvas scrollable a little beyond its edges, so the image can be
	/// panned past the viewport border. The margin is half of the viewport, which
	/// means that even when scrolled all the way to an edge the image still covers
	/// half of the viewport.
	/// </summary>
	/// <remarks>
	/// This is called whenever the view size or the viewport size changes. Changing
	/// the margin moves the canvas within the scroll area, so the scroll position is
	/// adjusted by the same amount to avoid the image jumping. That adjustment is
	/// deferred until the scroll area has been recalculated, since the new margin is
	/// not taken into account until the next layout.
	/// </remarks>
	public void UpdateViewPadding ()
	{
		if (Canvas is null || Canvas.Parent is not Gtk.Viewport view)
			return;

		Gtk.Adjustment? h = view.GetHadjustment ();
		Gtk.Adjustment? v = view.GetVadjustment ();

		if (h is null || v is null)
			return;

		// Only add room when the image is larger than the viewport. Images that
		// fit stay exactly centered and don't get scrollbars.
		int padX = ViewSize.Width > h.PageSize ? (int) Math.Floor (h.PageSize / 2) : 0;
		int padY = ViewSize.Height > v.PageSize ? (int) Math.Floor (v.PageSize / 2) : 0;

		if (padX != Canvas.MarginStart || padY != Canvas.MarginTop) {
			pending_scroll_shift += new PointD (padX - Canvas.MarginStart, padY - Canvas.MarginTop);

			Canvas.MarginStart = Canvas.MarginEnd = padX;
			Canvas.MarginTop = Canvas.MarginBottom = padY;

			return; // Applied once the layout has taken the new margin into account.
		}

		if (pending_scroll_shift == PointD.Zero)
			return;

		// The scroll area has been recalculated, so the pending movement is now
		// within range and can be applied without being clamped.
		h.Value = Math.Clamp (h.Value + pending_scroll_shift.X, h.Lower, Math.Max (h.Lower, h.Upper - h.PageSize));
		v.Value = Math.Clamp (v.Value + pending_scroll_shift.Y, v.Lower, Math.Max (v.Lower, v.Upper - v.PageSize));

		pending_scroll_shift = PointD.Zero;
	}

	public DocumentHistory History { get; }

	/// <summary>
	/// Returns whether the image (at 100% zoom) would fit in the window without requiring scrolling.
	/// </summary>
	public bool ImageFitsInWindow {
		get {
			Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
			int window_x = view.GetAllocatedWidth ();
			int window_y = view.GetAllocatedHeight ();
			return document.ImageSize.Width <= window_x && document.ImageSize.Height <= window_y;
		}
	}

	/// <summary>
	/// Scale factor for the zoomed image.
	/// </summary>
	public double Scale {
		get => ViewSize.Width / (double) document.ImageSize.Width;
		set {
			if (value == ViewSize.Width / (double) document.ImageSize.Width && value == ViewSize.Height / (double) document.ImageSize.Height)
				return;

			document.ImageSize = CoercedToPositive (document.ImageSize);
			ViewSize = GetNewViewSize (document.ImageSize, value);

			Invalidate ();
		}
	}

	/// <summary>
	/// Ensures that the size has a width and a height of at least 1
	/// </summary>
	private static Size CoercedToPositive (Size baseSize)
		=> new (
			Width: Math.Max (baseSize.Width, 1),
			Height: Math.Max (baseSize.Height, 1));

	private static Size GetNewViewSize (Size imageSize, double scale)
	{
		int new_x = Math.Max ((int) (imageSize.Width * scale), 1);
		int new_y = Math.Max ((int) ((long) new_x * imageSize.Height / imageSize.Width), 1);
		return new (new_x, new_y);
	}

	#endregion

	#region Public Methods
	public void Invalidate ()
	{
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs ());
	}

	/// <summary>
	/// Repaints a rectangle region on the canvas.
	/// </summary>
	/// <param name='canvasRect'>
	/// The rectangle region of the canvas requiring repainting
	/// </param>
	public void Invalidate (RectangleI canvasRect)
	{
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs (canvasRect));
	}

	/// <summary>
	/// Repaints a rectangle region in the window.
	/// Note that this overload uses window coordinates, whereas Invalidate() uses canvas coordinates.
	/// </summary>
	public void InvalidateWindowRect (RectangleI windowRect)
	{
		PointD windowTopLeft = new (windowRect.Left, windowRect.Top);
		PointD windowBtmRight = new (windowRect.Right + 1, windowRect.Bottom + 1);

		PointD canvasTopLeft = ViewPointToCanvas (windowTopLeft);
		PointD canvasBtmRight = ViewPointToCanvas (windowBtmRight);

		RectangleI canvasRect = RectangleD.FromPoints (canvasTopLeft, canvasBtmRight).ToInt ();
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs (canvasRect));
	}

	/// <summary>
	/// Grabs focus to the canvas widget. This can be used to avoid leaving focus in
	/// toolbar widgets, for example.
	/// </summary>
	public void GrabFocusToCanvas ()
	{
		bool gained_focus = CanvasWindow.GrabFocus ();
		// Log a warning if something went wrong, e.g. there is a non-focusable widget
		// in the hierarchy.
		if (!gained_focus)
			Console.Error.WriteLine ("Failed to gain focus on the canvas widget!");
	}

	/// <summary>
	/// Determines whether the rectangle lies (at least partially) outside the canvas area.
	/// </summary>
	public bool IsPartiallyOffscreen (RectangleI rect)
		=> rect.IsEmpty || rect.Left < 0 || rect.Top < 0;

	public bool PointInCanvas (PointD point)
	{
		if (point.X < 0 || point.Y < 0)
			return false;

		if (point.X >= document.ImageSize.Width || point.Y >= document.ImageSize.Height)
			return false;

		return true;
	}

	public void RecenterView (PointD point)
	{
		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
		PointD padding = ViewPadding;

		var h_adjust = view.GetHadjustment ()!;
		h_adjust.Value = Math.Clamp (point.X * Scale + padding.X - h_adjust.PageSize / 2, h_adjust.Lower, h_adjust.Upper);

		var v_adjust = view.GetVadjustment ()!;
		v_adjust.Value = Math.Clamp (point.Y * Scale + padding.Y - v_adjust.PageSize / 2, v_adjust.Lower, v_adjust.Upper);

		// The view was positioned explicitly, taking the padding into account.
		pending_scroll_shift = PointD.Zero;
	}

	public void ScrollCanvas (PointI delta)
	{
		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;

		var h_adjust = view.GetHadjustment ()!;
		h_adjust.Value = Math.Clamp (delta.X + h_adjust.Value, h_adjust.Lower, h_adjust.Upper - h_adjust.PageSize);

		var v_adjust = view.GetVadjustment ()!;
		v_adjust.Value = Math.Clamp (delta.Y + v_adjust.Value, v_adjust.Lower, v_adjust.Upper - v_adjust.PageSize);
	}

	/// <summary>
	/// Converts a point from image view coordinates to canvas coordinates
	/// </summary>
	/// <param name='x'>
	/// The X coordinate of the view point
	/// </param>
	/// <param name='y'>
	/// The Y coordinate of the view point
	/// </param>
	public PointD ViewPointToCanvas (PointD viewPoint)
	{
		Fraction<int> sf = ScaleFactor.CreateClamped (document.ImageSize.Width, ViewSize.Width);
		PointD pt = sf.ScalePoint (viewPoint);
		return new (pt.X, pt.Y);
	}

	/// <summary>
	/// Converts a point from canvas coordinates to view coordinates
	/// </summary>
	public PointD CanvasPointToView (PointD canvasPoint)
	{
		Fraction<int> sf = ScaleFactor.CreateClamped (document.ImageSize.Width, ViewSize.Width);
		return sf.UnscalePoint (canvasPoint);
	}

	public void ZoomIn ()
	{
		ZoomAroundCenter (ZoomType.ZoomIn);
	}

	public void ZoomOut ()
	{
		ZoomAroundCenter (ZoomType.ZoomOut);
	}

	public void ZoomInAroundViewPoint (in PointD view_point)
	{
		ZoomAroundViewPoint (ZoomType.ZoomIn, view_point); // Zoom in relative to mouse position.
	}

	public void ZoomInAroundCanvasPoint (in PointD canvas_point)
	{
		ZoomInAroundViewPoint (CanvasPointToView (canvas_point));
	}

	public void ZoomOutAroundViewPoint (in PointD view_point)
	{
		ZoomAroundViewPoint (ZoomType.ZoomOut, view_point); // Zoom out relative to mouse position.
	}

	public void ZoomOutAroundCanvasPoint (in PointD canvas_point)
	{
		ZoomOutAroundViewPoint (CanvasPointToView (canvas_point));
	}

	/// <summary>
	/// Zoom in or out while keeping the canvas point under the given view point
	/// in place.
	/// </summary>
	private void ZoomAroundViewPoint (ZoomType zoom_type, in PointD view_point)
	{
		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
		PointD padding = ViewPadding;

		// Where the point currently appears within the viewport.
		PointD anchor = new (
			padding.X - view.Hadjustment!.Value + view_point.X,
			padding.Y - view.Vadjustment!.Value + view_point.Y);

		ZoomAndRecenterView (zoom_type, ViewPointToCanvas (view_point), anchor);
	}

	public void ZoomManually ()
	{
		ZoomAroundCenter (ZoomType.ZoomManually);
	}

	public void ZoomToCanvasRectangle (RectangleD rect)
	{
		double ratio =
			(document.ImageSize.Width / rect.Width <= document.ImageSize.Height / rect.Height)
			? document.ImageSize.Width / rect.Width
			: document.ImageSize.Height / rect.Height;

		actions.View.ZoomComboBox.ComboBox.GetEntry ().SetText (ViewActions.ToPercent (ratio));
		GLib.MainContext.Default ().Iteration (false); //Force update of scrollbar upper before recenter

		PointD newPoint = new (
			X: rect.X + rect.Width / 2,
			Y: rect.Y + rect.Height / 2);

		RecenterView (newPoint);
	}
	#endregion

	#region Private Methods
	private void OnCanvasInvalidated (CanvasInvalidatedEventArgs e)
	{
		CanvasInvalidated?.Invoke (this, e);
	}

	public void OnViewSizeChanged ()
	{
		ViewSizeChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>
	/// Zoom in/out around the center of the screen.
	/// </summary>
	private void ZoomAroundCenter (ZoomType zoomType)
	{
		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
		PointD padding = ViewPadding;

		// The center of the viewport is the anchor, and the view point it
		// currently displays is offset by the scroll position and padding.
		PointD anchor = new (
			view.Hadjustment!.PageSize / 2.0,
			view.Vadjustment!.PageSize / 2.0);

		PointD center_view = new (
			view.Hadjustment.Value + anchor.X - padding.X,
			view.Vadjustment.Value + anchor.Y - padding.Y);

		ZoomAndRecenterView (zoomType, ViewPointToCanvas (center_view), anchor);
	}

	/// <summary>
	/// Zoom in or out around a specific canvas point.
	/// </summary>
	/// <param name="zoomType">Direction to zoom in.</param>
	/// <param name="canvas_point">Canvas point that should stay in place.</param>
	/// <param name="anchor_point">Where that point currently appears in the viewport, in view pixels.</param>
	private void ZoomAndRecenterView (ZoomType zoomType, PointD canvas_point, PointD anchor_point)
	{
		if (zoomType == ZoomType.ZoomOut && (ViewSize.Width == 1 || ViewSize.Height == 1))
			return; //Can't zoom in past a 1x1 px canvas

		if (!ViewActions.TryParsePercent (actions.View.ZoomComboBox.ComboBox.GetActiveText ()!, out var zoom))
			zoom = Scale * 100;

		zoom = Math.Min (zoom, 3600);

		actions.View.SuspendZoomUpdate ();

		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;

		if (zoomType == ZoomType.ZoomIn || zoomType == ZoomType.ZoomOut) {

			int i = 0;

			bool UpdateZoomLevel (string zoomInList)
			{
				if (!ViewActions.TryParsePercent (zoomInList, out var zoom_level))
					return false;

				switch (zoomType) {
					case ZoomType.ZoomIn:

						if (zoomInList == Translations.GetString ("Window") || zoom_level <= zoom) {
							actions.View.ZoomComboBox.ComboBox.Active = i - 1;
							return true;
						}

						break;

					case ZoomType.ZoomOut:

						if (zoomInList == Translations.GetString ("Window"))
							return true;

						if (zoom_level < zoom) {
							actions.View.ZoomComboBox.ComboBox.Active = i;
							return true;
						}

						break;
				}
				return false;
			}

			foreach (string item in actions.View.ZoomCollection) {

				if (UpdateZoomLevel (item))
					break;

				i++;
			}
		}

		actions.View.UpdateCanvasScale ();

		PointD padding = ViewPadding;

		// Quick fix : need to manually update Upper limit because the value is not changing after updating the canvas scale.
		// TODO : I think there is an event need to be fired so that those values updated automatically.
		view.Hadjustment!.Upper = Math.Max (ViewSize.Width + 2 * padding.X, view.Hadjustment.PageSize);
		view.Vadjustment!.Upper = Math.Max (ViewSize.Height + 2 * padding.Y, view.Vadjustment.PageSize);

		// Scroll so that the canvas position under the anchor is still in the same place.
		// Note that the canvas widget might not have resized yet, so using the anchor is
		// important for taking the size difference into account.
		PointD new_point = CanvasPointToView (canvas_point);
		view.Hadjustment.Value = new_point.X + padding.X - anchor_point.X;
		view.Vadjustment.Value = new_point.Y + padding.Y - anchor_point.Y;

		// The view was positioned explicitly, taking the new padding into account.
		pending_scroll_shift = PointD.Zero;

		actions.View.ResumeZoomUpdate ();
	}

	#endregion
}
