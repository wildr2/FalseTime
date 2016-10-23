// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Sprites/WorldTextured"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_OverTex("Overlay Texture", 2D) = "white" {}
		_Scale("Texture Scale", Float) = 1.0
		_Color("Tint", Color) = (1,1,1,1)
		[MaterialToggle] PixelSnap("Pixel snap", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		Blend One OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#pragma multi_compile _ PIXELSNAP_ON
			#pragma multi_compile _ ETC1_EXTERNAL_ALPHA
			#include "UnityCG.cginc"

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex    : SV_POSITION;
				fixed4 color	 : COLOR;
				float2 texcoord  : TEXCOORD0;
				float3 wpos		 : TEXCOORD1;
			};

			fixed4 _Color;

			v2f vert(appdata_t IN)
			{
				v2f OUT;

				OUT.wpos = mul(unity_ObjectToWorld, IN.vertex).xyz;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.texcoord = IN.texcoord;
				OUT.color = IN.color * _Color;
#ifdef PIXELSNAP_ON
				OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif

				return OUT;
			}

			sampler2D _MainTex;
			sampler2D _OverTex;
			sampler2D _AlphaTex;
			float _Scale;

			fixed4 SampleSpriteTexture(float2 uv, float3 wpos)
			{
				fixed4 color = tex2D(_MainTex, uv);

				float2 wpos_uv = wpos.xy;
				wpos_uv[0] = abs(wpos_uv[0]) % 1;
				wpos_uv[1] = abs(wpos_uv[1]) % 1;
				color *= tex2D(_OverTex, wpos_uv * _Scale);

#if ETC1_EXTERNAL_ALPHA
				// get the color from an external texture (usecase: Alpha support for ETC1 on android)
				color.a = tex2D(_AlphaTex, uv).r;
#endif //ETC1_EXTERNAL_ALPHA

				return color;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				fixed4 c = SampleSpriteTexture(IN.texcoord, IN.wpos) * IN.color;
				c.rgb *= c.a;
				return c;
			}
			ENDCG
		}
	}
}
