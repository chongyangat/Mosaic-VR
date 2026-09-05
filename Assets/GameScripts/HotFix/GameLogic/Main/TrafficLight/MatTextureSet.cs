using System;
using UnityEngine;

namespace VBSOED
{
    public class MatTextureSet : MonoBehaviour
    {
        [Serializable]
        public class TextureSetData 
        {
            public System.Collections.Generic.List<Data> data = new System.Collections.Generic.List<Data>
            {
                new Data() { property = "_BaseMap", texture = null },
                new Data() { property = "_MetallicGlossMap", texture = null },
                new Data() { property = "_BumpMap", texture = null },
                new Data() { property = "_EmissionMap", texture = null },
            };
        }

        [Serializable]
        public class Data 
        {
            public string property;
            public Texture2D texture;
        }

        [SerializeField] private MeshRenderer targetMesh;
        [SerializeField] private string targetMatName;

        private Material targetMat;

        public void Set(TextureSetData data) 
        {
            InitMat();
            if (targetMat == null) return;
            foreach (var itor in data.data) 
            {
                TrySetMap(targetMat, itor.texture, itor.property);
            }
        }

        private void InitMat() 
        {
            if (targetMat != null) return;
            var materials = targetMesh.materials;
            int count = materials != null ? materials.Length : 0;
            for (int i = 0; i < count; i++)
            {
                var itor = materials[i];
                if (itor != null && (string.IsNullOrEmpty(targetMatName) || itor.name.Contains(targetMatName)))
                {
                    targetMat = itor;
                    break;
                }
            }
        }

        private void TrySetMap(Material mat, Texture2D texture, string propertyName) 
        {
            if (texture == null || string.IsNullOrEmpty(propertyName))
                return;
            mat.SetTexture(propertyName, texture);
        }
    }
}
