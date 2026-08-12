namespace System.Runtime.CompilerServices;

/// <summary>
/// The marker the compiler looks for to allow an <c>init</c> accessor.
/// </summary>
/// <remarks>
/// It ships with .NET 5 and later, and an analyzer targets netstandard2.0 because that is what
/// Roslyn loads. Declaring it here is the usual shim, and it costs nothing: the compiler only ever
/// looks the type up by name.
/// </remarks>
internal static class IsExternalInit;
