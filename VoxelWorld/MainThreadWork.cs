using OpenGL;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using VoxelWorld.Shaders;

namespace VoxelWorld
{
    internal class GridVAO : VAO
    {
        private readonly VBO<Vector3> PositionsVBO;
        private readonly VBO<Vector3> NormalsVBO;
        private readonly VBO<uint> IndicesVBO;

        public GridVAO(VBO<Vector3> posVBO, VBO<Vector3> normalsVBO, VBO<uint> indicesVBO) : base(SimpleShader.GetShader(), new IGenericVBO[]
        {
            new GenericVBO<Vector3>(posVBO, "vertex_pos"),
            new GenericVBO<Vector3>(normalsVBO, "vertex_normal"),
            new GenericVBO<uint>(indicesVBO)
        })
        {
            this.PositionsVBO = posVBO;
            this.NormalsVBO = normalsVBO;
            this.IndicesVBO = indicesVBO;

            DisposeChildren = true;
            DisposeElementArray = true;
        }

        public bool IsLargeEnough(int posCount, int normalCount, int indiceCount)
        {
            return PositionsVBO.Count > posCount && NormalsVBO.Count > normalCount && IndicesVBO.Count > indiceCount;
        }

        public void Reuse(Vector3[] positions, Vector3[] normals, uint[] indices)
        {
            PositionsVBO.BufferSubData(positions);
            NormalsVBO.BufferSubData(normals);
            IndicesVBO.BufferSubData(indices);
            VertexCount = indices.Length;
        }
    }

    internal class WorkOptimizer
    {
        private static int madeCount = 0;

        //private readonly Stack<GridVAO> GridVaos = new Stack<GridVAO>();
        private readonly List<GridVAO> GridVaos = new List<GridVAO>();
        private readonly Queue<int> PosVBOSizes = new Queue<int>();
        private int PosVBOSizesSum = 0;
        private readonly Queue<int> IndiceVBOSizes = new Queue<int>();
        private int IndiceVBOSizesSum = 0;

        private const int SamplesNeeded = 1000;
        private const float MinSizeIncreaseOverAvgSize = 1.5f;

        private void AddSizeSamples(int posSize, int indiceSize)
        {
            PosVBOSizes.Enqueue(posSize);
            PosVBOSizesSum += posSize;
            if (PosVBOSizes.Count > SamplesNeeded)
            {
                PosVBOSizesSum -= PosVBOSizes.Dequeue();
            }

            IndiceVBOSizes.Enqueue(indiceSize);
            IndiceVBOSizesSum += indiceSize;
            if (IndiceVBOSizes.Count > SamplesNeeded)
            {
                IndiceVBOSizesSum -= IndiceVBOSizes.Dequeue();
            }
        }

        private int GetMinPositionVBOSize()
        {
            int avg = PosVBOSizesSum / PosVBOSizes.Count;
            return (int)(avg * MinSizeIncreaseOverAvgSize);
        }

        private int GetMinIndiceVBOSize()
        {
            int avg = IndiceVBOSizesSum / IndiceVBOSizes.Count;
            return (int)(avg * MinSizeIncreaseOverAvgSize);
        }

        public GridVAO MakeGridVAO(Vector3[] positions, Vector3[] normals, uint[] indices)
        {
            AddSizeSamples(positions.Length, indices.Length);

            for (int i = GridVaos.Count - 1; i >= 0; i--)
            {
                if (GridVaos[i].IsLargeEnough(positions.Length, normals.Length, indices.Length))
                {
                    GridVAO vao = GridVaos[i];
                    vao.Reuse(positions, normals, indices);
                    GridVaos.RemoveAt(i);

                    return vao;
                }
            }

            int minPosVBOSize = GetMinPositionVBOSize();
            if (positions.Length < minPosVBOSize)
            {
                Vector3[] resizedPos = new Vector3[minPosVBOSize];
                positions.CopyTo(resizedPos, 0);
                positions = resizedPos;

                Vector3[] resizedNorm = new Vector3[minPosVBOSize];
                normals.CopyTo(resizedNorm, 0);
                normals = resizedNorm;
            }

            int minIndiceVBOSize = GetMinIndiceVBOSize();
            if (indices.Length < minIndiceVBOSize)
            {
                uint[] resizedIndices = new uint[minIndiceVBOSize];
                indices.CopyTo(resizedIndices, 0);
                indices = resizedIndices;
            }

            VBO<uint> indiceBuffer = new VBO<uint>(indices, BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticRead);
            VBO<Vector3> posBuffer = new VBO<Vector3>(positions);
            VBO<Vector3> normalBuffer = new VBO<Vector3>(normals);
            return new GridVAO(posBuffer, normalBuffer, indiceBuffer);
        }

        public void StoreGridVAOForReuse(GridVAO vao)
        {
            if (GridVaos.Count > 3000)
            {
                vao.Dispose();
                return;
            }
            GridVaos.Add(vao);
        }
    }

    internal static class MainThreadWork
    {
        private static readonly ConcurrentQueue<Action<WorkOptimizer>> WorkToDo = new ConcurrentQueue<Action<WorkOptimizer>>();
        private static readonly WorkOptimizer optimizer = new WorkOptimizer();
        private static int MainThreadID = 0;

        public static void QueueWork(Action<WorkOptimizer> action)
        {
            WorkToDo.Enqueue(action);
        }

        public static void ExecuteWork()
        {
            int workLimit = Math.Min(200000, WorkToDo.Count);
            for (int i = 0; i < workLimit; i++)
            {
                if (WorkToDo.TryDequeue(out var work))
                {
                    work.Invoke(optimizer);
                }
            }
        }

        public static void SetThisThreadToMainThread()
        {
            MainThreadID = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
