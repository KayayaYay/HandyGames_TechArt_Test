using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace TechArtTest {
    [RequireComponent(typeof(VisualEffect)), ExecuteAlways]
    public class HologramMaterialReader : MonoBehaviour {
        [SerializeField] private VisualEffect hologrammEffect;
        public VisualEffect HologrammEffect => hologrammEffect;
        
        [SerializeField] private Transform rayOriginTransform;
        public Transform RayOriginTransform => rayOriginTransform;

        private void OnValidate() {
            if (hologrammEffect == null) {
                hologrammEffect = GetComponent<VisualEffect>();
            }
        }

        private void Update() {
            if (hologrammEffect == null) {
                return;
            }

            SkinnedMeshRenderer skinnedMeshRenderer = hologrammEffect.GetSkinnedMeshRenderer("SkinnedMeshRenderer");
            if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMaterial == null) {
                return;
            }

            Texture albedoTexture = skinnedMeshRenderer.sharedMaterial.GetTexture("_Albedo");
            if (albedoTexture != null) {
                hologrammEffect.SetTexture("SkinnedMeshAlbedoTexture", albedoTexture);
            }
            
            hologrammEffect.SetVector4("SkinnedMeshAlbedoTint", skinnedMeshRenderer.sharedMaterial.GetColor("_AlbedoTint"));
            if (rayOriginTransform != null) {
                hologrammEffect.SetVector3("RayOrigin", rayOriginTransform.position);
            }
        }
    }
}
