using Silk.NET.Input;
using System;
using System.Numerics;

namespace VoxelWorld
{
    internal sealed class PlayerCamera
    {
        private int WindowWidth;
        private int WindowHeight;

        public Vector3 CameraPos;
        public Vector3 LookDirection;
        public Vector3 UpVector;
        private Vector2 PrevMousePos;
        private Vector2 CameraAngles;
        private bool PrevMouseDown = false;

        public float FieldOfView = 45.0f * (MathF.PI / 180.0f);

        public Matrix4x4 Perspective
        {
            get
            {
                return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, (float)WindowWidth / WindowHeight, 0.1f, 100f);
            }
        }

        public Matrix4x4 View
        {
            get
            {
                return Matrix4x4.CreateLookAt(CameraPos, CameraPos + LookDirection, UpVector);
            }
        }

        public PlayerCamera(int width, int height, Vector3 cameraPos)
        {
            this.WindowWidth = width;
            this.WindowHeight = height;

            this.CameraPos = cameraPos;
            this.LookDirection = -Vector3.Normalize(CameraPos);//new Vector3(MathF.Cos(0) * MathF.Cos(0) - 3, MathF.Sin(0) * MathF.Cos(0), MathF.Sin(0));
            this.UpVector = new Vector3(0, 1, 0);
            this.PrevMousePos = new Vector2(0, 0);

            float yAngle = MathF.Asin(LookDirection.Y);
            float xAngle = MathF.Acos(LookDirection.X / MathF.Cos(yAngle));
            this.CameraAngles = new Vector2(xAngle, yAngle);
        }

        public void MoveLeft() => CameraPos += new Vector3(-0.01f, 0, 0);
        public void MoveRight() => CameraPos += new Vector3(0.01f, 0, 0);
        public void MoveForward() => CameraPos += 0.08f * LookDirection;
        public void MoveBackward() => CameraPos += -0.08f * LookDirection;

        public void UpdateCameraDirectionMouseDown(IMouse mouse, MouseButton mouseButton)
        {
            if (mouseButton != MouseButton.Left)
            {
                return;
            }

            PrevMouseDown = true;
            PrevMousePos = new Vector2(mouse.Position.X, mouse.Position.Y);
        }

        public void UpdateCameraDirectionMouseUp(IMouse mouse, MouseButton mouseButton)
        {
            if (mouseButton != MouseButton.Left)
            {
                return;
            }

            UpdateCameraDirectionMouseMove(mouse);

            PrevMouseDown = false;
        }

        public void UpdateCameraDirectionMouseMove(IMouse mouse)
        {
            if (!PrevMouseDown)
            {
                return;
            }

            float dx = 0.6f * MathF.PI * (PrevMousePos.X - mouse.Position.X) / WindowWidth;
            float dy = 0.3f * MathF.PI * (PrevMousePos.Y - mouse.Position.Y) / WindowHeight;
            CameraAngles += new Vector2(dx, -dy);
            LookDirection = new Vector3(MathF.Cos(CameraAngles.X) * MathF.Cos(CameraAngles.Y), MathF.Sin(CameraAngles.Y), MathF.Sin(CameraAngles.X) * MathF.Cos(CameraAngles.Y));


            PrevMousePos = new Vector2(mouse.Position.X, mouse.Position.Y);
        }

        public bool UpdateCameraDimensions(int width, int height)
        {
            if (WindowWidth != width || WindowHeight != height)
            {
                this.WindowWidth = width;
                this.WindowHeight = height;
                return true;
            }
            return false;
        }
    }
}
