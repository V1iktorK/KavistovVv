using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.Linq;
using System.IO;

/// <summary>
/// Конвертирует встроенные Standard-материалы в Robot.fbx и ScaraRobot.fbx
/// в отдельные HDRP/Lit материалы.
/// НЕ трогает материалы ангара (ROOMS/HQ Hangar Free).
/// </summary>
public class ConvertRobotMaterialsToHDRP : EditorWindow
{
    private static readonly string[] TARGET_FBX_PATHS = new string[]
    {
        "Assets/Robots/Robot.fbx",
        "Assets/Robots/ScaraRobot.fbx"
    };

    [MenuItem("Tools/Robots/Convert Materials to HDRP/Lit")]
    public static void ShowWindow()
    {
        GetWindow<ConvertRobotMaterialsToHDRP>("Convert Robot Materials");
    }

    [MenuItem("Tools/Robots/Convert Materials to HDRP/Lit", true)]
    public static bool ValidateConvert()
    {
        // FBX загружается как GameObject, поэтому проверяем тип Object.
        return TARGET_FBX_PATHS.All(path => AssetDatabase.LoadAssetAtPath<Object>(path) != null);
    }

    [ContextMenu("Convert Embedded Materials to HDRP/Lit")]
    public static void Convert()
    {
        int convertedCount = 0;
        int skippedCount = 0;

        foreach (string fbxPath in TARGET_FBX_PATHS)
        {
            GameObject fbxGO = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxGO == null)
            {
                Debug.LogWarning($"[ConvertRobotMaterials] FBX not found: {fbxPath}");
                skippedCount++;
                continue;
            }

            MeshFilter[] meshFilters = fbxGO.GetComponentsInChildren<MeshFilter>();
            SkinnedMeshRenderer[] skinnedRenderers = fbxGO.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                // Материалы хранятся на Renderer, а не на Mesh.
                Renderer renderer = mf.GetComponent<Renderer>();
                if (renderer == null) continue;
                Material[] mats = renderer.sharedMaterials;
                Material[] newMats = new Material[mats.Length];
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    newMats[i] = mats[i];
                    if (mats[i] != null && IsStandardOrUnlit(mats[i]))
                    {
                        Material newMat = ConvertToHDRPLit(mats[i], fbxPath, i);
                        if (newMat != null)
                        {
                            newMats[i] = newMat;
                            changed = true;
                            convertedCount++;
                        }
                    }
                }
                if (changed)
                {
                    renderer.sharedMaterials = newMats;
                }
            }

            foreach (var smr in skinnedRenderers)
            {
                if (smr.sharedMesh == null) continue;
                // SkinnedMeshRenderer — это Renderer, материалы лежат на нём.
                Material[] mats = smr.sharedMaterials;
                Material[] newMats = new Material[mats.Length];
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    newMats[i] = mats[i];
                    if (mats[i] != null && IsStandardOrUnlit(mats[i]))
                    {
                        Material newMat = ConvertToHDRPLit(mats[i], fbxPath, i);
                        if (newMat != null)
                        {
                            newMats[i] = newMat;
                            changed = true;
                            convertedCount++;
                        }
                    }
                }
                if (changed)
                {
                    smr.sharedMaterials = newMats;
                }
            }

            // Update FBX meta to separate materials on next import
            UpdateFBXMetaForMaterialSeparation(fbxPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ConvertRobotMaterials] Conversion complete. Converted: {convertedCount}, Skipped: {skippedCount}");
        EditorUtility.DisplayDialog("Conversion Complete",
            $"Converted {convertedCount} material(s) to HDRP/Lit.\nSkipped: {skippedCount} FBX file(s).",
            "OK");
    }

    private static bool IsStandardOrUnlit(Material mat)
    {
        if (mat == null) return false;
        string shaderName = mat.shader.name;
        // Check for Standard, Legacy Shaders, or any non-HDRP shader
        return shaderName.Contains("Standard")
            || shaderName.Contains("Legacy")
            || shaderName.Contains("Diffuse")
            || shaderName.Contains("Bumped")
            || shaderName.Contains("Parallax")
            || shaderName.Contains("Specular")
            || (!shaderName.Contains("HDRP") && !shaderName.Contains("High Definition"));
    }

    private static Material ConvertToHDRPLit(Material oldMat, string fbxPath, int matIndex)
    {
        if (oldMat == null) return null;

        string assetDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(oldMat));
        if (assetDir == null || assetDir.Length == 0)
            assetDir = Path.Combine("Assets", "Robots", "ConvertedMaterials");

        string newMatName = Path.GetFileName(fbxPath).Replace(".fbx", "") + "_Mat" + matIndex;
        if ((oldMat.name == null || oldMat.name.Length == 0) || oldMat.name == "No Name")
            newMatName = Path.GetFileName(fbxPath).Replace(".fbx", "") + "_Mat" + matIndex;
        else
            newMatName = oldMat.name + "_HDRP";

        string matPath = Path.Combine(assetDir, newMatName + ".mat");
        if (File.Exists(matPath))
        {
            Debug.LogWarning($"[ConvertRobotMaterials] Material already exists: {matPath}");
            return AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        // Create new HDRP/Lit material
        Shader hdrpLitShader = Shader.Find("HDRP/Lit");
        if (hdrpLitShader == null)
        {
            Debug.LogError("[ConvertRobotMaterials] HDRP/Lit shader not found! Make sure HDRP is installed.");
            return null;
        }

        Material newMat = new Material(hdrpLitShader);
        newMat.name = newMatName;

        // Copy properties from old material to new HDRP/Lit material
        // Base color: у Standard шейдера свойство называется _Color, у HDRP/Lit — _BaseColor
        if (oldMat.HasProperty("_BaseColor"))
        {
            newMat.SetColor("_BaseColor", oldMat.GetColor("_BaseColor"));
        }
        else if (oldMat.HasProperty("_Color"))
        {
            newMat.SetColor("_BaseColor", oldMat.GetColor("_Color"));
        }
        else
        {
            newMat.SetColor("_BaseColor", Color.white);
        }

        // Metallic
        if (oldMat.HasProperty("_MetallicGlossMap") && oldMat.GetTexture("_MetallicGlossMap") != null)
        {
            newMat.SetTexture("_MetallicGlossMap", oldMat.GetTexture("_MetallicGlossMap"));
        }
        newMat.SetFloat("_Metallic", oldMat.HasProperty("_Metallic") ? oldMat.GetFloat("_Metallic") : 0f);
        newMat.SetFloat("_Smoothness", oldMat.HasProperty("_Smoothness") ? oldMat.GetFloat("_Smoothness") : 0.5f);
        if (oldMat.HasProperty("_Glossiness"))
            newMat.SetFloat("_Smoothness", oldMat.GetFloat("_Glossiness"));

        // Normal map
        if (oldMat.HasProperty("_BumpMap") && oldMat.GetTexture("_BumpMap") != null)
        {
            newMat.SetTexture("_NormalMap", oldMat.GetTexture("_BumpMap"));
        }
        if (oldMat.HasProperty("_NormalScale"))
            newMat.SetFloat("_NormalScale", oldMat.GetFloat("_NormalScale"));
        if (oldMat.HasProperty("_BumpScale"))
            newMat.SetFloat("_NormalScale", oldMat.GetFloat("_BumpScale"));

        // Albedo/Color map
        if (oldMat.HasProperty("_MainTex") && oldMat.GetTexture("_MainTex") != null)
        {
            newMat.SetTexture("_BaseColorMap", oldMat.GetTexture("_MainTex"));
        }
        if (oldMat.HasProperty("_BaseColorMap") && oldMat.GetTexture("_BaseColorMap") != null)
        {
            newMat.SetTexture("_BaseColorMap", oldMat.GetTexture("_BaseColorMap"));
        }

        // Occlusion map
        if (oldMat.HasProperty("_OcclusionMap") && oldMat.GetTexture("_OcclusionMap") != null)
        {
            newMat.SetTexture("_MaskMap", oldMat.GetTexture("_OcclusionMap"));
        }

        // Emission
        if (oldMat.HasProperty("_EmissionColor") && oldMat.GetColor("_EmissionColor") != Color.black)
        {
            newMat.SetColor("_EmissiveColor", oldMat.GetColor("_EmissionColor"));
            newMat.EnableKeyword("_ENABLE_EMISSION");
        }

        // Alpha cutoff
        if (oldMat.HasProperty("_Cutoff"))
            newMat.SetFloat("_AlphaCutoff", oldMat.GetFloat("_Cutoff"));

        // Double-sided
        if (oldMat.doubleSidedGI)
        {
            newMat.SetFloat("_DoubleSidedEnable", 1f);
        }

        // Material ID (for HDRP post-processing)
        newMat.SetFloat("_MaterialID", 1f);

        // Save the new material
        AssetDatabase.CreateAsset(newMat, matPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ConvertRobotMaterials] Created HDRP/Lit material: {matPath}");
        return newMat;
    }

    private static void UpdateFBXMetaForMaterialSeparation(string fbxPath)
    {
        string metaPath = fbxPath + ".meta";
        if (!File.Exists(metaPath)) return;

        string metaContent = File.ReadAllText(metaPath);

        // Change materialImportMode from 0 (embedded) to 2 (separate)
        // and materialLocation from 0 (embedded) to 1 (separate)
        bool modified = false;

        if (metaContent.Contains("materialImportMode: 0"))
        {
            metaContent = metaContent.Replace("materialImportMode: 0", "materialImportMode: 2");
            modified = true;
            Debug.Log($"[ConvertRobotMaterials] Updated materialImportMode -> 2 in {metaPath}");
        }

        if (metaContent.Contains("materialLocation: 0"))
        {
            metaContent = metaContent.Replace("materialLocation: 0", "materialLocation: 1");
            modified = true;
            Debug.Log($"[ConvertRobotMaterials] Updated materialLocation -> 1 in {metaPath}");
        }

        if (modified)
        {
            File.WriteAllText(metaPath, metaContent);
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
