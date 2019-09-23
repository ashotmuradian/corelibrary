using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using LeanCode.ContractsGenerator.Extensions;
using LeanCode.ContractsGenerator.Statements;

namespace LeanCode.ContractsGenerator.Languages.Dart
{
    internal class DartVisitor : ILanguageVisitor
    {
        private static readonly HashSet<string> BuiltinTypes = new HashSet<string>
        {
            "int",
            "double",
            "float",
            "single",
            "int32",
            "uint32",
            "byte",
            "sbyte",
            "int64",
            "short",
            "long",
            "decimal",
            "bool",
            "boolean",
            "guid",
            "string",
        };
        private readonly StringBuilder definitionsBuilder = new StringBuilder();
        private readonly DartConfiguration configuration;
        private Dictionary<string, (string name, INamespacedStatement statement)> mangledStatements;

        public DartVisitor(DartConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public IEnumerable<LanguageFileOutput> Visit(ClientStatement statement)
        {
            GenerateDartImportsAndHelpers(statement);

            GenerateTypeNames(statement);

            Visit(statement, 0, null);

            yield return new LanguageFileOutput
            {
                Name = statement.Name + ".dart",
                Content = definitionsBuilder.ToString(),
            };
        }

        private void GenerateDartImportsAndHelpers(ClientStatement statement)
        {
            definitionsBuilder
                .AppendLine("import 'package:meta/meta.dart';")
                .AppendLine("import 'package:json_annotation/json_annotation.dart';")
                .AppendLine("import 'package:dart_cqrs/dart_cqrs.dart';");

            definitionsBuilder.AppendLine($"part '{statement.Name}.g.dart';");

            definitionsBuilder
                .AppendLine("abstract class IRemoteQuery<T> extends Query<T> {}")
                .AppendLine("abstract class IRemoteCommand extends Command {}");

            definitionsBuilder
                .AppendLine("List<T> _listFromJson<T>(")
                .AppendLine("dynamic decodedJson, T itemFromJson(Map<String, dynamic> map)) {")
                .AppendLine("return decodedJson?.map((dynamic e) => itemFromJson(e))?.toList()?.cast<T>(); }");

            definitionsBuilder
                .AppendLine("DateTime _normalizeDate(String dateString) {")
                .AppendLine("return dateString == null")
                .AppendLine("? null")
                .AppendLine(": DateTime.parse(dateString.substring(0, 19) + ' Z');")
                .AppendLine("}");
        }

        private void GenerateTypeNames(ClientStatement statement)
        {
            var symbols = new List<(string name, INamespacedStatement statement)>();

            foreach (var child in statement.Children)
            {
                if (child is InterfaceStatement || child is EnumStatement)
                {
                    symbols.Add((child.Name, child as INamespacedStatement));

                    if (child is InterfaceStatement iStmt)
                    {
                        symbols.AddRange(iStmt.Children.Select(x => (x.Name, x as INamespacedStatement)));
                    }
                }
            }

            mangledStatements = symbols
                .GroupBy(x => x.name)
                .Select(MangleGroup)
                .SelectMany(x => x)
                .ToDictionary(x => Mangle(x.statement.Namespace, x.statement.Name), x => (x.name, x.statement));
        }

        private void Visit(IStatement statement, int level, string parentName)
        {
            switch (statement)
            {
                case ClientStatement stmt: VisitClientStatement(stmt, level); break;
                case EnumStatement stmt: VisitEnumStatement(stmt, level); break;

                case CommandStatement stmt: VisitCommandStatement(stmt, level, parentName); break;
                case QueryStatement stmt: VisitQueryStatement(stmt, level, parentName); break;

                case InterfaceStatement stmt: VisitInterfaceStatement(stmt, level, parentName); break;
            }
        }

        private void VisitClientStatement(ClientStatement statement, int level)
        {
            foreach (var child in statement.Children)
            {
                Visit(child, level, statement.Name);
            }
        }

        private void VisitEnumStatement(EnumStatement statement, int level)
        {
            var name = mangledStatements[Mangle(statement.Namespace, statement.Name)].name;

            definitionsBuilder.AppendSpaces(level)
                .Append("enum ")
                .Append(name)
                .AppendLine(" {")
                .AppendSpaces(level + 1);

            foreach (var value in statement.Values)
            {
                VisitEnumValueStatement(value, level + 1);
            }

            definitionsBuilder.AppendSpaces(level)
                .AppendLine("}")
                .AppendLine();

            return;
        }

        private void VisitEnumValueStatement(EnumValueStatement statement, int level)
        {
            definitionsBuilder.AppendSpaces(level)
                .Append("@JsonValue(");

            if (int.TryParse(statement.Value, out int value))
            {
                definitionsBuilder.Append(value);
            }
            else
            {
                definitionsBuilder.Append($"\"{statement.Value}\"");
            }

            definitionsBuilder.AppendLine(")");

            definitionsBuilder.AppendSpaces(level)
                .Append(TranslateIdentifier(statement.Name))
                .AppendLine(",");
        }

        private void VisitTypeStatement(TypeStatement statement)
        {
            if (statement.IsDictionary)
            {
                definitionsBuilder.Append("Map<");

                VisitTypeStatement(statement.TypeArguments.First());

                definitionsBuilder.Append(", ");

                VisitTypeStatement(statement.TypeArguments.Last());

                definitionsBuilder.Append(">");
            }
            else if (statement.IsArrayLike)
            {
                definitionsBuilder.Append("List<");

                VisitTypeStatement(statement.TypeArguments.First());

                definitionsBuilder.Append(">");
            }
            else if (statement.TypeArguments.Count > 0)
            {
                var name = statement.Name;

                if (!configuration.UnmangledTypes.Contains(statement.Name))
                {
                    name = mangledStatements[Mangle(statement.Namespace, statement.Name)].name;
                }

                definitionsBuilder.Append(name);
                definitionsBuilder.Append("<");

                for (int i = 0; i < statement.TypeArguments.Count; i++)
                {
                    VisitTypeStatement(statement.TypeArguments[i]);

                    if (i < statement.TypeArguments.Count - 1)
                    {
                        definitionsBuilder.Append(", ");
                    }
                }

                definitionsBuilder.Append(">");
            }
            else if (configuration.TypeTranslations.TryGetValue(statement.Name.ToLowerInvariant(), out string newName))
            {
                definitionsBuilder.Append(newName);
            }
            else
            {
                var name = statement.Name;

                if (!configuration.UnmangledTypes.Contains(statement.Name))
                {
                    if (mangledStatements.TryGetValue(Mangle(statement.Namespace, statement.Name), out var type))
                    {
                        name = type.name;
                    }
                }

                definitionsBuilder.Append(name);
            }
        }

        private void VisitTypeParameterStatement(TypeParameterStatement statement)
        {
            definitionsBuilder.Append(statement.Name);

            if (statement.Constraints.Any())
            {
                definitionsBuilder.Append(" implements ");

                for (int i = 0; i < statement.Constraints.Count; i++)
                {
                    VisitTypeStatement(statement.Constraints[i]);

                    if (i < statement.Constraints.Count - 1)
                    {
                        definitionsBuilder.Append(", ");
                    }
                }
            }
        }

        private void VisitFieldStatement(FieldStatement statement, int level)
        {
            GenerateJsonKey(statement, level + 1);

            VisitTypeStatement(statement.Type);

            definitionsBuilder.Append(" ")
                .Append(TranslateIdentifier(statement.Name))
                .AppendLine(";");
        }

        private void VisitCommandStatement(CommandStatement statement, int level, string parentName)
        {
            VisitInterfaceStatement(statement, level, parentName, true);
        }

        private void VisitQueryStatement(QueryStatement statement, int level, string parentName)
        {
            VisitInterfaceStatement(statement, level, parentName, true, true);
        }

        private void VisitInterfaceStatement(InterfaceStatement statement, int level, string parentName, bool includeFullName = false, bool includeResultFactory = false)
        {
            var name = mangledStatements[Mangle(statement.Namespace, statement.Name)].name;

            if (statement.Extends.Any(x => x.Name == "Enum"))
            {
                VisitEnumStatement(new EnumStatement { Name = name }, level);
                return;
            }

            definitionsBuilder.AppendLine()
                .AppendSpaces(level + 1)
                .AppendLine("@JsonSerializable()");

            definitionsBuilder.AppendSpaces(level)
                .Append("class ")
                .Append(name);

            if (statement.Parameters.Any())
            {
                definitionsBuilder.Append("<");

                for (int i = 0; i < statement.Parameters.Count; i++)
                {
                    VisitTypeParameterStatement(statement.Parameters[i]);

                    if (i < statement.Parameters.Count - 1)
                    {
                        definitionsBuilder.Append(", ");
                    }
                }

                definitionsBuilder.Append(">");
            }

            var mapJsonIncludeSuper = false;

            if (statement.IsClass && statement.BaseClass != null)
            {
                mapJsonIncludeSuper = true;
                definitionsBuilder.Append(" extends ");
                VisitTypeStatement(statement.BaseClass);
            }

            var mappedInterfaces = statement.Extends
                .Where(e => e.Name.StartsWith("IRemoteQuery") || e.Name.StartsWith("IRemoteCommand"))
                .ToList();

            if (mappedInterfaces.Any())
            {
                definitionsBuilder.Append(" implements ");

                for (var i = 0; i < mappedInterfaces.Count; i++)
                {
                    VisitTypeStatement(mappedInterfaces[i]);

                    if (i < mappedInterfaces.Count - 1)
                    {
                        definitionsBuilder.Append(", ");
                    }
                }
            }

            definitionsBuilder.AppendLine(" {");

            GenerateDefaultConstructor(name, level);

            foreach (var constant in statement.Constants)
            {
                definitionsBuilder
                    .AppendSpaces(level + 1)
                    .AppendLine($"static const int {constant.Name} = {constant.Value};");
            }

            definitionsBuilder.AppendLine();

            foreach (var field in statement.Fields)
            {
                VisitFieldStatement(field, level + 1);
            }

            if (includeFullName)
            {
                definitionsBuilder
                    .AppendLine()
                    .AppendSpaces(level + 1)
                    .AppendLine("@override")
                    .AppendSpaces(level + 1)
                    .AppendLine($"String getFullName() => '{statement.Namespace}.{statement.Name}';")
                    .AppendLine();
            }

            if (includeResultFactory)
            {
                GenerateResultFactory(statement, level);
            }

            var includeOverrideAnnotation = includeFullName || statement.Extends.Any();
            GenerateToJsonMethod(statement, name, level, includeOverrideAnnotation, mapJsonIncludeSuper);
            GenerateFromJsonConstructor(name, statement, level);

            definitionsBuilder.AppendSpaces(level);
            definitionsBuilder.AppendLine("}");
            definitionsBuilder.AppendLine();

            if (statement.Children.Any())
            {
                foreach (var child in statement.Children)
                {
                    Visit(child, level, parentName + "." + statement.Name);
                }
            }
        }

        private void GenerateResultFactory(InterfaceStatement statement, int level)
        {
            var result = statement.Extends
                .Where(x => x.Name == "IRemoteQuery")
                .First()
                .TypeArguments.Last();

            definitionsBuilder
                .AppendLine()
                .AppendSpaces(level + 1)
                .AppendLine("@override")
                .AppendSpaces(level + 1);

            VisitTypeStatement(result);

            definitionsBuilder
                .AppendLine(" resultFactory(dynamic decodedJson) {");

            if (result.IsArrayLike)
            {
                definitionsBuilder.AppendLine()
                    .AppendSpaces(level + 1);

                result = result.TypeArguments.First();

                if (BuiltinTypes.Contains(result.Name.ToLowerInvariant()))
                {
                    definitionsBuilder
                        .AppendSpaces(level + 1)
                        .AppendLine("return decodedJson.cast<");

                    VisitTypeStatement(result);

                    definitionsBuilder
                        .AppendLine(">();");
                }
                else
                {
                    var name = mangledStatements[Mangle(result.Namespace, result.Name)].name;

                    definitionsBuilder
                        .AppendSpaces(level + 1)
                        .AppendLine($"return _listFromJson(decodedJson,_${name}FromJson);");
                }
            }
            else
            {
                if (!BuiltinTypes.Contains(result.Name.ToLowerInvariant()))
                {
                    var name = mangledStatements[Mangle(result.Namespace, result.Name)].name;

                    definitionsBuilder
                        .AppendLine($"return _${name}FromJson(decodedJson);");
                }
                else
                {
                    definitionsBuilder.Append("return decodedJson as ");
                    VisitTypeStatement(result);
                    definitionsBuilder.AppendLine(";");
                }
            }

            definitionsBuilder
                .AppendSpaces(level + 1)
                .AppendLine("}");
        }

        private void GenerateJsonKey(FieldStatement statement, int level)
        {
            var type = statement.Type;

            definitionsBuilder
                .AppendSpaces(level)
                .Append("@JsonKey(")
                .Append($"name: '{statement.Name}'");

            if (type.IsNullable)
            {
                definitionsBuilder.Append(", nullable: true");
            }

            if (type.Name == "DateTime")
            {
                definitionsBuilder.Append($", fromJson: _normalizeDate");
            }

            definitionsBuilder.AppendLine(")");
        }

        private void GenerateDefaultConstructor(string name, int level)
        {
            definitionsBuilder
                .AppendLine()
                .AppendSpaces(level)
                .AppendLine($"{name}();");
        }

        private void GenerateToJsonMethod(InterfaceStatement statement, string fullName, int level, bool includeOverrideAnnotation, bool includeSuper)
        {
            var annotation = includeOverrideAnnotation ? "@override" : "@virtual";

            definitionsBuilder
                .AppendLine()
                .AppendSpaces(level + 1)
                .AppendLine(annotation)
                .AppendSpaces(level + 1)
                .AppendLine("Map<String, dynamic> toJsonMap()")
                .Append("=>")
                .AppendLine($"_${fullName}ToJson(this);");
        }

        private void GenerateFromJsonConstructor(string name, InterfaceStatement statement, int level)
        {
            definitionsBuilder.AppendLine()
                .AppendSpaces(level + 1)
                .Append($"factory {name}.fromJson(Map<String, dynamic> json) =>")
                .AppendLine($"_${name}FromJson(json);");
        }

        private string Mangle(string namespaceName, string identifier)
        {
            if (configuration.UnmangledTypes.Any(x => identifier == x))
            {
                return identifier;
            }

            if (string.IsNullOrEmpty(namespaceName))
            {
                return identifier;
            }

            return $"{namespaceName}.{identifier}".Replace('.', '_');
        }

        private IList<(string name, INamespacedStatement statement)> MangleGroup(IGrouping<string, (string name, INamespacedStatement statement)> group)
        {
            var mangle = group.Count() > 1;

            if (!mangle)
            {
                return group.Select(x => (x.name, x.statement)).ToList();
            }

            var limit = group.Select(s => s.statement.Namespace.Split('.').Count()).Max();

            int depth = 1;

            while (depth <= limit)
            {
                var groups = group.Select(g => (name: MakeName(g.statement.Namespace, g.name, depth), g.statement))
                            .GroupBy(g => g.name);

                if (groups.Any(g => g.Count() > 1))
                {
                    ++depth;
                }
                else
                {
                    return groups.Select(x => (x.First().name, x.First().statement)).ToList();
                }
            }

            return group
                .Select(x => (mangle ? Mangle(x.statement.Namespace, x.name) : x.name, x.statement))
                .ToList();
        }

        private string MakeName(string namespaceName, string name, int depth)
        {
            var split = namespaceName.Split('.').Reverse().Take(depth).Append(name);
            return string.Join(string.Empty, split);
        }

        private string TranslateIdentifier(string identifier)
        {
            var translated = identifier.Uncapitalize();

            if (translated == "new")
            {
                translated += '_';
            }

            return translated;
        }
    }
}
