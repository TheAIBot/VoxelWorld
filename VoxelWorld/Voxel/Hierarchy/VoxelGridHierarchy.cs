using OpenGL;
using System;
using System.Numerics;

namespace VoxelWorld
{
    internal class VoxelGridHierarchy : IDisposable
    {
        private VoxelGridInfo Grid;
        private VoxelHierarchyInfo Hierarchy;

        public VoxelGridHierarchy(Vector3 center, int gridSize, float voxelSize)
        {
            this.Grid = new VoxelGridInfo(center);
            this.Hierarchy = new VoxelHierarchyInfo(center, gridSize, voxelSize);
        }

        public void GenerateGrid(VoxelSystemData genData, VoxelGrid grid, GridPos gridPos)
        {
            Grid.Generate(genData, this, grid, gridPos);
        }

        public void EndGeneratingGrid(VoxelSystemData genData, VoxelGrid grid, GridPos gridPos)
        {
            Grid.EndGenerating(genData, this, grid, gridPos);
        }

        public void EndGeneratingHierarchy(VoxelSystemData genData, VoxelGrid grid, GridPos gridPos)
        {
            Hierarchy.EndGenerating(genData, this, grid, gridPos);
        }

        private bool IsHighEnoughResolution(Vector3 voxelCenter, ModelTransformations modelTrans, VoxelSystemData genData)
        {
            Vector3 a = modelTrans.Translation + (modelTrans.RevRotation * voxelCenter);
            Vector3 c = modelTrans.CameraPos;

            float distance = (a - c).Length();
            distance = MathF.Pow(distance, 1.2f);

            float spaceLength = MathF.Tan(modelTrans.FOV) * distance * 2.0f;

            return genData.VoxelSize / spaceLength < 0.0015f;
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans, VoxelSystemData genData, in GridPos gridPos)
        {
            if (Grid.IsBeingGenerated)
            {
                return;
            }
            if (Hierarchy.GenStatus == GenerationStatus.Generating)
            {
                return;
            }

            //This right here actually sucks. Basically i have no way
            //of making sure that the hierarchy knows if it must be
            //generated, so instead i just check every time
            if (Hierarchy.IsEmpty && genData.IsMustGenerate(in gridPos))
            {
                MarkMustGenerate();
            }

            if (!Hierarchy.IsHollow && !Hierarchy.CanSee(renderCheck, modelTrans))
            {
                Hierarchy.MakeHollow(this);
                return;
            }

            if (IsHighEnoughResolution(Grid.GridCenter, modelTrans, genData))
            {
                if (Grid.CanSee(renderCheck, modelTrans))
                {
                    if (Grid.IsReadyToDraw())
                    {
                        if (!Hierarchy.IsHollow)
                        {
                            Hierarchy.MakeHollow(this);
                        }
                    }
                    else
                    {
                        if (Grid.ShouldGenerate())
                        {
                            Grid.StartGenerating(genData, this, in gridPos);
                        }
                    }
                }
                else
                {
                    Grid.MakeHollow(this);
                }
            }
            else
            {
                if (Hierarchy.CanSee(renderCheck, modelTrans))
                {
                    if (Hierarchy.ShouldGenerate(this))
                    {
                        Hierarchy.StartGenerating(genData.GetWithHalfVoxelSize(), this, in gridPos);
                    }
                    else
                    {
                        Grid.MakeHollow(this);
                        Hierarchy.CheckAndIncreaseResolution(renderCheck, modelTrans, genData, in gridPos);
                    }
                }
            }
        }

        public void MakeHollow()
        {
            Grid.MakeHollow(this);
            Hierarchy.MakeHollow(this);
        }

        public void MarkMustGenerate()
        {
            Hierarchy.MarkMustGenerate();
        }

        public bool IsEmpty()
        {
            if (Hierarchy.IgnoreIsEmpty)
            {
                return false;
            }

            return Grid.IsEmpty;
        }

        public BoundingCircle GetBoundingCircle()
        {
            return Grid.BoundingBox;
        }

        public void Dispose()
        {
            Grid.Dispose(this);
            Hierarchy.Dispose(this);
        }
    }
}
