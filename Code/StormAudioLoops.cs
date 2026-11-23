using System;

/// <summary>Deterministic placeholder loops synthesized once per client, without imported samples.</summary>
internal static class StormAudioLoops
{
	internal enum Layer { Wind, Rain, CoveredRain, Roar }
	internal const int SampleRate = 24000;
	// Bump when the generated PCM changes. The engine keeps the first buffer registered under a name.
	internal const int Revision = 2;
	const int Frames = SampleRate * 12;
	const int Overlap = SampleRate * 2;
	static readonly SoundFile[] loops = new SoundFile[4];
	static int cachedRevision;

	internal static SoundFile Get( Layer layer )
	{
		if ( cachedRevision != Revision )
		{
			Array.Clear( loops );
			cachedRevision = Revision;
		}
		return loops[(int)layer] ??= SoundFile.FromPcm( $"storm_chasers/weather_v{Revision}_{layer}", Render( layer ),
			new SoundFile.PcmOptions { Rate = SampleRate, Channels = 1, Bits = 16, Loop = true } );
	}

	internal static byte[] Render( Layer layer )
	{
		var rain = layer is Layer.Rain or Layer.CoveredRain;
		var random = new Random( rain ? 1109 : layer == Layer.Wind ? 2161 : 4049 );
		var samples = new float[Frames + Overlap];
		var sub = new LowPass( 18 );
		var bass = new LowPass( 86 );
		var low = new LowPass( 270 );
		var body = new LowPass( 760 );
		var hiss = new LowPass( 5200 );
		var pressure = new LowPass( 255 );
		var rainBed = new LowPass( layer == Layer.CoveredRain ? 260 : 1100 );
		var rainSoftener = new LowPass( layer == Layer.CoveredRain ? 330 : 1450 );
		var windBed = new LowPass( 295 );
		var windSoftener = new LowPass( 380 );
		for ( var i = 0; i < samples.Length; i++ )
		{
			var white = (float)random.NextDouble() * 2 - 1;
			var t = (float)i / SampleRate;
			var bottom = sub.Step( white );
			var rumble = bass.Step( white ) - bottom;
			var breath = low.Step( white ) - bottom;
			var rush = body.Step( white );
			var spray = hiss.Step( white ) - rush;
			if ( rain )
			{
				// A soft bed of distant water, with individual impacts added below instead of chopped hiss.
				samples[i] = rainSoftener.Step( rainBed.Step( white ) - bottom ) * .24f;
			}
			else if ( layer == Layer.Wind )
			{
				var surge = .80f + .16f * MathF.Sin( t * .67f ) + .10f * MathF.Sin( t * 1.76f + 1.2f );
				var air = windSoftener.Step( windBed.Step( white ) - bottom );
				samples[i] = air * surge;
			}
			else
			{
				var turbulence = .93f + .14f * MathF.Sin( t * 5.2f + MathF.Sin( t * 1.17f ) ) + .17f * MathF.Sin( t * 2.31f );
				var growl = pressure.Step( white * (1 + .55f * MathF.Tanh( rumble * 13 )) ) - bottom;
				samples[i] = (rumble * 2.9f + growl * 1.23f + (rush - breath) * .59f + spray * .064f) * turbulence;
			}
		}

		if ( rain ) AddRainImpacts( samples, layer == Layer.CoveredRain );

		// Crossfade the end over the start, then remove DC and normalize by measured energy.
		double sum = 0, squared = 0;
		for ( var i = 0; i < Frames; i++ )
		{
			if ( i < Overlap )
			{
				var blend = StormWeatherTextures.Smooth( (float)i / Overlap );
				samples[i] = samples[Frames + i] * (1 - blend) + samples[i] * blend;
			}
			sum += samples[i];
			squared += samples[i] * samples[i];
		}
		var mean = (float)(sum / Frames);
		var rms = MathF.Sqrt( (float)(squared / Frames) - mean * mean );
		var target = layer switch { Layer.Roar => .205f, Layer.Rain => .108f, Layer.CoveredRain => .089f, _ => .083f };
		var gain = target / MathF.Max( rms, .001f );
		var pcm = new byte[Frames * 2];
		for ( var i = 0; i < Frames; i++ )
		{
			var sample = (short)(MathF.Tanh( (samples[i] - mean) * gain ) * 32767);
			pcm[i * 2] = (byte)sample;
			pcm[i * 2 + 1] = (byte)(sample >> 8);
		}
		return pcm;
	}

	static void AddRainImpacts( float[] samples, bool covered )
	{
		// Short, rounded resonances approximate drops striking a mix of wet surfaces.
		var random = new Random( covered ? 5041 : 3613 );
		var kernels = new float[12][];
		for ( var k = 0; k < kernels.Length; k++ )
		{
			var frequency = covered ? 155 + k * 24 : 390 + k * 82;
			var tau = (covered ? .009f : .005f) + .004f * (k % 4) / 3;
			var kernel = kernels[k] = new float[(int)(SampleRate * tau * 5)];
			for ( var i = 0; i < kernel.Length; i++ )
			{
				var t = (float)i / SampleRate;
				var tone = MathF.Sin( MathF.Tau * frequency * t );
				var sparkle = MathF.Sin( MathF.Tau * frequency * 1.57f * t + .3f );
				var attack = StormWeatherTextures.Smooth( t / .004f );
				var decay = MathF.Exp( -t / tau ) * StormWeatherTextures.Smooth( (kernel.Length - i) / (SampleRate * .004f) );
				kernel[i] = (tone + (covered ? .12f : .24f) * sparkle) * attack * decay;
			}
		}
		var eventsPerSecond = covered ? 43 : 168;
		for ( var start = 0; start < samples.Length; )
		{
			start += 1 + (int)(-Math.Log( 1 - random.NextDouble() ) * SampleRate / eventsPerSecond);
			var kernel = kernels[random.Next( kernels.Length )];
			var size = (float)random.NextDouble();
			var gain = (covered ? .021f : .016f) + (covered ? .115f : .090f) * size * size;
			if ( random.Next( 2 ) == 0 ) gain = -gain;
			for ( var i = 0; i < kernel.Length && start + i < samples.Length; i++ )
				samples[start + i] += gain * kernel[i];
		}
	}

	struct LowPass( float frequency )
	{
		readonly float coefficient = 1 - MathF.Exp( -MathF.Tau * frequency / SampleRate );
		float state;
		internal float Step( float input ) => state += (input - state) * coefficient;
	}
}
