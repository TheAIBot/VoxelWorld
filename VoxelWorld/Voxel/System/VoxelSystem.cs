﻿using System.Numerics;
using VoxelWorld.ShapeGenerators;
using VoxelWorld.Voxel.Grid;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Voxel.System
{
    internal sealed class VoxelSystem
    {
        private VoxelHierarchy Grid = null;
        private readonly Vector3 Center;
        internal VoxelSystemData FirstLevelSystemData;
        private readonly ModelTransformations ModelTrans = new ModelTransformations();

        public Matrix4x4 Model { get { return ModelTrans.Rotation; } }

        public VoxelSystem(int gridSize, Vector3 center, float voxelSize, PlanetGen generator)
        {
            Center = center;
            FirstLevelSystemData = new VoxelSystemData(gridSize, voxelSize, generator);
        }

        /// <summary>
        /// Initializes the system with a root grid hierarchy with a voxel size
        /// that encompasses the whole model in the root grid hierarchy.
        /// </summary>
        public void TestResizeToFindFirstGrid()
        {
            VoxelGrid vGrid = new VoxelGrid(Center, FirstLevelSystemData);
            while (true)
            {
                vGrid.Repurpose(Center, FirstLevelSystemData);
                vGrid.Randomize();
                (int _, int triangleCount) = vGrid.PreCalculateGeometryData();

                if (triangleCount == 0)
                {
                    FirstLevelSystemData = FirstLevelSystemData.GetWithDoubleVoxelSize();
                    continue;
                }

                if (vGrid.EdgePointsUsed().IsAnyUsed())
                {
                    FirstLevelSystemData = FirstLevelSystemData.GetWithDoubleVoxelSize();
                    continue;
                }

                /* So at this point it has found a grid size that contains the whole grid within it
                 * without any of it touching the sides. The hierarchy is a 2x2x2 box of grids meaning
                 * that the grid size in a hierarchy only has to be halfthe size. That's why the system
                 * data is halved here.
                 */
                FirstLevelSystemData = FirstLevelSystemData.GetWithHalfVoxelSize();

                Grid = new VoxelHierarchy(Center, FirstLevelSystemData, GridPos.RootPos().GoDownTree());
                Grid.Generate(Center, FirstLevelSystemData, vGrid, GridPos.RootPos().GoDownTree());
                break;
            }
        }

        public void UpdateModel(PlayerCamera camera, float yAngle)
        {
            ModelTrans.Update(camera, yAngle);
        }

        public void CheckVoxelResolution(Frustum renderCheck)
        {
            GridPos rootPos = GridPos.RootPos().GoDownTree();
            Grid.CheckAndIncreaseResolution(renderCheck, ModelTrans, FirstLevelSystemData, in rootPos);
        }
    }
}
