using System;
using PurrNet.Packing;

[Serializable]
public struct MP_CostumeIndices : IPackedAuto, IEquatable<MP_CostumeIndices>
{
    public int CloudBase;
    public int Bottom;
    public int Right;
    public int Left;
    public int Face;

    public bool Equals(MP_CostumeIndices other)
    {
        return CloudBase == other.CloudBase && Bottom == other.Bottom &&
            Right == other.Right && Left == other.Left && Face == other.Face;
    }
}
