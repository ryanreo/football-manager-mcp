// Unity's generated interop assembly contains a native NullableAttribute wrapper
// without the constructors Roslyn needs. Local compiler attributes avoid binding
// emitted .NET metadata to that IL2CPP wrapper.
#nullable disable
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
internal sealed class NullableAttribute : Attribute
{
    public readonly byte[] NullableFlags;
    public NullableAttribute(byte value) { NullableFlags = new[] { value }; }
    public NullableAttribute(byte[] value) { NullableFlags = value; }
}
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
internal sealed class NullableContextAttribute : Attribute
{
    public readonly byte Flag;
    public NullableContextAttribute(byte value) { Flag = value; }
}
