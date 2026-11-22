using System;

/// <summary>Shared soft-edged sprite masks made from deterministic multi-scale density noise.</summary>
internal static class StormSmokeTexture
{
	const int Resolution = 128;
	const int Variants = 4;

	static Sprite[] sprites;
	internal static Sprite[] Sprites => sprites ??= CreateSprites();

	static Sprite[] CreateSprites()
	{
		var result = new Sprite[Variants];
		for ( var variant = 0; variant < result.Length; variant++ )
		{
			var pixels = new byte[Resolution * Resolution * 4];
			var seed = 79 + variant * 127;
			for ( var y = 0; y < Resolution; y++ )
			for ( var x = 0; x < Resolution; x++ )
			{
				var u = (x + .5f) * 2 / Resolution - 1;
				var v = (y + .5f) * 2 / Resolution - 1;
				var warpX = (Noise( u * 2.5f, v * 2.5f, seed + 8 ) - .5f) * .52f;
				var warpY = (Noise( u * 2.5f + 20, v * 2.5f, seed + 19 ) - .5f) * .46f;
				var broad = Noise( (u + warpX) * 4.1f, (v + warpY) * 4.1f, seed );
				var medium = Noise( u * 9.4f + warpY, v * 9.4f, seed + 29 );
				var fine = Noise( u * 21f, v * 21f, seed + 43 );
				var detail = .58f * broad + .30f * medium + .12f * fine;
				var radius = MathF.Sqrt( u * u + v * v );
				var contour = Math.Clamp( (1.04f - radius + (detail - .52f) * .58f) / .66f, 0, 1 );
				var border = Math.Clamp( (1 - MathF.Max( MathF.Abs( u ), MathF.Abs( v ) )) * 7f, 0, 1 );
				var alpha = contour * contour * (3 - 2 * contour) * border * (.40f + .60f * detail);
				var shade = Math.Clamp( .56f + .32f * detail - .08f * v, 0, 1 );
				var offset = (y * Resolution + x) * 4;
				pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = (byte)(shade * 255);
				pixels[offset + 3] = (byte)(alpha * 255);
			}

			var texture = Texture.Create( Resolution, Resolution, ImageFormat.RGBA8888 )
				.WithName( $"storm_chasers/smoke_{variant}" ).WithData( pixels ).Finish();
			result[variant] = Sprite.FromTexture( texture );
		}
		return result;
	}

	internal static float Noise( float x, float y, int seed )
	{
		var ix = (int)MathF.Floor( x );
		var iy = (int)MathF.Floor( y );
		var a = x - ix;
		var b = y - iy;
		a = a * a * (3 - 2 * a);
		b = b * b * (3 - 2 * b);
		var south = Mix( Hash( ix, iy, seed ), Hash( ix + 1, iy, seed ), a );
		var north = Mix( Hash( ix, iy + 1, seed ), Hash( ix + 1, iy + 1, seed ), a );
		return Mix( south, north, b );
	}

	static float Mix( float a, float b, float t ) => a + (b - a) * t;

	internal static float Hash( int x, int y, int seed )
	{
		unchecked
		{
			var h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
			h = (h ^ (h >> 13)) * 1274126177u;
			return ((h ^ (h >> 16)) & 0x00ffffff) / 16777216f;
		}
	}
}
