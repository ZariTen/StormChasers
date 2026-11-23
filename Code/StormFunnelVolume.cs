using System;

/// <summary>Single client-local proxy for the depth-clipped condensation ray march.</summary>
internal static class StormFunnelVolume
{
	const string ShaderPath = "shaders/storm_funnel.shader";
	const int NoiseSize = 32;
	static Texture noise;

	internal static Material LoadMaterial()
	{
		var shader = Shader.Load( ShaderPath );
		return shader is { IsValid: true, IsError: false } ? Material.FromShader( shader ) : null;
	}

	internal static Texture Noise => noise ??= CreateNoise();

	static Texture CreateNoise()
	{
		var data = new byte[NoiseSize * NoiseSize * NoiseSize * 4];
		for ( var z = 0; z < NoiseSize; z++ )
		for ( var y = 0; y < NoiseSize; y++ )
		for ( var x = 0; x < NoiseSize; x++ )
		{
			var i = ((z * NoiseSize + y) * NoiseSize + x) * 4;
			data[i] = (byte)(255 * TileNoise( x, y, z, 4, 701 ));
			data[i + 1] = (byte)(255 * TileNoise( x, y, z, 8, 163 ));
			data[i + 2] = (byte)(255 * TileNoise( x, y, z, 16, 397 ));
			data[i + 3] = 255;
		}
		return Texture.CreateVolume( NoiseSize, NoiseSize, NoiseSize, ImageFormat.RGBA8888 )
			.WithName( "storm_chasers/funnel_density" ).WithData( data ).Finish();
	}

	static float TileNoise( int x, int y, int z, int cells, int seed )
	{
		var step = NoiseSize / cells;
		var a = x / step;
		var b = y / step;
		var c = z / step;
		var u = StormWeatherTextures.Smooth( (x % step) / (float)step );
		var v = StormWeatherTextures.Smooth( (y % step) / (float)step );
		var w = StormWeatherTextures.Smooth( (z % step) / (float)step );
		float Node( int i, int j, int k ) => StormSmokeTexture.Hash( i % cells + cells * (j % cells), k % cells, seed );
		float Layer( int k ) => Mix( Mix( Node( a, b, k ), Node( a + 1, b, k ), u ), Mix( Node( a, b + 1, k ), Node( a + 1, b + 1, k ), u ), v );
		return Mix( Layer( c ), Layer( c + 1 ), w );
	}

	static float Mix( float a, float b, float t ) => a + (b - a) * t;
}
