// 
// RibbonControls.cs
//  
// Small building blocks for the Paint-style ribbon: dropdown buttons, menu
// entries, and the big primary button used by the Selection and Brushes groups.
// 

using System;
using System.Linq;
using Pinta.Core;
using Pinta.Gui.Widgets;
using Pinta.Tools;

namespace Pinta;

/// <summary>
/// Helpers for building the ribbon's buttons and dropdown menus.
/// </summary>
internal static class RibbonControls
{
	private const string ChevronIcon = "pan-down-symbolic";

	/// <summary>
	/// A large flat toggle button, used for the primary tool of a ribbon group.
	/// </summary>
	public static Gtk.ToggleButton CreateBigToggle (string tooltip)
	{
		Gtk.ToggleButton button = Gtk.ToggleButton.New ();
		button.FocusOnClick = false;
		button.WidthRequest = 48;
		button.HeightRequest = 48;
		button.TooltipText = tooltip;
		button.SetCssClasses ([AdwaitaStyles.Flat]);
		button.AddCssClass ("ribbon-big-button");
		return button;
	}

	/// <summary>
	/// Places a small dropdown chevron in the bottom-right corner of a button,
	/// like the arrow drawn inside Paint's Selection and Brushes buttons.
	/// </summary>
	public static Gtk.Widget AddChevron (Gtk.Widget button, Gtk.Popover popover, string tooltip)
	{
		Gtk.MenuButton chevron = Gtk.MenuButton.New ();
		chevron.IconName = ChevronIcon;
		chevron.Popover = popover;
		chevron.TooltipText = tooltip;
		chevron.Halign = Gtk.Align.End;
		chevron.Valign = Gtk.Align.End;
		chevron.MarginEnd = 1;
		chevron.MarginBottom = 1;
		chevron.SetCssClasses ([AdwaitaStyles.Flat]);
		chevron.AddCssClass ("ribbon-chevron");

		Gtk.Overlay overlay = Gtk.Overlay.New ();
		overlay.Child = button;
		overlay.AddOverlay (chevron);
		return overlay;
	}

	/// <summary>
	/// An icon button with a chevron that opens a dropdown menu.
	/// </summary>
	public static Gtk.MenuButton CreateMenuButton (string icon, string tooltip, Gtk.Popover popover)
	{
		Gtk.Image image = Gtk.Image.NewFromIconName (icon);
		image.PixelSize = 16;

		Gtk.MenuButton button = Gtk.MenuButton.New ();
		button.Child = image;
		button.Popover = popover;
		button.TooltipText = tooltip;
		button.FocusOnClick = false;
		button.SetCssClasses ([AdwaitaStyles.Flat]);
		button.AddCssClass ("ribbon-menu-button");
		return button;
	}

	/// <summary>
	/// A large icon button with a chevron that opens a dropdown menu, used by
	/// the Shapes group.
	/// </summary>
	public static Gtk.MenuButton CreateBigMenuButton (string icon, string tooltip, Gtk.Popover popover)
	{
		Gtk.Image image = Gtk.Image.NewFromIconName (icon);
		image.PixelSize = 32;

		Gtk.Image chevron = Gtk.Image.NewFromIconName (ChevronIcon);
		chevron.Halign = Gtk.Align.End;
		chevron.Valign = Gtk.Align.End;

		Gtk.Overlay overlay = Gtk.Overlay.New ();
		overlay.Child = image;
		overlay.AddOverlay (chevron);

		Gtk.MenuButton button = Gtk.MenuButton.New ();
		button.Child = overlay;
		button.Popover = popover;
		button.TooltipText = tooltip;
		button.WidthRequest = 48;
		button.HeightRequest = 48;
		button.FocusOnClick = false;
		button.SetCssClasses ([AdwaitaStyles.Flat]);
		button.AddCssClass ("ribbon-big-button");
		return button;
	}

	/// <summary>
	/// Vertical container for the entries of a dropdown.
	/// </summary>
	public static Gtk.Box CreateMenu ()
	{
		Gtk.Box menu = Gtk.Box.New (Gtk.Orientation.Vertical, 2);
		menu.MarginStart = 6;
		menu.MarginEnd = 6;
		menu.MarginTop = 6;
		menu.MarginBottom = 6;
		menu.WidthRequest = 190;
		return menu;
	}

	/// <summary>
	/// Adds an entry that activates a tool.
	/// </summary>
	public static void AppendToolEntry (Gtk.Box menu, ToolManager tools, BaseTool tool, Gtk.Popover popover)
	{
		Gtk.Box content = Gtk.Box.New (Gtk.Orientation.Horizontal, 8);
		Gtk.Image image = Gtk.Image.NewFromIconName (tool.Icon);
		image.PixelSize = 16;
		content.Append (image);

		Gtk.Label label = Gtk.Label.New (tool.Name);
		label.Xalign = 0.0f;
		content.Append (label);

		menu.Append (CreateEntryButton (content, () => {
			popover.Popdown ();
			tools.SetCurrentTool (tool);
		}));
	}

	/// <summary>
	/// Adds an entry that runs a command.
	/// </summary>
	public static void AppendCommandEntry (Gtk.Box menu, Command command, Gtk.Popover popover)
		=> menu.Append (CreateEntryButton (CreateEntryLabel (command.Label), () => {
			popover.Popdown ();
			command.Activate ();
		}));

	/// <summary>
	/// Adds a horizontal rule between two groups of entries.
	/// </summary>
	public static void AppendSeparator (Gtk.Box menu)
		=> menu.Append (Gtk.Separator.New (Gtk.Orientation.Horizontal));

	/// <summary>
	/// A menu entry with just a label, e.g. for a brush type.
	/// </summary>
	public static void AppendLabeledEntry (Gtk.Box menu, string label, Action on_clicked)
		=> menu.Append (CreateEntryButton (CreateEntryLabel (label), on_clicked));

	private static Gtk.Widget CreateEntryLabel (string text)
	{
		Gtk.Label label = Gtk.Label.New (text);
		label.Xalign = 0.0f;
		return label;
	}

	private static Gtk.Button CreateEntryButton (Gtk.Widget content, Action on_clicked)
	{
		Gtk.Button button = Gtk.Button.New ();
		button.Child = content;
		button.Halign = Gtk.Align.Fill;
		button.SetCssClasses ([AdwaitaStyles.Flat]);
		button.OnClicked += (_, _) => on_clicked ();
		return button;
	}

	/// <summary>
	/// Keeps the ribbon's big Selection and Brushes buttons in sync with the
	/// active tool, so that selecting a tool from a dropdown leaves exactly one
	/// button highlighted.
	/// </summary>
	public sealed class PrimaryButtonSync
	{
		private readonly ToolManager tools;
		private readonly Gtk.ToggleButton selection_button;
		private readonly Gtk.ToggleButton brushes_button;
		// Tools that have a button of their own in the Tools group; their
		// activation is already reflected by that button's shared toggle group.
		private readonly Func<BaseTool, bool> has_tools_button;
		// Invisible member of the toggle group, used to clear the visible buttons
		// when a tool with no button of its own (e.g. one from the Shapes menu)
		// is active.
		private readonly Gtk.ToggleButton none_button = Gtk.ToggleButton.New ();
		private BaseTool? last_selection_tool;

		public PrimaryButtonSync (
			ToolManager tools,
			Gtk.ToggleButton selection_button,
			Gtk.ToggleButton brushes_button,
			Func<BaseTool, bool> has_tools_button)
		{
			this.tools = tools;
			this.selection_button = selection_button;
			this.brushes_button = brushes_button;
			this.has_tools_button = has_tools_button;

			selection_button.Group = ToolBoxWidget.SharedToggleGroup;
			brushes_button.Group = ToolBoxWidget.SharedToggleGroup;
			none_button.Group = ToolBoxWidget.SharedToggleGroup;

			selection_button.OnClicked += (_, _) => {
				if (last_selection_tool is null)
					tools.SetCurrentTool (nameof (RectangleSelectTool));
				else
					tools.SetCurrentTool (last_selection_tool);
			};

			brushes_button.OnClicked += (_, _) => {
				if (FindPaintBrush () is BaseTool brush)
					tools.SetCurrentTool (brush);
			};

			tools.ToolActivated += (_, e) => HandleToolActivated (e.Tool);
		}

		private static BaseTool? FindPaintBrush ()
			=> PintaCore.Tools.OfType<PaintBrushTool> ().FirstOrDefault ();

		private void HandleToolActivated (BaseTool tool)
		{
			if (tool.IsSelectionTool || tool is MoveSelectedTool) {
				last_selection_tool = tool;
				selection_button.IconName = tool.Icon;
				selection_button.Active = true;
				return;
			}

			if (tool is PaintBrushTool) {
				brushes_button.Active = true;
				return;
			}

			if (has_tools_button (tool))
				return; // The tool's own button in the Tools group handles this.

			// A tool with no visible button of its own.
			none_button.Active = true;
		}
	}
}
