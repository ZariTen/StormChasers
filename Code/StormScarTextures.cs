using System;

/// <summary>Shared dirt and scuffed-asphalt decal textures, authored procedurally.</summary>
internal static class StormScarTextures
{
	const int Size = 128;
	static readonly DecalDefinition[] definitions = new DecalDefinition[6];

	internal static DecalDefinition Get( bool road, int variant )
	{
		var index = variant + (road ? 3 : 0);
		return definitions[index] ??= Create( road, variant );
	}

	static DecalDefinition Create( bool road, int variant )
	{
		var seed = 665 + variant * 283;
		var pixels = new byte[Size * Size * 4];
		for ( var y = 0; y < Size; y++ )
		for ( var x = 0; x < Size; x++ )
		{
			var u = (x + .5f) / Size * 2 - 1;
			var v = (y + .5f) / Size * 2 - 1;
			float Noise( float x, float y, int offset ) => StormSmokeTexture.Noise( x, y, seed + offset );
			var lumps = Noise( u * 4.1f, v * 3.1f, 4 );
			var grains = Noise( u * 28, v * 32, 82 );
			var fibers = Noise( u * 16 + .5f * v, v * 4.5f, 15 );
			var bend = (Noise( 2.3f * v, .4f, 20 ) - .5f) * .42f;
			var radius = MathF.Sqrt( (u - bend) * (u - bend) * 1.58f + v * v * .88f );
			var edge = 1 - StormWeatherTextures.Smooth( (radius - .32f - .37f * (lumps - .5f) - .16f * (fibers - .5f)) / .56f );
			edge *= StormWeatherTextures.Smooth( (1 - MathF.Max( MathF.Abs( u ), MathF.Abs( v ) )) * 12 );
			float r, g, b, opacity;
			if ( road )
			{
				var crack = 0f;
				for ( var k = 0; k < 2; k++ )
				{
					var px = (k == 0 ? -.28f : .36f) + v * (k == 0 ? .48f : -.39f);
					px += .18f * (Noise( v * 12, 1.7f * k, 127 ) - .5f) + .12f * (Noise( v * 5, k, 73 ) - .5f);
					var branch = MathF.Abs( u - px + MathF.Max( 0, v + .29f - .4f * k ) * (k == 0 ? .65f : -.54f) );
					var main = 1 - StormWeatherTextures.Smooth( (MathF.Abs( u - px ) - .007f) / .021f );
					var fork = (1 - StormWeatherTextures.Smooth( (branch - .003f) / .014f )) * StormWeatherTextures.Smooth( (v + .29f - .4f * k) * 9 );
					crack = MathF.Max( crack, MathF.Max( main, fork ) );
				}
				var grit = .27f + .15f * grains + .08f * lumps;
				r = grit * (1 - crack * .84f );
				g = r * .97f;
				b = r * .91f;
				opacity = edge * MathF.Max( crack, .19f + .40f * fibers );
			}
			else
			{
				var soil = .19f + .19f * lumps + .115f * grains + .06f * fibers;
				r = soil;
				g = soil * (.69f + fibers * .12f);
				b = soil * (.49f + fibers * .10f);
				opacity = edge * (.67f + .33f * StormWeatherTextures.Smooth( (lumps - .28f) * 2.8f ));
			}
			var i = (y * Size + x) * 4;
			pixels[i] = Byte( r );
			pixels[i + 1] = Byte( g );
			pixels[i + 2] = Byte( b );
			pixels[i + 3] = Byte( opacity );
		}
		var texture = Texture.Create( Size, Size, ImageFormat.RGBA8888 )
			.WithName( $"storm_chasers/scars/{(road ? "asphalt" : "soil")}_{variant}" ).WithData( pixels ).Finish();
		return new DecalDefinition { ColorTexture = texture, Width = 1, Height = 1, ColorMix = 1, CoverageAmount = 1, Tint = Color.White };
	}

	static byte Byte( float v ) => (byte)(Math.Clamp( v, 0, 1 ) * 255);
}
