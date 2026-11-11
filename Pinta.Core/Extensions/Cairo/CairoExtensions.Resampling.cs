using System;
using Cairo;

namespace Pinta.Core;

partial class CairoExtensions
{
	/// <summary>
	/// Resizes an image surface with a separable bicubic (Catmull-Rom) filter.
	/// The filter support is widened when downscaling so that the result does
	/// not alias. The surface data is premultiplied, which is what Argb32
	/// stores, so the filter is applied to the premultiplied channels.
	/// </summary>
	public static ImageSurface ResizeBicubic (ImageSurface source, Size new_size)
	{
		int source_width = source.Width;
		int source_height = source.Height;
		int dest_width = new_size.Width;
		int dest_height = new_size.Height;

		if (dest_width <= 0 || dest_height <= 0)
			throw new ArgumentOutOfRangeException (nameof (new_size), "The new size must be positive.");

		if (source_width == dest_width && source_height == dest_height) {
			ImageSurface copy = CreateImageSurface (Format.Argb32, dest_width, dest_height);
			using Context g = new (copy);
			g.SetSourceSurface (source, 0, 0);
			g.Paint ();
			return copy;
		}

		source.Flush ();

		ReadOnlySpan<ColorBgra> source_data = source.GetReadOnlyPixelData ();

		// Horizontal pass: (source_width x source_height) -> (dest_width x source_height).
		(int Start, float[] Weights)[] horizontal_weights = BuildWeights (source_width, dest_width);
		ColorBgra[] horizontal = new ColorBgra[dest_width * source_height];

		for (int y = 0; y < source_height; y++) {
			int source_row = y * source_width;
			int dest_row = y * dest_width;

			for (int x = 0; x < dest_width; x++) {
				(int start, float[] weights) = horizontal_weights[x];

				float b = 0, g = 0, r = 0, a = 0;

				for (int i = 0; i < weights.Length; i++) {
					int sx = Math.Clamp (start + i, 0, source_width - 1);
					ColorBgra colour = source_data[source_row + sx];
					float weight = weights[i];

					b += colour.B * weight;
					g += colour.G * weight;
					r += colour.R * weight;
					a += colour.A * weight;
				}

				horizontal[dest_row + x] = Premultiplied (b, g, r, a);
			}
		}

		// Vertical pass: (dest_width x source_height) -> (dest_width x dest_height).
		(int Start, float[] Weights)[] vertical_weights = BuildWeights (source_height, dest_height);
		ImageSurface dest = CreateImageSurface (Format.Argb32, dest_width, dest_height);
		Span<ColorBgra> dest_data = dest.GetPixelData ();

		for (int y = 0; y < dest_height; y++) {
			(int start, float[] weights) = vertical_weights[y];
			int dest_row = y * dest_width;

			for (int x = 0; x < dest_width; x++) {
				float b = 0, g = 0, r = 0, a = 0;

				for (int i = 0; i < weights.Length; i++) {
					int sy = Math.Clamp (start + i, 0, source_height - 1);
					ColorBgra colour = horizontal[sy * dest_width + x];
					float weight = weights[i];

					b += colour.B * weight;
					g += colour.G * weight;
					r += colour.R * weight;
					a += colour.A * weight;
				}

				dest_data[dest_row + x] = Premultiplied (b, g, r, a);
			}
		}

		dest.MarkDirty ();
		return dest;
	}

	/// <summary>
	/// Precomputes the normalized filter weights for every destination pixel.
	/// </summary>
	private static (int Start, float[] Weights)[] BuildWeights (int source_size, int dest_size)
	{
		double scale = (double) source_size / dest_size;
		double filter_scale = Math.Max (1.0, scale);
		double radius = 2.0 * filter_scale;

		(int Start, float[] Weights)[] result = new (int, float[])[dest_size];

		for (int i = 0; i < dest_size; i++) {
			// Pixel centres, so that the image stays centred when scaled.
			double centre = (i + 0.5) * scale - 0.5;

			int start = (int) Math.Floor (centre - radius);
			int end = (int) Math.Ceiling (centre + radius);

			float[] weights = new float[end - start + 1];
			double total = 0;

			for (int j = start; j <= end; j++) {
				double weight = CubicKernel ((j - centre) / filter_scale);
				weights[j - start] = (float) weight;
				total += weight;
			}

			if (Math.Abs (total) > double.Epsilon) {
				float inverse = (float) (1.0 / total);

				for (int j = 0; j < weights.Length; j++)
					weights[j] *= inverse;
			}

			result[i] = (start, weights);
		}

		return result;
	}

	/// <summary>
	/// Catmull-Rom cubic kernel (a = -0.5).
	/// </summary>
	private static double CubicKernel (double x)
	{
		x = Math.Abs (x);

		const double a = -0.5;

		if (x <= 1.0)
			return ((a + 2.0) * x - (a + 3.0)) * x * x + 1.0;

		if (x < 2.0)
			return (((x - 5.0) * x + 8.0) * x - 4.0) * a;

		return 0.0;
	}

	/// <summary>
	/// Builds a premultiplied colour, keeping the colour channels at or below
	/// the alpha channel as required by Cairo's Argb32 format.
	/// </summary>
	private static ColorBgra Premultiplied (float b, float g, float r, float a)
	{
		byte alpha = Utility.ClampToByte (Math.Round (a));

		return ColorBgra.FromBgra (
			Math.Min (Utility.ClampToByte (Math.Round (b)), alpha),
			Math.Min (Utility.ClampToByte (Math.Round (g)), alpha),
			Math.Min (Utility.ClampToByte (Math.Round (r)), alpha),
			alpha);
	}
}
