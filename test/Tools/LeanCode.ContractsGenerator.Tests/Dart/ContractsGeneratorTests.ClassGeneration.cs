using Xunit;
using static LeanCode.ContractsGenerator.Tests.Dart.ContractsGeneratorTestHelpers;

namespace LeanCode.ContractsGenerator.Tests.Dart
{
    public partial class ContractsGeneratorTests
    {
        [Fact]
        public void Private_class_is_not_resolved()
        {
            var generator = CreateDartGeneratorFromNamespace("private class TestClass { }");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.DoesNotContain("interface TestClass", contracts);
        }

        [Fact]
        public void Protected_class_is_not_resolved()
        {
            var generator = CreateDartGeneratorFromNamespace("private class TestClass { }");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.DoesNotContain("interface TestClass", contracts);
        }

        [Fact]
        public void Class_with_no_access_modifier_is_not_resolved()
        {
            var generator = CreateDartGeneratorFromNamespace("class TestClass { }");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.DoesNotContain("interface TestClass", contracts);
        }

        [Fact]
        public void Public_class_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public class TestClass { }");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("interface TestClass", contracts);
        }

        [Fact]
        public void Generic_class_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public class TestClass<T> { }");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("interface TestClass<T>", contracts);
        }

        [Fact]
        public void Generic_class_with_constraints_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public interface IInt { } public class TestClass<T> where T: IList<IInt> { }");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("interface TestClass<T extends IInt[]>", contracts);
        }

        [Fact]
        public void Class_inheritance_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public interface IInt { } public class TestClass : IInt {}");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("interface TestClass extends IInt", contracts);
        }

        [Fact]
        public void Generic_class_inheritance_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public interface IInt<T> { } public class TestClass<T> : IInt<T> {}");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("interface TestClass<T> extends IInt<T>", contracts);
        }

        [Fact]
        public void Deep_inheritance_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public interface IInt<T> { } public class TestClass : IInt<List<int>> {}");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("interface TestClass extends IInt<number[]>", contracts);
        }

        [Fact]
        public void Deep_generic_inheritance_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public interface IInt<T> { } public class TestClass<T> : IInt<List<T>> { }");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("interface TestClass<T> extends IInt<T[]>", contracts);
        }

        [Fact]
        public void Nested_class_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public class TestClass : List<Result> { public class Result {} }");

            var contracts = GetContracts(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("interface TestClass extends Result[] {", contracts);
            Assert.Contains("interface Result {", contracts);
        }

        [Fact]
        public void Static_class_class_is_resolved_correctly()
        {
            var generator = CreateDartGeneratorFromNamespace("public static class ErrorCodes { public const int Invalid = 1; }");

            var client = GetClient(generator.Generate(DefaultDartConfiguration));

            Assert.Contains("Invalid: 1", client);
            Assert.Contains("ErrorCodes: {", client);
        }
    }
}
