using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallGame.Editor
{
    /// <summary>
    /// アセットストアの Polyart 素材のうち、URP では描けずにピンク色になるマテリアルを、URP のシェーダーに置き換えます。
    /// 古い描画方式（Built-in）専用のシェーダー（Standard、パーティクル用、Built-in 専用の Shader Graph など）や、
    /// 見つからないシェーダーを使っているマテリアルが対象です。
    /// エディターを開いたときに一度だけ自動で調べます。メニュー「BallGame/Polyart のピンクのマテリアルを直す」でも実行できます。
    /// </summary>
    [InitializeOnLoad]
    public static class PolyartUrpMaterialFixer
    {
        /// <summary>調べるフォルダ。</summary>
        public const string TargetFolder = "Assets/Polyart";

        private const string SessionKey = "BallGame.PolyartUrpMaterialFixer.Checked";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";
        private const string ErrorShaderName = "Hidden/InternalErrorShader";

        private static readonly string[] UrpLightModes = { "SRPDefaultUnlit", "UniversalForward", "UniversalForwardOnly", "Universal2D" };
        private static readonly string[] MainTextureNames = { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_BaseTexture", "_ColorMap", "_Texture" };
        private static readonly string[] ColorNames = { "_BaseColor", "_Color", "_TintColor", "_MainColor", "_Tint" };
        private static readonly string[] AdditiveHints = { "Additive", "Glow", "Spark" };

        static PolyartUrpMaterialFixer()
        {
            EditorApplication.delayCall += RunOncePerSession;
        }

        private static void RunOncePerSession()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            if (AssetDatabase.IsValidFolder(TargetFolder))
            {
                FixAll();
            }
        }

        /// <summary>
        /// メニューから実行します。
        /// </summary>
        [MenuItem("BallGame/Polyart のピンクのマテリアルを直す")]
        public static void FixFromMenu()
        {
            if (!AssetDatabase.IsValidFolder(TargetFolder))
            {
                EditorUtility.DisplayDialog("Polyart", TargetFolder + " が見つかりません。", "OK");
                return;
            }

            int count = FixAll();
            EditorUtility.DisplayDialog("Polyart", count == 0 ? "直すマテリアルはありませんでした。" : count + " 個のマテリアルを URP 用に直しました。", "OK");
        }

        /// <summary>
        /// フォルダ内のピンクになるマテリアルをすべて直します。
        /// </summary>
        /// <returns>直したマテリアルの数</returns>
        public static int FixAll()
        {
            List<string> fixedPaths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { TargetFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && NeedsFix(material) && TryConvert(material, path))
                {
                    EditorUtility.SetDirty(material);
                    fixedPaths.Add(path);
                }
            }

            if (fixedPaths.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[BallGame] URP で描けないマテリアルを " + fixedPaths.Count + " 個直しました。\n" + string.Join("\n", fixedPaths));
            }

            return fixedPaths.Count;
        }

        /// <summary>
        /// URP で描けない（ピンクになる）マテリアルかどうかを返します。
        /// </summary>
        /// <param name="material">マテリアル</param>
        /// <returns>直す必要があれば true</returns>
        public static bool NeedsFix(Material material)
        {
            Shader shader = material.shader;
            if (shader == null || shader.name == ErrorShaderName)
            {
                return true;
            }

            // 空（スカイボックス）は描き方が別なので対象外
            if (shader.name.StartsWith("Skybox", StringComparison.Ordinal))
            {
                return false;
            }

            if (ShaderUtil.ShaderHasError(shader))
            {
                return true;
            }

            return !HasUrpPass(shader);
        }

        private static bool HasUrpPass(Shader shader)
        {
            ShaderData data = ShaderUtil.GetShaderData(shader);
            if (data == null)
            {
                return true;
            }

            ShaderTagId pipelineTag = new ShaderTagId("RenderPipeline");
            ShaderTagId lightModeTag = new ShaderTagId("LightMode");
            for (int i = 0; i < data.SubshaderCount; i++)
            {
                ShaderData.Subshader subshader = data.GetSubshader(i);
                string pipeline = subshader.FindTagValue(pipelineTag).name;
                if (!string.IsNullOrEmpty(pipeline) && pipeline != "UniversalPipeline")
                {
                    continue;
                }

                for (int j = 0; j < subshader.PassCount; j++)
                {
                    if (IsUrpLightMode(subshader.GetPass(j).FindTagValue(lightModeTag).name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsUrpLightMode(string lightMode)
        {
            return string.IsNullOrEmpty(lightMode) || Array.IndexOf(UrpLightModes, lightMode) >= 0;
        }

        private static bool TryConvert(Material material, string path)
        {
            string oldShaderName = material.shader != null ? material.shader.name : string.Empty;
            Texture mainTexture = FindMainTexture(material);
            Color color = FindColor(material);
            bool particle = oldShaderName.IndexOf("Particle", StringComparison.Ordinal) >= 0
                || path.IndexOf("/Effects/", StringComparison.Ordinal) >= 0
                || material.renderQueue >= (int)RenderQueue.AlphaTest + 200;
            Shader target = Shader.Find(particle ? ParticleShaderName : LitShaderName);
            if (target == null)
            {
                Debug.LogWarning("[BallGame] URP のシェーダーが見つからないため直せませんでした：" + path);
                return false;
            }

            int oldQueue = material.renderQueue;
            material.shader = target;
            if (mainTexture != null)
            {
                material.SetTexture("_BaseMap", mainTexture);
                material.SetTexture("_MainTex", mainTexture);
            }

            material.SetColor("_BaseColor", color);
            if (particle)
            {
                SetupTransparent(material, IsAdditive(material.name, oldShaderName));
            }
            else if (oldQueue >= (int)RenderQueue.AlphaTest && oldQueue < (int)RenderQueue.GeometryLast)
            {
                SetupCutout(material);
            }

            return true;
        }

        private static bool IsAdditive(string materialName, string oldShaderName)
        {
            foreach (string hint in AdditiveHints)
            {
                if (materialName.IndexOf(hint, StringComparison.Ordinal) >= 0 || oldShaderName.IndexOf(hint, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static Texture FindMainTexture(Material material)
        {
            // シェーダーが壊れていても値が残っているよう、保存された値から読む
            SerializedProperty textures = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
            Texture fallback = null;
            for (int i = 0; textures != null && i < textures.arraySize; i++)
            {
                SerializedProperty entry = textures.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("first").stringValue;
                Texture texture = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                if (texture == null)
                {
                    continue;
                }

                if (Array.IndexOf(MainTextureNames, name) >= 0)
                {
                    return texture;
                }

                if (fallback == null && !IsHelperTextureName(name))
                {
                    fallback = texture;
                }
            }

            return fallback;
        }

        private static bool IsHelperTextureName(string name)
        {
            string[] helpers = { "Normal", "Bump", "Mask", "Noise", "Detail", "Occlusion", "Metallic", "Emission", "Height" };
            foreach (string helper in helpers)
            {
                if (name.IndexOf(helper, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static Color FindColor(Material material)
        {
            SerializedProperty colors = new SerializedObject(material).FindProperty("m_SavedProperties.m_Colors");
            foreach (string wanted in ColorNames)
            {
                for (int i = 0; colors != null && i < colors.arraySize; i++)
                {
                    SerializedProperty entry = colors.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("first").stringValue != wanted)
                    {
                        continue;
                    }

                    Color color = entry.FindPropertyRelative("second").colorValue;
                    // 古いパーティクルの _TintColor は 0.5 が「そのまま」の意味なので 2 倍にする
                    return wanted == "_TintColor" ? Clamp01(color * 2f) : color;
                }
            }

            return Color.white;
        }

        private static Color Clamp01(Color color)
        {
            return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));
        }

        private static void SetupTransparent(Material material, bool additive)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetupCutout(Material material)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
    }
}
