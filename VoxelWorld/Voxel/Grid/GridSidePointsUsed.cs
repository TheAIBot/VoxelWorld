using System;

namespace VoxelWorld
{
    public struct GridSidePointsUsed
    {
        public bool PlusX;
        public bool MinusX;
        public bool PlusY;
        public bool MinusY;
        public bool PlusZ;
        public bool MinusZ;

        public const int AdjacentOffsets = 6;

        public void FillWithAdjacentGridOffsets(Span<(bool useAdjacent, GridOffset offset)> offsets)
        {
            if (offsets.Length != AdjacentOffsets)
            {
                throw new ArgumentOutOfRangeException(nameof(offsets), $"Must have a length of {AdjacentOffsets}.");
            }

            offsets[0] = (PlusX, new GridOffset(1, 0, 0));
            offsets[1] = (MinusX, new GridOffset(-1, 0, 0));
            offsets[2] = (PlusY, new GridOffset(0, 1, 0));
            offsets[3] = (MinusY, new GridOffset(0, -1, 0));
            offsets[4] = (PlusZ, new GridOffset(0, 0, 1));
            offsets[5] = (MinusZ, new GridOffset(0, 0, -1));
        }

        public bool IsAnyUsed()
        {
            return PlusX || MinusX || PlusY || MinusY || PlusZ || MinusZ;
        }
    }
}
