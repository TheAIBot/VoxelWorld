using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using VoxelWorld.Render.VoxelGrid;
using VoxelWorld.Shaders;
using static VoxelWorld.Render.VoxelGrid.VAO;

namespace VoxelWorld.Render.Box
{
    internal sealed class BoxRender : IDisposable
    {
        private static readonly Vector3[] BoxVertices = new Vector3[]
        {
            new Vector3(-1, -1,  1),
            new Vector3( 1, -1,  1),
            new Vector3(-1,  1,  1),
            new Vector3( 1,  1,  1),
            new Vector3( 1, -1, -1),
            new Vector3( 1,  1, -1),
            new Vector3(-1,  1, -1),
            new Vector3(-1, -1, -1)
        };

        private static readonly uint[] BoxIndices = new uint[]
        {
            0, 1, 2, 1, 3, 2,
            1, 4, 3, 4, 5, 3,
            4, 7, 5, 7, 6, 5,
            7, 0, 6, 0, 2, 6,
            7, 4, 0, 4, 1, 0,
            2, 3, 6, 3, 5, 6
        };

        private readonly VBO<Vector3> VerticesBuffer;
        private readonly VBO<uint> IndicesBuffer;
        private readonly VBO<float> BoxScalesBuffer;
        private readonly VBO<Vector3> BoxCenterBuffer;
        private readonly VAO BoxVAO;
        private readonly float[] BoxScales = new float[100_000];
        private readonly Vector3[] BoxCenters = new Vector3[100_000];
        private readonly Dictionary<Vector3, int> CenterToIndex = new Dictionary<Vector3, int>();

        public BoxRender(GL openGl)
        {
            VerticesBuffer = new VBO<Vector3>(openGl, BoxVertices);
            IndicesBuffer = new VBO<uint>(openGl, BoxIndices, BufferTargetARB.ElementArrayBuffer);
            BoxScalesBuffer = new VBO<float>(openGl, 100_000);
            BoxCenterBuffer = new VBO<Vector3>(openGl, 100_000);
            BoxScalesBuffer.Divisor = 1;
            BoxCenterBuffer.Divisor = 1;

            IGenericVBO[] vbos = new IGenericVBO[]
            {
                new GenericVBO<Vector3>(VerticesBuffer, "vertex_pos"),
                new GenericVBO<float>(BoxScalesBuffer, "vertex_scale"),
                new GenericVBO<Vector3>(BoxCenterBuffer, "vertex_offset"),
                new GenericVBO<uint>(IndicesBuffer),
            };
            BoxVAO = new VAO(openGl, BoxShader.GetShader(openGl), vbos);
        }

        public bool TryAdd(in BoxRenderInfo boxInfo)
        {
            if (CenterToIndex.Count == BoxCenters.Length)
            {
                return false;
            }

            int index = CenterToIndex.Count;
            BoxScales[index] = boxInfo.GridSideLength;
            BoxCenters[index] = boxInfo.GridCenter;
            CenterToIndex.Add(boxInfo.GridCenter, index);
            return true;
        }

        public void Remove(in Vector3 center)
        {
            int index;
            if (!CenterToIndex.TryGetValue(center, out index))
            {
                throw new Exception("Tried to remove a box thatwasn't present.");
            }
            CenterToIndex.Remove(center);

            //Because a box was removed, the arrays might no longer be
            //contiguous and that is a problem because drawing depends
            //on them being so. If there is more boxes to render and
            //if we didn't just remove the last box, then take the box
            //at the last used index and move to the index that a box
            //was just removed from.
            if (CenterToIndex.Count == 0)
            {
                return;
            }
            else if (index == CenterToIndex.Count)
            {
                return;
            }
            else
            {
                int lastUsedIndex = CenterToIndex.Count;
                BoxScales[index] = BoxScales[lastUsedIndex];
                BoxCenters[index] = BoxCenters[lastUsedIndex];
                CenterToIndex[BoxCenters[index]] = index;
            }
        }

        public void CopyToGPU()
        {
            BoxScalesBuffer.BufferSubData(BoxScales.AsSpan(0, CenterToIndex.Count));
            BoxCenterBuffer.BufferSubData(BoxCenters.AsSpan(0, CenterToIndex.Count));
        }

        public void Draw()
        {
            BoxVAO.DrawInstanced(CenterToIndex.Count);
        }

        public void Dispose()
        {
            BoxVAO.Dispose();
        }
    }
}
