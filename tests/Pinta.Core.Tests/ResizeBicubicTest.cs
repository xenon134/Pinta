using System;
using Cairo;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class ResizeBicubicTest
{
	static ResizeBicubicTest ()
	{
		// Registers the GirCore native library resolver, which maps the
		// "cairo-graphics" import to the system libcairo. The other fixtures
		// get this from the Utilities static constructor.
		Cairo.Module.Initialize ();
	}

	private static ImageSurface CreateSurface (int width, int height, ColorBgra colour)
	{
		ImageSurface surface = CairoExtensions.CreateImageSurface (Format.Argb32, width, height);
		Span<ColorBgra> data = surface.GetPixelData ();

		for (int i = 0; i < data.Length; i++)
			data[i] = colour;

		surface.MarkDirty ();
		return surface;
	}

	[Test]
	public void ResizeProducesRequestedSize ()
	{
		using ImageSurface source = CreateSurface (4, 3, ColorBgra.Red);
		using ImageSurface result = CairoExtensions.ResizeBicubic (source, new Size (9, 7));

		Assert.That (result.Width, Is.EqualTo (9));
		Assert.That (result.Height, Is.EqualTo (7));
	}

	[Test]
	public void UniformColourIsPreserved ()
	{
		ColorBgra colour = ColorBgra.FromBgra (40, 80, 120, 200);
		using ImageSurface source = CreateSurface (5, 5, colour);

		foreach (Size newSize in new[] { new Size (13, 11), new Size (2, 3) }) {
			using ImageSurface result = CairoExtensions.ResizeBicubic (source, newSize);
			Span<ColorBgra> data = result.GetPixelData ();

			foreach (ColorBgra pixel in data) {
				Assert.That (pixel.B, Is.EqualTo (colour.B).Within (1));
				Assert.That (pixel.G, Is.EqualTo (colour.G).Within (1));
				Assert.That (pixel.R, Is.EqualTo (colour.R).Within (1));
				Assert.That (pixel.A, Is.EqualTo (colour.A).Within (1));
			}
		}
	}

	[Test]
	public void UpscaleInterpolatesBetweenNeighbours ()
	{
		const int width = 2;
		const int height = 1;

		using ImageSurface source = CairoExtensions.CreateImageSurface (Format.Argb32, width, height);
		Span<ColorBgra> sourceData = source.GetPixelData ();
		sourceData[0] = ColorBgra.Black;
		sourceData[1] = ColorBgra.White;
		source.MarkDirty ();

		using ImageSurface result = CairoExtensions.ResizeBicubic (source, new Size (32, 1));
		Span<ColorBgra> data = result.GetPixelData ();

		// The middle of the image should contain a value strictly between the
		// two source colours, which distinguishes bicubic from nearest neighbour.
		byte middle = data[16].R;
		Assert.That (middle, Is.GreaterThan (0));
		Assert.That (middle, Is.LessThan (255));
	}

	[Test]
	public void SameSizeReturnsCopy ()
	{
		using ImageSurface source = CreateSurface (3, 2, ColorBgra.Blue);
		using ImageSurface result = CairoExtensions.ResizeBicubic (source, new Size (3, 2));

		Assert.That (result, Is.Not.SameAs (source));
		Assert.That (result.Width, Is.EqualTo (3));
		Assert.That (result.Height, Is.EqualTo (2));
		Assert.That (result.GetPixelData ()[0].BGRA, Is.EqualTo (ColorBgra.Blue.BGRA));
	}
}
