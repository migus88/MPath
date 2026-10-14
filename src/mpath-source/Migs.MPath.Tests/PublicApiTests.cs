using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Migs.MPath.Core;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PublicApiTests
    {
        [Test]
        public void PublicApi_HasNoObsoleteMembers()
        {
            // Arrange
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.DeclaredOnly;
            var publicTypes = typeof(Pathfinder).Assembly.GetExportedTypes();

            // Act
            var obsolete = publicTypes
                .Where(IsObsoleteType)
                .Select(type => type.FullName)
                .Concat(publicTypes
                    .SelectMany(type => type.GetMembers(flags))
                    .Where(member => member.IsDefined(typeof(ObsoleteAttribute), false))
                    .Select(member => $"{member.DeclaringType!.FullName}.{member.Name}"))
                .ToList();

            // Assert
            obsolete.Should().BeEmpty();
        }

        /// <summary>
        /// The compiler marks every ref struct (such as <c>QueryOptions</c>) with an error-level
        /// <see cref="ObsoleteAttribute"/> so old compilers refuse it; that one is not an API deprecation.
        /// </summary>
        private static bool IsObsoleteType(Type type)
        {
            var attribute = type.GetCustomAttribute<ObsoleteAttribute>(false);
            return attribute != null && !(type.IsByRefLike && attribute.IsError);
        }
    }
}
