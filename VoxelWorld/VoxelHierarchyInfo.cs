using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelWorld
{
    internal class VoxelHierarchyInfo : IDisposable
    {
        private Vector3 Center;
        private VoxelHierarchy VoxelHir = null;
        public bool IsEmpty = false;
        public bool HasBeenGenerated { get; private set; } = false;
        public bool IsBeingGenerated { get; private set; } = false;
        public bool IsHollow { get; private set; } = true;
        private float BoundingCircleRadius;
        private GridNormal Normal;

        private readonly object DisposeLock = new object();
        private bool HasBeenDisposed = false;

        public VoxelHierarchyInfo(Vector3 center, int gridSize, float voxelSize)
        {
            this.Center = center;
            this.BoundingCircleRadius = (gridSize / 2) * voxelSize;
        }

        public Action GenerateHierarchyAction(Vector3 gridCenter, VoxelSystemData GenData, Vector3 rotatedLookDir)
        {
            IsBeingGenerated = true;
            IsHollow = false;
            return () =>
            {
                if (IsHollow)
                {
                    IsBeingGenerated = false;
                    return;
                }


                VoxelHierarchy hir = new VoxelHierarchy(gridCenter, GenData);
                var hirData = hir.Generate(Center, rotatedLookDir);
                BoundingCircleRadius = hirData.Item1.Radius;
                Normal = hirData.Item2;

                if (hir.IsEmpty())
                {
                    IsEmpty = true;
                    hir.Dispose();
                    IsBeingGenerated = false;
                    HasBeenGenerated = true;
                    return;
                }

                lock (DisposeLock)
                {
                    if (HasBeenDisposed)
                    {
                        hir?.Dispose();
                        hir = null;
                    }
                    else
                    {
                        if (IsHollow)
                        {
                            hir.MakeHollow();
                        }

                        Debug.Assert(VoxelHir == null);
                        VoxelHir = hir;
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

        public bool CanSee(Frustum onScreenCheck, ModelTransformations modelTrans)
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

            if (!Normal.CanSee(modelTrans.RotatedLookDir))
            {
                return false;
            }

            Vector3 newCenter = modelTrans.RevRotation * Center + modelTrans.Translation;
            if (!onScreenCheck.Intersects(new BoundingCircle(newCenter, BoundingCircleRadius)))
            {
                return false;
            }

            return true;
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans)
        {
            IsHollow = false;
            VoxelHir.CheckAndIncreaseResolution(renderCheck, modelTrans);
        }

        public void MakeHollow()
        {
            if (IsHollow)
            {
                return;
            }

            lock (DisposeLock)
            {
                IsHollow = true;

                VoxelHir?.MakeHollow();
            }
        }

        public bool IsReadyToDraw()
        {
            return !IsBeingGenerated && VoxelHir != null && !IsHollow;
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
