HEADER
{
	Description = "Storm Chasers: backlit airborne fragments";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
}

COMMON
{
	#define CUSTOM_MATERIAL_INPUTS
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"
	PixelInput MainVs( VertexInput v )
	{
		return FinalizeVertex( ProcessVertex( v ) );
	}
}

PS
{
	#include "common/pixel.hlsl"
	float3 FragmentColor < Attribute( "FragmentColor" ); Default3( .035, .031, .025 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		#if S_MODE_DEPTH
			return 0;
		#else
			float3 position = i.vPositionWithOffsetWs + g_vHighPrecisionLightingOffsetWs.xyz;
			float sky = saturate( dot( normalize( i.vNormalWs ), float3( -.63, .34, .69 ) ) );
			// Keep dark silhouettes legible through rain without removing distance haze.
			return float4( Fog::Apply( position, i.vPositionSs.xy, FragmentColor * (.69 + .52 * sky), .42 ), 1 );
		#endif
	}
}
