using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CompositionStuff;

#pragma warning disable IDE1006

internal static partial class UuidOfTypeMethods
{
    /// <summary>Retrieves the GUID of of a specified type.</summary>
    /// <typeparam name="T">The type to retrieve the GUID for.</typeparam>
    /// <returns>A <see cref="UuidOfType"/> value wrapping a pointer to the GUID data for the input type. This value can be either converted to a <see cref="Guid"/> pointer, or implicitly assigned to a <see cref="Guid"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Guid* __uuidof<T>()
        where T : unmanaged, Windows.Win32.IComIID
    {
        // This is safe, as we know T.Guid here is a reference to an RVA-backed field,
        // which is directly embedded into our assembly (which is always present).
        return (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in T.Guid));
    }
}