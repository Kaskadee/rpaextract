using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace rpaextract.Extensions;

/// <summary>
///     Provides extension methods for the <see cref="IEnumerator{T}" /> interface.
/// </summary>
public static class IEnumeratorExtensions {
    /// <summary>
    ///     Endlessly cycles through the specified source array.
    /// </summary>
    /// <typeparam name="T">The type of the source array.</typeparam>
    /// <param name="source">The source array to repeat indefinitely.</param>
    /// <returns>The <see cref="IEnumerator{T}" /> which cycles through the source array indefinitely.</returns>
    [SuppressMessage("ReSharper", "IteratorNeverReturns")]
    public static IEnumerator<T> Cycle<T>(this T[] source) {
        while (true)
            foreach (T e in source)
                yield return e;
    }

    /// <summary>
    ///     Advances the enumerator to the next element of the collection and returns the element at the new position of the
    ///     enumerator.
    /// </summary>
    /// <typeparam name="T">The type of the enumerator.</typeparam>
    /// <param name="enumerator">The <see cref="IEnumerator{T}" /> to advance.</param>
    /// <returns>The next element in the specified <see cref="IEnumerator{T}" />.</returns>
    public static T Next<T>(this IEnumerator<T> enumerator) {
        enumerator.MoveNext();
        return enumerator.Current;
    }
}
