namespace System.Threading.Tasks
{
    /// <summary>
    /// Shim extension providing polyfills for common <see cref="ValueTask"/> factory methods
    /// that are available in later .NET versions but missing in .NET Standard 2.1 / Meadow.
    /// 
    /// Purpose:
    /// - <see cref="CompletedTask"/> allows returning a completed <see cref="ValueTask"/> 
    ///   without allocating a new instance.
    /// - <see cref="FromResult{T}(T)"/> allows returning a <see cref="ValueTask{T}"/> 
    ///   with an already computed result.
    /// 
    /// Usage:
    /// These helpers let APIs expose <see cref="ValueTask"/> results while keeping
    /// the code portable across runtimes without requiring newer BCL features.
    /// </summary>
    internal static class ValueTaskEx
    {
        /// <summary>
        /// Returns a default, already-completed <see cref="ValueTask"/>.
        /// Equivalent to <c>ValueTask.CompletedTask</c> in .NET 5+.
        /// </summary>
        public static ValueTask CompletedTask => default;

        /// <summary>
        /// Creates a completed <see cref="ValueTask{T}"/> wrapping the specified <paramref name="value"/>.
        /// Equivalent to <c>ValueTask.FromResult(value)</c> in .NET 5+.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="value">The value to wrap in the completed task.</param>
        /// <returns>A completed <see cref="ValueTask{T}"/> containing <paramref name="value"/>.</returns>
        public static ValueTask<T> FromResult<T>(T value) => new ValueTask<T>(value);
    }
}
