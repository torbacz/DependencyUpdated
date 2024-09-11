using FluentAssertions;
using FluentAssertions.Execution;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace DependencyUpdated.Core.UnitTests;

public abstract class BaseConfigTest
{
    protected abstract IEnumerable<Tuple<IValidatableObject, IEnumerable<ValidationResult>>> TestCases { get; }

    [Fact]
    public void Validate()
    {
        var data = TestCases.ToList();
        using (new AssertionScope())
        {
            foreach (var test in data)
            {
                var context = new ValidationContext(test.Item1);
                var result = test.Item1.Validate(context);
                result.Should().BeEquivalentTo(test.Item2);
            }
        }
    }
}