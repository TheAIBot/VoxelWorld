using OpenGL;
using System;
using System.Numerics;

namespace VoxelWorld
{
    internal class VoxelGridHierarchy : IDisposable
    {
        public VoxelGridInfo Grid;
        public VoxelHierarchyInfo Hierarchy;

        public VoxelGridHierarchy(Vector3 center, int gridSize, float voxelSize)
        {
            this.Grid = new VoxelGridInfo(center);
            this.Hierarchy = new VoxelHierarchyInfo(center, gridSize, voxelSize);
        }

        public void GenerateGrid(VoxelSystemData genData, VoxelGrid grid)
        {
            Grid.Generate(genData, this, grid);
        }

        public void EndGeneratingGrid(VoxelSystemData genData, VoxelGrid grid)
        {
            Grid.EndGenerating(genData, this, grid);
        }

        public void EndGeneratingHierarchy(VoxelSystemData genData, VoxelGrid grid)
        {
            Hierarchy.EndGenerating(genData, this, grid);
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

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans, VoxelSystemData genData)
        {
            if (Grid.IsBeingGenerated)
            {
                return;
            }
            if (Hierarchy.GenStatus == GenerationStatus.Generating)
            {
                return;
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
                            Grid.StartGenerating(genData, this);
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
                    if (Hierarchy.ShouldGenerate())
                    {
                        Hierarchy.StartGenerating(genData.GetOneDown(), this);
                    }
                    else
                    {
                        Grid.MakeHollow(this);
                        Hierarchy.CheckAndIncreaseResolution(renderCheck, modelTrans, genData);
                    }
                }
            }
        }

        public void MakeHollow()
        {
            Grid.MakeHollow(this);
            Hierarchy.MakeHollow(this);
        }

        public void Dispose()
        {
            Grid.Dispose(this);
            Hierarchy.Dispose(this);
        }
    }
}
