using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace TechArtTest.Editor {
    [CustomEditor(typeof(WaterfallControls))]
    internal sealed class WaterfallControlsEditor : UnityEditor.Editor{
        public override VisualElement CreateInspectorGUI() {
            VisualElement root = new();
            Button generateButton = new(GenerateWaterfallAssets);
            generateButton.text = "Generate Waterfall";

            PropertyField waterCollisionLayerField = new();
            waterCollisionLayerField.BindProperty(serializedObject.FindProperty("waterCollisionLayer"));
            
            PropertyField widthField = new();
            widthField.BindProperty(serializedObject.FindProperty("width"));
            
            PropertyField iterationsXField = new();
            iterationsXField.BindProperty(serializedObject.FindProperty("iterationsX"));
            
            PropertyField iterationsYField = new();
            iterationsYField.BindProperty(serializedObject.FindProperty("iterationsY"));

            PropertyField assetField = new();
            assetField.BindProperty(serializedObject.FindProperty("pointCloudAsset"));
            
            root.Add(waterCollisionLayerField);
            root.Add(widthField);
            root.Add(iterationsXField);
            root.Add(iterationsYField);
            root.Add(assetField);
            root.Add(generateButton);
            
            return root;
        }

        /// <summary>
        /// Generates all needed Assets for the Waterfall VFX.
        /// </summary>
        private void GenerateWaterfallAssets() {
            if (target is WaterfallControls controls) {
                string creationPath;
                if (controls.PointCloudAsset == null) {
                    creationPath = EditorUtility.SaveFilePanel("Create Point Cloud", Application.dataPath, "PointCloud", "pcache");
                    controls.PointCloudAsset = CreateInstance<PointCloudAsset>();
                    controls.PointCloudAsset.name = Path.GetFileNameWithoutExtension(creationPath);
                    string assetPath = Path.ChangeExtension($"Assets/{Path.GetRelativePath(Application.dataPath, creationPath)}", ".asset");
                    AssetDatabase.CreateAsset(controls.PointCloudAsset, assetPath);
                }
                else {
                    creationPath = Path.GetRelativePath(Application.dataPath, Path.GetDirectoryName(AssetDatabase.GetAssetPath(controls.PointCloudAsset)));
                }
                
                controls.PointCloudAsset.Points = GeneratePoints(controls);
                GenerateMesh(controls, AssetDatabase.GetAssetPath(controls.PointCloudAsset));
                GenerateFlowMap(controls, AssetDatabase.GetAssetPath(controls.PointCloudAsset));
                UpdateFile(controls.PointCloudAsset);
            }
        }

        /// <summary>
        /// Generates a Texture used to add Foam after the Waterfall collided with something
        /// </summary>
        /// <param name="path">The File path the Texture should be saved to. Automatically adds "_FlowMap" to the Filename</param>
        private void GenerateFlowMap(WaterfallControls controls, string path) {
            Spline spline = controls.WaterfallSpline.Spline;
            if (spline == null) {
                return;
            }
            
            int xIterations = controls.IterationsX;
            int yIterations = controls.IterationsY;

            Texture2D map;
            int width = xIterations * 2 + 1;
            int height = Mathf.FloorToInt(width * spline.CalculateLength(controls.transform.localToWorldMatrix) / Mathf.Max(1, controls.HalfWidth));
            if (controls.FlowMap != null) {
                map = controls.FlowMap;
                if (map.width != width || map.height != height) {
                    map.Reinitialize(width, height);
                }
            }
            else {
                map = new(width, height) {
                    name = $"{Path.GetFileNameWithoutExtension(path)}_FlowMap"
                };
                controls.FlowMap = map;
                Debug.Log($"{Path.GetDirectoryName(path)}/{controls.FlowMap.name}.asset");
                AssetDatabase.CreateAsset(controls.FlowMap, $"{Path.GetDirectoryName(path)}/{controls.FlowMap.name}.asset");
            }
            
            Color[] pixels = Enumerable.Repeat(new Color(0.5f, 0.5f, 0, 1), width * height).ToArray();
            map.SetPixels(pixels);
            map.Apply();
            
            
            for (int y = 0; y < spline.Count * yIterations; y++) {
                float t = (float)y / (spline.Count * yIterations);
                float tn = (float)(y+1) / (spline.Count * yIterations);
                for (int x = -xIterations; x <= xIterations; x++) {
                    float deltaX = (float)x / xIterations;
                    Vector3 origin = controls.transform.TransformPoint(spline.EvaluatePosition(t));
                    Vector3 direction = (Vector3)controls.transform.TransformPoint(spline.EvaluatePosition(tn)) - origin;
                    Ray ray = new(origin + controls.transform.right * deltaX * controls.HalfWidth, direction.normalized);
                    if (Physics.Raycast(ray, out RaycastHit hit, direction.magnitude, controls.WaterCollisionLayer)) {
                        float uvX = (float)(x + xIterations) / width;
                        float stepYDelta = 1.0f / (spline.Count * yIterations);
                        Vector2 hitTextureUV = new(uvX, (hit.distance / direction.magnitude) * stepYDelta + t);
                        int pixY = Mathf.FloorToInt(hitTextureUV.y * height);
                        int pixX = Mathf.FloorToInt(hitTextureUV.x * width);
                        map.SetPixel(pixX, pixY, new Color(0.5f,0.5f,1,1));
                    }
                }
            }
            map.Apply();

            // Unoptimized Flow Map Calculation due to lack of time (it only runs in Editor)
            Color[] flowPix = map.GetPixels(0, 0, width, height);
            int flowRangeDown = 15 * controls.IterationsY;
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    Color pix = flowPix[x + y * width];
                    if (pix.b > 0.5f) {
                        for (int i = 0; i < flowRangeDown; i++) {
                            int pY = y + i;
                            
                            if (pY < height) {
                                map.SetPixel(x,pY, new Color(pix.r, Mathf.Max(pix.g, 0.5f + (1.0f - (float)i / flowRangeDown)*0.5f), Mathf.Max(pix.g, 0.5f + (1.0f - (float)i / flowRangeDown)*0.5f), 1));
                            }
                        }
                    }
                }
            }
            map.Apply();
        }

        /// <summary>
        /// Generates a List of Points along the Waterfall where it hits Colliders
        /// </summary>
        private List<Point> GeneratePoints(WaterfallControls controls) {
            Spline spline = controls.WaterfallSpline.Spline;
            if (spline == null) {
                return null;
            }

            int xIterations = controls.IterationsX;
            int yIterations = controls.IterationsY;
            List<Point> points = new();
            for (int y = 0; y < spline.Count * yIterations; y++) {
                float t = (float)y / (spline.Count * yIterations);
                float tn = (float)(y+1) / (spline.Count * yIterations);
                for (int x = -xIterations; x <= xIterations; x++) {
                    float deltaX = (float)x / xIterations;
                    Vector3 origin = controls.transform.TransformPoint(spline.EvaluatePosition(t));
                    Vector3 direction = (Vector3)controls.transform.TransformPoint(spline.EvaluatePosition(tn)) - origin;
                    Ray ray = new(origin + controls.transform.right * deltaX * controls.HalfWidth, direction.normalized);
                    if (Physics.Raycast(ray, out RaycastHit hit, direction.magnitude, controls.WaterCollisionLayer)) {
                        Vector3 normal = controls.transform.InverseTransformDirection(Quaternion.AngleAxis(-90, controls.transform.right) * Vector3.ProjectOnPlane(direction, controls.transform.right).normalized);
                        points.Add(new(controls.transform.InverseTransformPoint(hit.point), 
                            controls.transform.InverseTransformDirection(hit.normal), 
                            controls.transform.InverseTransformDirection(normal)));
                    }
                }
            }
            // Randomize Points
            points = points.OrderBy(x => Random.Range(0, points.Count)).ToList();
            return points;
        }

        /// <summary>
        /// Generates the Mesh of the Waterfall along the Spline
        /// </summary>
        /// <param name="path">The File path the Mesh should be saved to. Automatically adds "_Mesh" to the Filename</param>
        private void GenerateMesh(WaterfallControls controls, string path) {
            Spline spline = controls.WaterfallSpline.Spline;
            if (spline == null) {
                return;
            }

            Mesh mesh;
            if (controls.WaterfallMesh != null) {
                mesh = controls.WaterfallMesh;
            }
            else {
                mesh = new() {
                    name = $"{Path.GetFileNameWithoutExtension(path)}_Mesh"
                };
                controls.WaterfallMesh = mesh;
                Debug.Log($"{Path.GetDirectoryName(path)}/{controls.WaterfallMesh.name}.asset");
                AssetDatabase.CreateAsset(controls.WaterfallMesh, $"{Path.GetDirectoryName(path)}/{controls.WaterfallMesh.name}.asset");
            }
            
            int xIterations = controls.IterationsX;
            int yIterations = controls.IterationsY;
            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<Vector2> uv0 = new();
            List<Vector2> uv1_Curvature_Length = new();
            List<Vector2> uv2_Width = new();
            List<int> indices = new();
            float length = spline.CalculateLength(controls.transform.localToWorldMatrix);
            int vertexCountX = (xIterations * 2 + 1);
            for (int y = 0; y <= spline.Count * yIterations; y++) {
                float t = (float)y / (spline.Count * yIterations);
                for (int x = -xIterations; x <= xIterations; x++) {
                    float deltaX = (float)x / xIterations;
                    Vector3 origin = controls.transform.TransformPoint(spline.EvaluatePosition(t));
                    Vector3 vertex = controls.transform.InverseTransformPoint(origin + controls.transform.right * deltaX * controls.HalfWidth);
                    vertices.Add(vertex);
                    
                    // Calculate Normal
                    Vector3 direction;
                    if (y >= spline.Count) {
                        float tn = (float)(y-1) / (spline.Count * yIterations);
                        direction = origin - (Vector3)controls.transform.TransformPoint(spline.EvaluatePosition(tn));
                    }
                    else {
                        float tn = (float)(y+1) / (spline.Count * yIterations);
                        direction = (Vector3)controls.transform.TransformPoint(spline.EvaluatePosition(tn)) - origin;
                    }
                    Vector3 normal = controls.transform.InverseTransformDirection(Quaternion.AngleAxis(-90, controls.transform.right) * Vector3.ProjectOnPlane(direction, controls.transform.right).normalized).normalized;
                    normals.Add(normal);
                    
                    uv0.Add(new((deltaX + 1) * 0.5f, t));
                    uv1_Curvature_Length.Add(new((y == 0 || y == spline.Count * yIterations) ? 0 : spline.EvaluateCurvature(t), length - length * t));
                    uv2_Width.Add(new((deltaX + 1) * 0.5f * controls.HalfWidth, t));
                    
                    if (x < xIterations && y < spline.Count * yIterations) {
                        indices.Add(xIterations + x + y * vertexCountX);
                        indices.Add(xIterations + x + (y+1) * vertexCountX);
                        indices.Add(xIterations + x+1 + (y+1) * vertexCountX);
                        indices.Add(xIterations + x+1+ + y * vertexCountX);
                    }
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1_Curvature_Length);
            mesh.SetUVs(2, uv2_Width);
            mesh.SetIndices(indices, MeshTopology.Quads, 0);
            mesh.RecalculateNormals();
        }
        
        /// <summary>
        /// Writes a pcache File based on the given <see cref="PointCloudAsset"/>
        /// </summary>
        /// <param name="asset">The <see cref="PointCloudAsset"/> the File should be created from.</param>
        public void UpdateFile(PointCloudAsset asset) {
            if (asset == null) {
                Debug.LogError("Asset is null");
                return;
            }

            string fullPath;
            string relativePath;
            if (asset.AssetFile == null) {
                relativePath = AssetDatabase.GetAssetPath(asset);
                fullPath = Path.ChangeExtension($"{Application.dataPath}/{Path.GetRelativePath(Application.dataPath, relativePath)}", "pcache");
            }
            else {
                relativePath = AssetDatabase.GetAssetPath(asset.AssetFile);
                fullPath = Path.ChangeExtension($"{Application.dataPath}/{Path.GetRelativePath(Application.dataPath, relativePath)}", "pcache");
            }
            
            using (StreamWriter writer = new StreamWriter(fullPath, false)) {
                writer.WriteLine("pcache\nformat ascii 1.0\ncomment Exported with WaterfallControlsEditor.cs");
                writer.WriteLine($"elements {asset.Points.Count}");
                writer.WriteLine("property float position.x");
                writer.WriteLine("property float position.y");
                writer.WriteLine("property float position.z");
                writer.WriteLine("property float normal.x");
                writer.WriteLine("property float normal.y");
                writer.WriteLine("property float normal.z");
                writer.WriteLine("property float forward.x");
                writer.WriteLine("property float forward.y");
                writer.WriteLine("property float forward.z");
                writer.WriteLine("end_header");
                CultureInfo cultureInfo = CultureInfo.CreateSpecificCulture("en-GB");
                foreach (Point point in asset.Points) {
                    writer.WriteLine($"{point.Position.x.ToString(cultureInfo)} {point.Position.y.ToString(cultureInfo)} {point.Position.z.ToString(cultureInfo)} " +
                                     $"{point.Normal.x.ToString(cultureInfo)} {point.Normal.y.ToString(cultureInfo)} {point.Normal.z.ToString(cultureInfo)} " +
                                     $"{point.Forward.x.ToString(cultureInfo)} {point.Forward.y.ToString(cultureInfo)} {point.Forward.z.ToString(cultureInfo)} ");
                }
                writer.Flush();
            }

            string relativePCachePath = Path.ChangeExtension(relativePath, "pcache");
            AssetDatabase.ImportAsset(relativePCachePath);
            asset.AssetFile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(relativePCachePath);
            EditorUtility.SetDirty(asset);
        }
    }
}