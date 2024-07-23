using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using UnityEngine.VFX;

[assembly: InternalsVisibleTo("TechArtTest.Editor")]
namespace TechArtTest {
    [RequireComponent(typeof(SplineContainer), typeof(VisualEffect))]
    public class WaterfallControls : MonoBehaviour {
        
        [SerializeField] private LayerMask waterCollisionLayer;
        public LayerMask WaterCollisionLayer => waterCollisionLayer;
        
        /// <summary>
        /// The width of the Waterfall
        /// </summary>
        [SerializeField] private float width = 1;
        public float HalfWidth => width * 0.5f;
        
        /// <summary>
        /// Waterfall Mesh width Iterations
        /// </summary>
        [SerializeField, Min(1)] private int iterationsX = 5;
        public int IterationsX => iterationsX;
        
        /// <summary>
        /// Waterfall Mesh Iterations along the Spline
        /// </summary>
        [SerializeField, Min(1)] private int iterationsY = 5;
        public int IterationsY => iterationsY;
        
        [SerializeField] private PointCloudAsset pointCloudAsset;
        public PointCloudAsset PointCloudAsset{
            get => pointCloudAsset;
            internal set => pointCloudAsset = value;
        }
        
        [SerializeField] private Mesh waterfallMesh;
        public Mesh WaterfallMesh {
            get => waterfallMesh;
            internal set => waterfallMesh = value;
        }
        
        [SerializeField] private Texture2D flowMap;
        public Texture2D FlowMap{
            get => flowMap;
            internal set => flowMap = value;
        }
        
        [FormerlySerializedAs("spline")] [SerializeField] private SplineContainer waterfallSpline;
        public SplineContainer WaterfallSpline => waterfallSpline;
        
        [SerializeField] private VisualEffect waterfallEffect;
        public VisualEffect WaterfallEffect => waterfallEffect;

        private void OnValidate() {
            if (waterfallSpline == null) {
                waterfallSpline = GetComponent<SplineContainer>();
            }

            if (waterfallEffect == null) {
                waterfallEffect = GetComponent<VisualEffect>();
            }
            
            SetVisualEffectProperties();
        }

        private void Start() {
            SetVisualEffectProperties();
        }
        
        private void SetVisualEffectProperties() {
            Spline spline = WaterfallSpline?.Spline;
            if (waterfallEffect == null || spline == null) {
                return;
            }

            Vector3 endCenter = spline.EvaluatePosition(1);
            Vector3 normalCenter = spline.EvaluatePosition(((float)spline.Count - 1) / spline.Count);
            waterfallEffect.SetVector3("BasePointA", endCenter + new Vector3(HalfWidth, 0, 0));
            waterfallEffect.SetVector3("BasePointB", endCenter - new Vector3(HalfWidth, 0, 0));
            waterfallEffect.SetVector3("BaseNormal", Vector3.Normalize(normalCenter - endCenter));
        }
    }
}

