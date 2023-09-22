using System.Numerics;

namespace VoxelWorld.Voxel.System
{
    internal sealed class ModelTransformations
    {
        public Matrix4x4 Rotation = Matrix4x4.Identity;
        public Matrix4x4 RevRotation = Matrix4x4.Identity;
        public Vector3 Translation = new Vector3(0, 0, 0);
        public Vector3 RotatedLookDir = new Vector3(0, 0, 0);
        public Vector3 CameraPos = new Vector3(0, 0, 0);
        public float FOV = 0.0f;

        public void Update(PlayerCamera camera, float yAngle)
        {
            Rotation = Matrix4x4.CreateRotationY(yAngle);
            RevRotation = Matrix4x4.CreateRotationY(-yAngle);
            RotatedLookDir = Vector3.Transform(camera.LookDirection, Rotation);
            CameraPos = camera.CameraPos;
            FOV = camera.FieldOfView;
        }
    }
}
