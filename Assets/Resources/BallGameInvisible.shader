// 何も描かないシェーダー。
// 3D のクリスマスツリーに最初から付いている飾りと電球を見えなくするために使います。
// 頂点を画面の外へ出し、色も深度も書かないので、影も含めて何も表示されません。
Shader "BallGame/Invisible"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            ColorMask 0
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                // 画面（クリップ空間）の外に置いて描かれないようにする
                return float4(2.0, 2.0, 2.0, 1.0);
            }

            half4 Frag() : SV_Target
            {
                return half4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
