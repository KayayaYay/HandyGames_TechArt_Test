using UnityEngine;

namespace TechArtTest {
    public sealed class Point {
        [SerializeField] private Vector3 position;
        public Vector3 Position{
            get => position;
            set => position = value;
        }
        
        [SerializeField] private Vector3 normal;
        public Vector3 Normal{
            get => normal;
            set => normal = value;
        }
        
        [SerializeField] private Vector3 forward;
        public Vector3 Forward{
            get => forward;
            set => forward = value;
        }

        public Point(Vector3 position, Vector3 normal, Vector3 forward) {
            this.position = position;
            this.normal = normal;
            this.forward = forward;
        }
    }
}