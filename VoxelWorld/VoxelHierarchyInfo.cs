﻿using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelWorld
{
    internal enum GenerationStatus : byte
    {
        NotGenerated,
        Generating,
        HasBeenGenerated
    }

    internal class VoxelHierarchyInfo : IDisposable
    {
        private Vector3 Center;
        private VoxelHierarchy VoxelHir = null;
        public bool IsEmpty = false;
        public GenerationStatus GenStatus { get; private set; } = GenerationStatus.NotGenerated;
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
            Debug.Assert(GenStatus == GenerationStatus.NotGenerated);
            Debug.Assert(VoxelHir == null);

            GenStatus = GenerationStatus.Generating;
            IsHollow = false;
            return () =>
            {
                if (IsHollow)
                {
                    GenStatus = GenerationStatus.NotGenerated;
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
                    GenStatus = GenerationStatus.HasBeenGenerated;
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

                    GenStatus = GenerationStatus.HasBeenGenerated;
                }
            };
        }

        public bool ShouldGenerate()
        {
            if (GenStatus != GenerationStatus.NotGenerated)
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
            if (IsEmpty)
            {
                return false;
            }

            if (GenStatus == GenerationStatus.HasBeenGenerated && !Normal.CanSee(modelTrans.RotatedLookDir))
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
