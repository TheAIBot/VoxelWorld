using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelWorld
{
    internal class VoxelHierarchyInfo : IDisposable
    {
        private VoxelHierarchy VoxelHir = null;
        public bool IsEmpty = false;
        public bool HasBeenGenerated { get; private set; } = false;
        public bool IsBeingGenerated { get; private set; } = false;
        public bool IsHollow { get; private set; } = true;
        private AxisAlignedBoundingBox BoundingBox = null;
        private GridNormal Normal;

        private readonly object DisposeLock = new object();
        private bool HasBeenDisposed = false;

        public Action GenerateHierarchyAction(int gridSize, Vector3 gridCenter, float voxelSize, Func<Vector3, float> weightGen, int hirDepth, Matrix4 model_rot, Vector3 lookDir)
        {
            IsBeingGenerated = true;
            return () =>
            {
                Debug.Assert(VoxelHir == null);
                VoxelHir = new VoxelHierarchy(gridSize, gridCenter, voxelSize, weightGen, hirDepth + 1);
                VoxelHir.Generate(model_rot, lookDir);

                if (VoxelHir.IsEmpty())
                {
                    IsEmpty = true;
                    VoxelHir.Dispose();
                    VoxelHir = null;
                    IsBeingGenerated = false;
                    HasBeenGenerated = true;
                    return;
                }

                lock (DisposeLock)
                {
                    if (HasBeenDisposed)
                    {
                        VoxelHir?.Dispose();
                        VoxelHir = null;
                    }

                    if (VoxelHir != null)
                    {
                        BoundingBox = VoxelHir.BoundingBox;
                        Normal = VoxelHir.HirNormal;
                    }

                    IsBeingGenerated = false;
                    HasBeenGenerated = true;
                }
            };
        }

        public bool ShouldGenerate()
        {
            if (HasBeenGenerated)
            {
                return false;
            }

            if (IsBeingGenerated)
            {
                return false;
            }

            if (IsEmpty)
            {
                return false;
            }

            return true;
        }

        public bool CanSee(Frustum onScreenCheck, Matrix4 model_rot, Vector3 lookDir)
        {
            if (!HasBeenGenerated)
            {
                return false;
            }

            if (IsBeingGenerated)
            {
                return false;
            }

            if (IsEmpty)
            {
                return false;
            }

            if (!Normal.CanSee(model_rot, lookDir))
            {
                return false;
            }

            if (!onScreenCheck.Intersects(BoundingBox))
            {
                return false;
            }

            return true;
        }

        public void CheckAndIncreaseResolution(PlayerCamera camera, Frustum renderCheck)
        {
            IsHollow = false;
            VoxelHir.CheckAndIncreaseResolution(camera, renderCheck);
        }

        public void MakeHollow()
        {
            if (IsHollow)
            {
                return;
            }

            IsHollow = true;
            VoxelHir.MakeHollow();
        }

        public bool IsReadyToDraw()
        {
            return !IsBeingGenerated && VoxelHir != null;
        }

        public bool DrawMesh()
        {
            return VoxelHir.DrawMesh();
        }

        public bool DrawPoints()
        {
            return VoxelHir.DrawPoints();
        }

        public void Dispose()
        {
            lock (DisposeLock)
            {
                HasBeenDisposed = true;
                VoxelHir?.Dispose();
            }
        }
    }
}
