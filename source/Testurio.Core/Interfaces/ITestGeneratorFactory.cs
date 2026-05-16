using Testurio.Core.Enums;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Factory for creating <see cref="ITestGeneratorAgent"/> instances by test type.
/// Implemented in <c>Testurio.Pipeline.AgentRouter</c> as <c>TestGeneratorFactory</c>.
/// Concrete generator implementations are registered in DI by feature 0028.
/// </summary>
public interface ITestGeneratorFactory
{
    /// <summary>
    /// Creates an <see cref="ITestGeneratorAgent"/> for the specified <paramref name="testType"/>.
    /// </summary>
    /// <param name="testType">The test type to produce a generator for.</param>
    /// <returns>An <see cref="ITestGeneratorAgent"/> capable of generating tests of the given type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="testType"/> is not a recognised MVP value.
    /// No silent fallback is provided — unrecognised types must fail loudly.
    /// </exception>
    ITestGeneratorAgent Create(TestType testType);
}
