﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using Our.ModelsBuilder.Api;
using Our.ModelsBuilder.Building;

namespace Our.ModelsBuilder.Tests.Testing
{
    public static class TestUtilities
    {
        public static string ExpectedHeader { get; }
            = @"//------------------------------------------------------------------------------
// <auto-generated>
//   This code was generated by a tool.
//
//    Our.ModelsBuilder v" + ApiVersion.Current.Version + @"
//
//   Changes to this file will be lost if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Web;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Our.ModelsBuilder;
using Our.ModelsBuilder.Umbraco;
using System.CodeDom.Compiler;";

        public static List<PortableExecutableReference> AddReference<T>(this List<PortableExecutableReference> references)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(T).Assembly.Location));
            return references;
        }

        public static List<PortableExecutableReference> CreateDefaultReferences() 
            => new List<PortableExecutableReference>()
                .AddReference<object>() // System
                .AddReference<ContentTypeModelInfo>(); // Our.ModelsBuilder

        public static Compilation Compile(IDictionary<string, string> sources, List<PortableExecutableReference> references = null)
        {
            references ??= CreateDefaultReferences();

            // add references for compilation
            references
                .AddReference<global::Umbraco.Core.ICompletable>() // Umbraco.Core
                .AddReference<global::Umbraco.Web.IHttpContextAccessor>() // Umbraco.Web
                .AddReference<IQueryProvider>() // System.Linq
                .AddReference<global::System.Web.HtmlString>() // System.Web
                .AddReference<global::System.CodeDom.Compiler.GeneratedCodeAttribute>(); // System.CodeDom

            // compile
            var compiler = new Compiler { References = references };
            return compiler.GetCompilation("assembly", sources);
        }

        public static Compilation Compile(CodeModel model, IDictionary<string, string> sources =null, List<PortableExecutableReference> references = null)
        {
            var writer = new CodeWriter(model);

            sources ??= new Dictionary<string, string>();
            foreach (var contentTypeModel in model.ContentTypes.ContentTypes)
            {
                writer.Reset();
                writer.WriteModelFile(contentTypeModel);
                sources[contentTypeModel.Alias + ".generated"] = writer.Code;
                Console.WriteLine(sources[contentTypeModel.Alias + ".generated"]);
            }

            writer.Reset();
            writer.WriteModelInfosFile();
            sources["infos.generated"] = writer.Code;

            return Compile(sources, references);
        }

        public static bool Equals(this Location location, string source, int line)
        {
            var lineSpan = location.GetLineSpan();
            return lineSpan.Path == source && lineSpan.StartLinePosition.Line == line - 1;
        }

        public static SemanticModel GetSemanticModel(this Compilation compilation, string path)
        {
            var tree = compilation.SyntaxTrees.FirstOrDefault(x => x.FilePath == path);
            Assert.IsNotNull(tree, $"Could not get syntax tree with path \"{path}\"");

            return compilation.GetSemanticModel(tree);
        }

        private static Location GetSymbolLocation(this SemanticModel semanticModel, ISymbol symbol)
        {
            var locations = symbol.Locations.Where(x => x.SourceTree == semanticModel.SyntaxTree).ToArray();
            if (locations.Length != 1) Assert.Fail();
            return locations[0];
        }

        public static INamespaceSymbol LookupNamespaceSymbol(this SemanticModel semanticModel, string namespaceName)
        {
            var root = semanticModel.SyntaxTree.GetRoot();

            var namespaceSyntax = root.ChildNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault(x => x.Name.ToString() == namespaceName);

            if (namespaceSyntax == null) return null;

            return semanticModel.GetDeclaredSymbol(namespaceSyntax) as INamespaceSymbol;
        }

        public static ITypeSymbol LookupTypeSymbol(this SemanticModel semanticModel, string declaringNamespace, string typeName)
        {
            var namespaceSymbol = semanticModel.LookupNamespaceSymbol(declaringNamespace);
            if (namespaceSymbol == null) Assert.Fail($"Namespace not found: {declaringNamespace}");

            return namespaceSymbol.GetMembers(typeName).OfType<ITypeSymbol>().FirstOrDefault();
        }

        public static ImmutableArray<ISymbol> LookupTypeSymbolMembers(this SemanticModel semanticModel, string declaringNamespace, string declaringType, string memberName = null)
        {
            var typeSymbol = semanticModel.LookupTypeSymbol(declaringNamespace, declaringType);
            if (typeSymbol == null) Assert.Fail($"Symbol not found: {declaringNamespace}.{declaringType}.{memberName}");

            return memberName == null ? typeSymbol.GetMembers() : typeSymbol.GetMembers(memberName);
        }
    }
}