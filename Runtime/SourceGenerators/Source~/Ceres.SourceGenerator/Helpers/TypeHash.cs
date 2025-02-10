namespace Ceres.SourceGenerator;

// A copy of the TypeHash in com.unity.entities, used to compute hashes for serializer and variants.
// This is due to the fact that it is not possible to share any dll and having dependencies (now) with packages dlls.
internal static class TypeHash
{
    // http://www.isthe.com/chongo/src/fnv/hash_64a.c
    // with basis and prime:
    const ulong kFNV1A64OffsetBasis = 14695981039346656037;
    const ulong kFNV1A64Prime = 1099511628211;

    public static ulong FNV1A64(string text)
    {
        ulong result = kFNV1A64OffsetBasis;
        if (!string.IsNullOrEmpty(text))
        {
            foreach (var c in text)
            {
                result = kFNV1A64Prime * (result ^ (byte)(c & 255));
                result = kFNV1A64Prime * (result ^ (byte)(c >> 8));
            }
        }
        return result;
    }

    public static ulong FNV1A64(int val)
    {
        ulong result = kFNV1A64OffsetBasis;
        unchecked
        {
            result = (((ulong)(val & 0x000000FF) >>  0) ^ result) * kFNV1A64Prime;
            result = (((ulong)(val & 0x0000FF00) >>  8) ^ result) * kFNV1A64Prime;
            result = (((ulong)(val & 0x00FF0000) >> 16) ^ result) * kFNV1A64Prime;
            result = (((ulong)(val & 0xFF000000) >> 24) ^ result) * kFNV1A64Prime;
        }

        return result;
    }

    public static ulong CombineFNV1A64(ulong hash, params ulong[] values)
    {
        foreach (var value in values)
        {
            hash ^= value;
            hash *= kFNV1A64Prime;
        }

        return hash;
    }
}