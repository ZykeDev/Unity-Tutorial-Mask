Shader "UI/Noya/TutorialMask/Circle Hole Mask"
{
	Properties
	{
		[PerRendererData] _MainTex ("Texture", 2D) = "white" {}
		_Color ("Tint Color", Color) = (0,0,0,1)
		
		[HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
		[HideInInspector] _Stencil ("Stencil ID", Float) = 0
		[HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
		[HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
		[HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
		[HideInInspector] _ColorMask ("Color Mask", Float) = 15
		[HideInInspector] _ParentAspectRatio ("Parent Aspect Ratio", Float) = 1
		
		_HoleCenter ("Hole Center (Screen UV)", Vector) = (0.5, 0.5, 0, 0)
		_HoleRadius ("Hole Radius (Screen UV)", Float) = 0.1
		_FadeDistance ("Fade Distance", Float) = 0.01
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
		}

		// Standard UI blending setup
		Cull Off
		Lighting Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			// Used for masking operations (though we won't use Unity's Mask component)
			Stencil
			{
				Ref [_Stencil]
				Comp [_StencilComp]
				Pass [_StencilOp]
				ReadMask [_StencilReadMask]
				WriteMask [_StencilWriteMask]
			}
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile __ UNITY_UI_ALPHACLIP

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			fixed4 _Color;
			float4 _HoleCenter; // Normalized screen position of the hole center (0 to 1)
			float _HoleRadius;
			float _ParentAspectRatio;
			float _FadeDistance;
			float _Output;

			float smoothstep(float a, float b, float x)
			{
				// Normalize and clamp the input 'x' to the 0..1 range [a, b]
				float t = saturate((x - a) / (b - a));
				// Apply the smoothstep curve (3t^2 - 2t^3)
				return t * t * (3 - 2 * t);
			}
			
			v2f vert (appdata_t v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = v.texcoord;
				o.color = v.color * _Color;
				o.screenPos = ComputeScreenPos(o.vertex);

				return o;
			}
						
			fixed4 frag (v2f i) : SV_Target
			{
				// Get the current fragment's normalized screen position [0..1]
				float2 current_screen_uv = i.screenPos.xy / i.screenPos.w;
				
				// Calculate the offset from the hole center
				float2 offset = current_screen_uv - _HoleCenter.xy;

				// Because the hole starts in UV Space [0..1], it needs to be stretched to match the Aspect Ratio of the parent
				offset.x *= _ParentAspectRatio; 
				
				float inner_radius = _HoleRadius - _FadeDistance;

				// Use smoothstep to get a clean fade value around the border
				float opacity = smoothstep(inner_radius, _HoleRadius, length(offset));

				// Determine the output color/alpha
				fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;
				col.a *= opacity;

				return col;
			}
			ENDCG
		}
	}
}
