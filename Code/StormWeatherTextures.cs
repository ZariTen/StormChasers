using System;

/// <summary>Procedural textures for broad cloud strata and individual rain streaks.</summary>
internal static class StormWeatherTextures
{
	internal static Sprite CloudSheet( int seed, bool storm, bool eye = false, bool blanket = false )
	{
		const int Size = 512;
		var data = new byte[Size * Size * 4];
		for ( var y = 0; y < Size; y++ )
		for ( var x = 0; x < Size; x++ )
		{
			var u = (x + .5f) / Size * 2 - 1;
			var v = (y + .5f) / Size * 2 - 1;
			var bend = StormSmokeTexture.Noise( u * 2.1f, v * 2.8f, seed + 7 ) - .5f;
			var drift = StormSmokeTexture.Noise( u * 3.7f, v * 2.2f, seed + 33 ) - .5f;
			var f = storm ? 1f : 2.8f;
			var px = f * (u * 3.1f + bend * .9f);
			var py = f * (v * 4.6f + drift * 1.5f + .44f * u * u);
			var broad = StormSmokeTexture.Noise( px, py, seed );
			var cells = StormSmokeTexture.Noise( px * 2.32f + 4, py * 2.17f, seed + 43 );
			var ripples = StormSmokeTexture.Noise( px * 5.9f, py * 5.8f + 15, seed + 74 );
			var tips = StormSmokeTexture.Noise( px * 14.9f, py * 13.4f, seed + 129 );
			var density = .51f * broad + .30f * cells + .14f * ripples + .05f * tips;
			var spread = MathF.Sqrt( u * u + v * v );
			var bounds = Smooth( (1 - MathF.Max( MathF.Abs( u ), MathF.Abs( v ) )) * (storm ? 11 : 5) );
			var profile = storm ? Smooth( (1.06f - spread + (broad - .5f) * .33f + bend * .22f) / .37f ) : bounds;
			var alpha = bounds * profile * (storm ? (.51f + .49f * Smooth( (density - .24f) * 2.4f )) : Smooth( (density - .13f) * 2.48f ));
			// Broad decks vary mainly in brightness, not transparency. Keep the sky from shining through every gap.
			if ( blanket ) alpha = bounds * (.83f + .17f * Smooth( (density - .20f) * 2.3f ));
			if ( eye ) alpha *= Smooth( (spread - .17f) / .22f );
			// Contrast at several scales, without repeating a puff silhouette.
			var shade = Math.Clamp( .20f + .86f * density + .25f * MathF.Abs( cells - broad ) + .09f * ripples, 0, 1 );
			var offset = (y * Size + x) * 4;
			data[offset] = data[offset + 1] = data[offset + 2] = (byte)(shade * 255);
			data[offset + 3] = (byte)(alpha * 255);
		}
		return Upload( $"weather_cloud_{seed}{(eye ? "_eye" : blanket ? "_blanket" : "")}", Size, Size, data );
	}

	internal static Sprite RainDrop()
	{
		const int Width = 8, Height = 64;
		var data = new byte[Width * Height * 4];
		for ( var y = 0; y < Height; y++ )
		for ( var x = 0; x < Width; x++ )
		{
			var u = (x + .5f) / Width;
			var v = (y + .5f) / Height;
			var gleam = Smooth( MathF.Min( u, 1 - u ) * 2 );
			var tail = Smooth( v / .64f ) * Smooth( (1 - v) / .12f );
			var offset = (y * Width + x) * 4;
			data[offset] = data[offset + 1] = data[offset + 2] = 255;
			data[offset + 3] = (byte)(255 * gleam * gleam * tail);
		}
		return Upload( "weather_rain_drop", Width, Height, data );
	}

	static Sprite Upload( string name, int width, int height, byte[] data ) => Sprite.FromTexture(
		Texture.Create( width, height, ImageFormat.RGBA8888 ).WithName( $"storm_chasers/{name}" ).WithData( data ).Finish() );

	internal static float Smooth( float v ) { v = Math.Clamp( v, 0, 1 ); return v * v * (3 - 2 * v); }
}
