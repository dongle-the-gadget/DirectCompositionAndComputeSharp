using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Windows.Win32.Foundation;
using static Windows.Win32.Foundation.HRESULT;

namespace CompositionStuff;

/// <summary>A type that allows working with pointers to COM objects more securely.</summary>
/// <typeparam name="T">The type to wrap in the current <see cref="ComPtr{T}"/> instance.</typeparam>
/// <remarks>While this type is not marked as <see langword="ref"/> so that it can also be used in fields, make sure to keep the reference counts properly tracked if you do store <see cref="ComPtr{T}"/> instances on the heap.</remarks>
internal unsafe struct ComPtr<T> : IDisposable
    where T : unmanaged, Windows.Win32.IComIID
{
    /// <summary>The raw pointer to a COM object, if existing.</summary>
    private T* ptr_;

    /// <summary>Creates a new <see cref="ComPtr{T}"/> instance from a raw pointer and increments the ref count.</summary>
    /// <param name="other">The raw pointer to wrap.</param>
    public ComPtr(T* other)
    {
        ptr_ = other;
        InternalAddRef();
    }

    /// <summary>Releases the current COM object, if any, and replaces the internal pointer with an input raw pointer.</summary>
    /// <param name="other">The input raw pointer to wrap.</param>
    /// <remarks>This method will release the current raw pointer, if any, but it will not increment the references for <paramref name="other"/>.</remarks>
    public void Attach(T* other)
    {
        if (ptr_ != null)
        {
            var @ref = ((Windows.Win32.System.Com.IUnknown*)ptr_)->Release();
            Debug.Assert((@ref != 0) || (ptr_ != other));
        }
        ptr_ = other;
    }

    /// <summary>Returns the raw pointer wrapped by the current instance, and resets the current <see cref="ComPtr{T}"/> value.</summary>
    /// <returns>The raw pointer wrapped by the current <see cref="ComPtr{T}"/> value.</returns>
    /// <remarks>This method will not change the reference count for the COM object in use.</remarks>
    public T* Detach()
    {
        T* ptr = ptr_;
        ptr_ = null;
        return ptr;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        T* pointer = ptr_;
        if (pointer != null)
        {
            ptr_ = null;

            _ = ((Windows.Win32.System.Com.IUnknown*)pointer)->Release();
        }
    }

    /// <summary>Gets the currently wrapped raw pointer to a COM object.</summary>
    /// <returns>The raw pointer wrapped by the current <see cref="ComPtr{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T* Get()
    {
        return ptr_;
    }

    /// <summary>Gets the address of the current <see cref="ComPtr{T}"/> instance as a raw <typeparamref name="T"/> double pointer. This method is only valid when the current <see cref="ComPtr{T}"/> instance is on the stack or pinned.
    /// </summary>
    /// <returns>The raw pointer to the current <see cref="ComPtr{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T** GetAddressOf()
    {
        return (T**)Unsafe.AsPointer(ref Unsafe.AsRef(in this));
    }

    /// <summary>Gets the address of the current <see cref="ComPtr{T}"/> instance as a raw <typeparamref name="T"/> double pointer.</summary>
    /// <returns>The raw pointer to the current <see cref="ComPtr{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [UnscopedRef]
    public readonly ref T* GetPinnableReference()
    {
        return ref Unsafe.AsRef(in this).ptr_;
    }

    /// <summary>Releases the current COM object in use and gets the address of the <see cref="ComPtr{T}"/> instance as a raw <typeparamref name="T"/> double pointer. This method is only valid when the current <see cref="ComPtr{T}"/> instance is on the stack or pinned.</summary>
    /// <returns>The raw pointer to the current <see cref="ComPtr{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T** ReleaseAndGetAddressOf()
    {
        Dispose();

        return GetAddressOf();
    }

    /// <summary>Swaps the current COM object reference with that of a given <see cref="ComPtr{T}"/> instance.</summary>
    /// <param name="r">The target <see cref="ComPtr{T}"/> instance to swap with the current one.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Swap(ComPtr<T>* r)
    {
        T* tmp = ptr_;
        ptr_ = r->ptr_;
        r->ptr_ = tmp;
    }

    /// <summary>Swaps the current COM object reference with that of a given <see cref="ComPtr{T}"/> instance.</summary>
    /// <param name="other">The target <see cref="ComPtr{T}"/> instance to swap with the current one.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Swap(ref ComPtr<T> other)
    {
        T* tmp = ptr_;
        ptr_ = other.ptr_;
        other.ptr_ = tmp;
    }

    /// <summary>Converts the current COM object reference to a given interface type and assigns that to a target <see cref="ComPtr{T}"/>.</summary>
    /// <param name="other">The target reference to copy the address of the current COM object to.</param>
    /// <returns>The result of <see cref="IUnknown.QueryInterface"/> for the target type <typeparamref name="U"/>.</returns>
    public readonly HRESULT CopyTo<U>(ref ComPtr<U> other)
        where U : unmanaged, Windows.Win32.IComIID
    {
        U* ptr;
        HRESULT result = ((Windows.Win32.System.Com.IUnknown*)ptr_)->QueryInterface(UuidOfTypeMethods.__uuidof<U>(), (void**)&ptr);

        other.Attach(ptr);
        return result;
    }

    // Increments the reference count for the current COM object, if any
    private readonly void InternalAddRef()
    {
        T* temp = ptr_;

        if (temp != null)
        {
            _ = ((Windows.Win32.System.Com.IUnknown*)ptr_)->AddRef();
        }
    }
}