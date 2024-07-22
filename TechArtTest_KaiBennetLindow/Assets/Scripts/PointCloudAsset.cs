using System.Collections.Generic;
using UnityEngine;

namespace TechArtTest {
    public sealed class PointCloudAsset : ScriptableObject{
        [SerializeField] private List<Point> points = new();

        public List<Point> Points {
            get => points;
            internal set => points = value;
        }

        [SerializeField] private ScriptableObject assetFile;
        public ScriptableObject AssetFile{
            get => assetFile;
            internal set => assetFile = value;
        }
    }
}