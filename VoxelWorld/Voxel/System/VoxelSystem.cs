using OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace VoxelWorld
{
    internal class VoxelSystem
    {
        private VoxelHierarchy Grid = null;
        private readonly Vector3 Center;
        internal VoxelSystemData FirstLevelSystemData;
        private readonly ModelTransformations ModelTrans = new ModelTransformations();

        public Matrix4 Model { get { return ModelTrans.Rotation; } }

        public VoxelSystem(int gridSize, Vector3 center, float voxelSize, PlanetGen generator)
        {
            this.Center = center;
            this.FirstLevelSystemData = new VoxelSystemData(gridSize, voxelSize, generator);
        }

        /// <summary>
        /// Initializes the system with a root grid hierarchy with a voxel size
        /// that encompasses the whole model in the root grid hierarchy.
        /// </summary>
        public void TestResizeToFindFirstGrid()
        {
            VoxelGrid vGrid = new VoxelGrid(new Vector3(0, 0, 0), FirstLevelSystemData);
            while (true)
            {
                using VoxelGridHierarchy grid = new VoxelGridHierarchy(Center, FirstLevelSystemData.GridSize, FirstLevelSystemData.VoxelSize);
                
                vGrid.Repurpose(Center, FirstLevelSystemData);

                grid.GenerateGrid(FirstLevelSystemData, vGrid, GridPos.RootPos());
                if (grid.IsEmpty())
                {
                    FirstLevelSystemData = FirstLevelSystemData.GetWithDoubleVoxelSize();
                    continue;
                }


                if (grid.AnyVoxelsAtGridEdge())
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

                VoxelHierarchy hir = new VoxelHierarchy(Center, FirstLevelSystemData, GridPos.RootPos());
                hir.Generate(new Vector3(0, 0, 0), FirstLevelSystemData, vGrid, GridPos.RootPos());
                Grid = hir;

                break;
            }
        }

        public void UpdateModel(PlayerCamera camera, float yAngle)
        {
            ModelTrans.Update(camera, yAngle);
        }

        public void CheckVoxelResolution(Frustum renderCheck)
        {
            GridPos rootPos = new GridPos();
            Grid.CheckAndIncreaseResolution(renderCheck, ModelTrans, FirstLevelSystemData, ref rootPos);
        }
    }
}
