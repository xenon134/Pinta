//
// ActionManager.cs
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

namespace Pinta.Core;

public sealed class ActionManager
{
	public AppActions App { get; }
	public FileActions File { get; }
	public EditActions Edit { get; }
	public ViewActions View { get; }
	public ImageActions Image { get; }
	public LayerActions Layers { get; }
	public AdjustmentsActions Adjustments { get; }
	public EffectsActions Effects { get; }
	public WindowActions Window { get; }
	public HelpActions Help { get; }
	public AddinActions Addins { get; }

	private readonly SystemManager system;
	private readonly ChromeManager chrome;
	public ActionManager (
		ChromeManager chrome,
		ImageConverterManager imageFormats,
		PaletteFormatManager paletteFormats,
		PaletteManager palette,
		RecentFileManager recentFiles,
		SystemManager system,
		ToolManager tools,
		WorkspaceManager workspace)
	{
		// --- Action handlers that don't depend on other handlers

		AddinActions addins = new ();
		AdjustmentsActions adjustments = new ();
		AppActions app = new ();
		EditActions edit = new (chrome, paletteFormats, palette, tools, workspace);
		EffectsActions effects = new (chrome);
		ViewActions view = new (chrome, workspace);
		WindowActions window = new (workspace);

		// --- Action handlers that depend on other handlers

		FileActions file = new (system, app);
		HelpActions help = new (system, app);
		ImageActions image = new (tools, workspace, view);
		LayerActions layers = new (chrome, imageFormats, recentFiles, tools, workspace, image);

		// --- References to keep

		App = app;
		File = file;
		Edit = edit;
		View = view;
		Image = image;
		Layers = layers;
		Adjustments = adjustments;
		Effects = effects;
		Window = window;
		Help = help;
		Addins = addins;

		this.system = system;
		this.chrome = chrome;
	}

	public void CreateToolBar (Gtk.Box toolbar)
	{
		toolbar.Append (File.New.CreateToolBarItem ());
		toolbar.Append (File.Open.CreateToolBarItem ());
		toolbar.Append (File.Save.CreateToolBarItem ());
		// Printing is disabled for now until it is fully functional.
#if false
		toolbar.AppendItem (File.Print.CreateToolBarItem ());
#endif
		toolbar.Append (GtkExtensions.CreateToolBarSeparator ());

		// Cut/Copy/Paste comes before Undo/Redo on Windows
		if (system.OperatingSystem == OS.Windows) {
			toolbar.Append (Edit.Cut.CreateToolBarItem ());
			toolbar.Append (Edit.Copy.CreateToolBarItem ());
			toolbar.Append (Edit.Paste.CreateToolBarItem ());
			toolbar.Append (GtkExtensions.CreateToolBarSeparator ());
			toolbar.Append (Edit.Undo.CreateToolBarItem ());
			toolbar.Append (Edit.Redo.CreateToolBarItem ());
		} else {
			toolbar.Append (Edit.Undo.CreateToolBarItem ());
			toolbar.Append (Edit.Redo.CreateToolBarItem ());
			toolbar.Append (GtkExtensions.CreateToolBarSeparator ());
			toolbar.Append (Edit.Cut.CreateToolBarItem ());
			toolbar.Append (Edit.Copy.CreateToolBarItem ());
			toolbar.Append (Edit.Paste.CreateToolBarItem ());
		}

		toolbar.Append (GtkExtensions.CreateToolBarSeparator ());
		toolbar.Append (Image.CropToSelection.CreateToolBarItem ());
		toolbar.Append (Edit.Deselect.CreateToolBarItem ());
	}

	public void CreateHeaderToolBar (Adw.HeaderBar header)
	{
		header.PackStart (File.New.CreateToolBarItem ());
		header.PackStart (File.Open.CreateToolBarItem ());
		header.PackStart (File.Save.CreateToolBarItem ());

		header.PackStart (GtkExtensions.CreateToolBarSeparator ());
		header.PackStart (Edit.Undo.CreateToolBarItem ());
		header.PackStart (Edit.Redo.CreateToolBarItem ());

		header.PackStart (GtkExtensions.CreateToolBarSeparator ());
		header.PackStart (Edit.Cut.CreateToolBarItem ());
		header.PackStart (Edit.Copy.CreateToolBarItem ());
		header.PackStart (Edit.Paste.CreateToolBarItem ());

		header.PackStart (GtkExtensions.CreateToolBarSeparator ());
		header.PackStart (Image.CropToSelection.CreateToolBarItem ());
		header.PackStart (Edit.Deselect.CreateToolBarItem ());
	}

	public void CreateStatusBar (Gtk.Box statusbar, WorkspaceManager workspaceManager)
	{
		// Active tool widget - shows which tool is currently in use.
		Gtk.Image tool_icon = Gtk.Image.NewFromIconName (Resources.Icons.ToolPaintBrush);
		var tool_name = Gtk.Label.New (string.Empty);
		tool_name.Xalign = 0.0f;
		tool_name.Halign = Gtk.Align.Start;
		statusbar.Append (tool_icon);
		statusbar.Append (tool_name);

		PintaCore.Tools.ToolActivated += (_, e) => {
			tool_icon.IconName = e.Tool.Icon;
			tool_name.SetText (e.Tool.Name);
		};

		// Cursor position widget - left aligned with enough space to display coordinates up to tens of thousands (e.g. 10000, 10000).
		statusbar.Append (Gtk.Image.NewFromIconName (Resources.Icons.CursorPosition));
		var cursor = Gtk.Label.New ("0, 0");
		cursor.Xalign = 0.0f;
		cursor.Halign = Gtk.Align.Start;
		cursor.WidthChars = 11;
		statusbar.Append (cursor);

		chrome.LastCanvasCursorPointChanged += delegate {
			var pt = chrome.LastCanvasCursorPoint;
			cursor.SetText ($"{pt.X}, {pt.Y}");
		};

		// Selection size widget - blank when nothing is selected, so that it is
		// clear whether the size refers to a selection or to the canvas.
		statusbar.Append (Gtk.Image.NewFromIconName (Resources.Icons.ToolSelectRectangle));
		var selection_size = Gtk.Label.New (string.Empty);
		selection_size.Xalign = 0.0f;
		selection_size.Halign = Gtk.Align.Start;
		selection_size.WidthChars = 11;
		statusbar.Append (selection_size);

		// Canvas size widget - always shows the size of the active image.
		statusbar.Append (Gtk.Image.NewFromIconName (Resources.Icons.ImageResizeCanvas));
		var canvas_size = Gtk.Label.New (string.Empty);
		canvas_size.Xalign = 0.0f;
		canvas_size.Halign = Gtk.Align.Start;
		canvas_size.WidthChars = 11;
		statusbar.Append (canvas_size);

		void updateSizes ()
		{
			if (!workspaceManager.HasOpenDocuments) {
				selection_size.SetText (string.Empty);
				canvas_size.SetText (string.Empty);
				return;
			}

			Document document = workspaceManager.ActiveDocument;

			if (document.Selection.Visible) {
				RectangleD bounds = document.Selection.GetBounds ();
				selection_size.SetText ($"{bounds.Width}, {bounds.Height}");
			} else {
				selection_size.SetText (string.Empty);
			}

			canvas_size.SetText ($"{document.ImageSize.Width}, {document.ImageSize.Height}");
		}

		workspaceManager.SelectionChanged += (_, _) => updateSizes ();
		workspaceManager.ActiveDocumentChanged += (_, _) => updateSizes ();
		workspaceManager.ImageSizeChanged += (_, _) => updateSizes ();
		// Catches e.g. zoom changes, which can also refresh the readouts.
		workspaceManager.ViewSizeChanged += (_, _) => updateSizes ();

		updateSizes ();

		// Document zoom widget
		View.CreateStatusBar (statusbar);
	}

	public void RegisterHandlers ()
	{
		File.RegisterHandlers ();
		Edit.RegisterHandlers ();
		Image.RegisterHandlers ();
		Layers.RegisterHandlers ();
		View.RegisterHandlers ();
		Help.RegisterHandlers ();
	}
}
