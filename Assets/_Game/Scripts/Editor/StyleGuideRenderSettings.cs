using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EvilSanta.EditorTools
{
    /// <summary>
    /// Applies the Art Style Guide's "Project and URP asset" values to every URP asset
    /// used by a quality level. Assets named Mobile/Low get a lighter preset sized for
    /// Switch-class hardware (render scale 0.75, MSAA 2x, 1024 shadows, no SSAO).
    /// Uses serialized field names so it works even where URP has no public setter;
    /// anything it can't find is reported in the Console instead of failing silently.
    /// </summary>
    public static class StyleGuideRenderSettings
    {
        [MenuItem("Evil Santa/3. Apply Style Guide Render Settings", priority = 20)]
        public static void Apply()
        {
            var log = new StringBuilder("[Evil Santa] Style guide render settings\n");

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                log.AppendLine("  Color space -> Linear");
            }

            var assets = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main) assets.Add(main);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q) assets.Add(q);

            if (assets.Count == 0)
            {
                Debug.LogError("[Evil Santa] No URP asset found. Create the project from the Universal 3D template.");
                return;
            }

            foreach (var asset in assets)
            {
                string n = asset.name.ToLowerInvariant();
                bool low = n.Contains("mobile") || n.Contains("low") || n.Contains("switch");
                ApplyTo(asset, low, log);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static void ApplyTo(UniversalRenderPipelineAsset asset, bool low, StringBuilder log)
        {
            log.AppendLine($"  {asset.name} ({(low ? "low / Switch-sized preset" : "PC preset")})");
            var so = new SerializedObject(asset);

            SetBool(so, "m_SupportsHDR", true, log);
            SetInt(so, "m_MSAA", low ? 2 : 4, log);
            SetFloat(so, "m_RenderScale", low ? 0.75f : 1f, log);
            SetBool(so, "m_MainLightShadowsSupported", true, log);
            SetInt(so, "m_MainLightShadowmapResolution", low ? 1024 : 2048, log);
            SetFloat(so, "m_ShadowDistance", low ? 40f : 60f, log);
            SetInt(so, "m_ShadowCascadeCount", 2, log);
            SetFloat(so, "m_Cascade2Split", 0.25f, log);
            SetBool(so, "m_SoftShadowsSupported", true, log);
            SetInt(so, "m_SoftShadowQuality", 2, log, optional: true); // Medium
            SetInt(so, "m_AdditionalLightsRenderingMode", 1, log);     // Per Pixel
            SetInt(so, "m_AdditionalLightsPerObjectLimit", low ? 4 : 8, log);
            SetBool(so, "m_AdditionalLightShadowsSupported", !low, log);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            // SSAO lives on the renderer. Its class is internal to URP, so match it by name.
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            bool foundSsao = false;
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is not ScriptableRendererData data) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    foundSsao = true;
                    feature.SetActive(!low);
                    var fso = new SerializedObject(feature);
                    SetFloat(fso, "m_Settings.Intensity", 0.6f, log);
                    SetFloat(fso, "m_Settings.Radius", 0.3f, log);
                    SetFloat(fso, "m_Settings.DirectLightingStrength", 0.25f, log);
                    fso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(feature);
                    EditorUtility.SetDirty(data);
                    log.AppendLine($"    SSAO on {data.name}: {(low ? "off" : "on, 0.6 / 0.3 / 0.25")}");
                }
            }
            if (!foundSsao && !low)
                log.AppendLine("    ! No SSAO feature on this renderer. Add 'Screen Space Ambient Occlusion' to its Renderer Features by hand.");
        }

        static SerializedProperty Find(SerializedObject so, string path, StringBuilder log, bool optional)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null && !optional) log.AppendLine($"    ! {path} not found (URP version changed?). Set it by hand.");
            return p;
        }

        static void SetBool(SerializedObject so, string path, bool v, StringBuilder log, bool optional = false)
        { var p = Find(so, path, log, optional); if (p != null) p.boolValue = v; }

        static void SetInt(SerializedObject so, string path, int v, StringBuilder log, bool optional = false)
        { var p = Find(so, path, log, optional); if (p != null) p.intValue = v; }

        static void SetFloat(SerializedObject so, string path, float v, StringBuilder log, bool optional = false)
        { var p = Find(so, path, log, optional); if (p != null) p.floatValue = v; }
    }
}
