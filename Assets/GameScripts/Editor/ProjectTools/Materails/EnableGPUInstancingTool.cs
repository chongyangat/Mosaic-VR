using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GameMain.Editor.Assets.GameScripts.Editor.ProjectTools.Materails
{
    public class EnableGPUInstancingTool : MonoBehaviour
    {
        [MenuItem("Tools/VBSOED/材质/启用所选对象的所有材质的GPU Instancing")]
        static void EnableGPUInstancingOnSelected()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("No objects selected in Hierarchy.");
                return;
            }

            ToggleGPUInstancingOnMaterial(selectedObjects, true);
        }

        /// <summary>
        /// 禁用所有材质的GPU Instancing
        /// </summary>
        [MenuItem("Tools/VBSOED/材质/禁用所选对象的所有材质的GPU Instancing")]
        static void DisableGPUInstancingOnSelected()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("No objects selected in Hierarchy.");
                return;
            }

            ToggleGPUInstancingOnMaterial(selectedObjects, false);
        }

        private static void ToggleGPUInstancingOnMaterial(GameObject[] selectedObjects, bool enable)
        {
            int materialsProcessed = 0;
            int materialsAlreadyEnabled = 0;
            HashSet<Material> processedMaterials = new HashSet<Material>();

            foreach (GameObject obj in selectedObjects)
            {
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;

                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material mat = materials[i];
                        if (mat == null) continue;

                        // Skip if we've already processed this material
                        if (processedMaterials.Contains(mat)) continue;
                        processedMaterials.Add(mat);

                        if (mat.enableInstancing)
                        {
                            materialsAlreadyEnabled++;
                            continue;
                        }

                        // Enable GPU instancing
                        mat.enableInstancing = enable;
                        materialsProcessed++;

                        // Log which object and material was modified
                        if (enable)
                        {
                            Debug.Log($"Enabled GPU Instancing on: {obj.name} - Material: {mat.name}");
                        }
                        else
                        {
                            Debug.Log($"Disabled GPU Instancing on: {obj.name} - Material: {mat.name}");
                        }
                    }
                }
            }

            if (enable)
            {
                Debug.Log($"Enabled GPU Instancing on {materialsProcessed} materials. {materialsAlreadyEnabled} materials already had it enabled.");
            }
            else
            {
                Debug.Log($"Disabled GPU Instancing on {materialsProcessed} materials. {materialsAlreadyEnabled} materials already had it disabled.");
            }
        }
    }
}
