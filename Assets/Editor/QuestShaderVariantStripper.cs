#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Codex.Editor
{
    /// <summary>
    /// Removes URP variants for features that the Android/Quest renderer cannot
    /// enable. YooAsset builds shader bundles outside a player build, where URP's
    /// normal feature analysis otherwise keeps a very large cross-product of
    /// desktop and mobile keywords.
    /// </summary>
    internal sealed class QuestShaderVariantStripper : IPreprocessShaders
    {
        private static readonly string[] UnsupportedQuestKeywords =
        {
            // The Performant/VR URP assets disable additional lights and their shadows.
            "_ADDITIONAL_LIGHTS_VERTEX",
            "_ADDITIONAL_LIGHTS",
            "_ADDITIONAL_LIGHT_SHADOWS",
            "_CASTING_PUNCTUAL_LIGHT_SHADOW",

            // Quest uses one main-light shadow cascade and no screen-space shadows.
            "_MAIN_LIGHT_SHADOWS_CASCADE",
            "_MAIN_LIGHT_SHADOWS_SCREEN",
            "_SHADOWS_SOFT_LOW",
            "_SHADOWS_SOFT_HIGH",

            // Disabled by both Quest pipeline assets and the performant renderer.
            "_REFLECTION_PROBE_BLENDING",
            "_REFLECTION_PROBE_BOX_PROJECTION",
            "_SCREEN_SPACE_OCCLUSION",
            "_DBUFFER_MRT1",
            "_DBUFFER_MRT2",
            "_DBUFFER_MRT3",
            "_DECAL_NORMAL_BLEND_LOW",
            "_DECAL_NORMAL_BLEND_MEDIUM",
            "_DECAL_NORMAL_BLEND_HIGH",
            "_DECAL_LAYERS",
            "_WRITE_RENDERING_LAYERS",
            "_LIGHT_LAYERS",
            "_FORWARD_PLUS",
            "_GBUFFER_NORMALS_OCT",
            "LOD_FADE_CROSSFADE",
            "DEBUG_DISPLAY",
            "EDITOR_VISUALIZATION"
        };

        // Run after Unity/URP's built-in shader strippers.
        public int callbackOrder => 1000;

        public void OnProcessShader(
            Shader shader,
            ShaderSnippetData snippet,
            IList<ShaderCompilerData> variants)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android
                || variants.Count == 0)
            {
                return;
            }

            var keywords = new List<LocalKeyword>(UnsupportedQuestKeywords.Length);
            foreach (string keywordName in UnsupportedQuestKeywords)
            {
                LocalKeyword keyword = shader.keywordSpace.FindKeyword(keywordName);
                if (keyword.isValid)
                {
                    keywords.Add(keyword);
                }
            }

            if (keywords.Count == 0)
            {
                return;
            }

            int originalCount = variants.Count;
            int fallbackVariantIndex = -1;
            int fallbackUnsupportedKeywordCount = int.MaxValue;
            for (int variantIndex = 0; variantIndex < variants.Count; variantIndex++)
            {
                int unsupportedKeywordCount = CountEnabledKeywords(
                    variants[variantIndex].shaderKeywordSet,
                    keywords);
                if (unsupportedKeywordCount >= fallbackUnsupportedKeywordCount)
                {
                    continue;
                }

                fallbackVariantIndex = variantIndex;
                fallbackUnsupportedKeywordCount = unsupportedKeywordCount;
                if (unsupportedKeywordCount == 0)
                {
                    break;
                }
            }

            ShaderCompilerData fallbackVariant = variants[fallbackVariantIndex];
            for (int variantIndex = variants.Count - 1; variantIndex >= 0; variantIndex--)
            {
                ShaderKeywordSet keywordSet = variants[variantIndex].shaderKeywordSet;
                for (int keywordIndex = 0; keywordIndex < keywords.Count; keywordIndex++)
                {
                    if (!keywordSet.IsEnabled(keywords[keywordIndex]))
                    {
                        continue;
                    }

                    // Some third-party collections only contain a feature-on
                    // variant for a pass. Never turn that pass into a missing
                    // shader; preserve the least-featured input as a fallback.
                    if (variantIndex == fallbackVariantIndex
                        && fallbackUnsupportedKeywordCount > 0)
                    {
                        break;
                    }

                    variants.RemoveAt(variantIndex);
                    break;
                }
            }

            int strippedCount = originalCount - variants.Count;
            if (fallbackUnsupportedKeywordCount > 0)
            {
                var fallbackKeywords = new List<string>();
                ShaderKeywordSet fallbackKeywordSet = fallbackVariant.shaderKeywordSet;
                for (int keywordIndex = 0; keywordIndex < keywords.Count; keywordIndex++)
                {
                    if (fallbackKeywordSet.IsEnabled(keywords[keywordIndex]))
                    {
                        fallbackKeywords.Add(keywords[keywordIndex].name);
                    }
                }

                Debug.LogWarning(
                    $"[QuestShaderVariants] Preserved one fallback for "
                    + $"{shader.name}/{snippet.passName}; every input variant used "
                    + $"a disabled Quest feature. Fallback keywords: "
                    + $"{string.Join(", ", fallbackKeywords)}");
            }

            if (strippedCount > 0 && (originalCount >= 1000
                                     || shader.name == "Universal Render Pipeline/Lit"))
            {
                Debug.Log(
                    $"[QuestShaderVariants] {shader.name}/{snippet.passName}: "
                    + $"{originalCount} -> {variants.Count} "
                    + $"(removed {strippedCount} unsupported Quest variants).");
            }
        }

        private static int CountEnabledKeywords(
            ShaderKeywordSet keywordSet,
            List<LocalKeyword> keywords)
        {
            int count = 0;
            for (int keywordIndex = 0; keywordIndex < keywords.Count; keywordIndex++)
            {
                if (keywordSet.IsEnabled(keywords[keywordIndex]))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
#endif
