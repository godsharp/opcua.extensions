﻿using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;
using GodSharp.Extensions.Opc.Ua.Types;
using System.Collections.Generic;
using System.IO;
using System;
using static GodSharp.Extensions.Opc.Ua.CodeGenerator.FieldMetadata;

namespace GodSharp.Extensions.Opc.Ua.CodeGenerator
{
    public class CodeGeneratorTool
    {
        public static readonly string Suffix = ".uamgen.cs";
        public static readonly string Header =
@"//------------------------------------------------------------------------------
// <auto-generated>
//     Generated by {0} generator.
//     Source: {1}
// </auto-generated>
//------------------------------------------------------------------------------
";

        public static string AttributeName { get; private set; } = "ComplexObjectGenerator";
        public static string AttributeFullName { get; private set; } = "GodSharp.Extensions.Opc.Ua.Types.ComplexObjectGeneratorAttribute";

        public static bool CheckClassModifiers(SyntaxTokenList modifiers)
        {
            if (modifiers.Any(SyntaxKind.PrivateKeyword)) return false;
            if (modifiers.Any(SyntaxKind.AbstractKeyword)) return false;
            if (!modifiers.Any(SyntaxKind.PartialKeyword)) return false;
            return true;
        }

        public static bool CheckFieldModifiers(SyntaxTokenList modifiers)
        {
            if (modifiers.Any(SyntaxKind.PublicKeyword)) return true;
            if (modifiers.Any(SyntaxKind.InternalKeyword)) return true;
            return false;
        }

        public static bool CheckAttributes(SyntaxList<AttributeListSyntax> attributes)
        {
            if (attributes.Count == 0) return false;

            return attributes
                .Any(a => a?.Attributes
                    .Any(x => HasAttributeName(x?.Name?.ToString())) == true
                );
        }

        public static bool HasAttributeName(string name)
            => HasAttribute(AttributeName, name);

        private static bool HasAttribute(string src, string dst)
        {
            return !string.IsNullOrWhiteSpace(dst) && src.Equals(dst);
        }

        /// <summary>
        /// Validate SyntaxNode is a class and has attribute <seealso cref="ComplexObjectGeneratorAttribute"/>.
        /// </summary>
        /// <param name="syntaxNode"></param>
        /// <returns></returns>
        public static ClassDeclarationSyntax ValidateSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is not ClassDeclarationSyntax cls) return null;
            return CheckClassModifiers(cls.Modifiers) && CheckAttributes(cls.AttributeLists) ? cls : null;
        }

        /// <summary>
        /// Build type with encode and decode method
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="type"></param>
        public static void BuildType(StringBuilder builder, CodeGeneratorMetadataType type)
        {

            builder.AppendLine($"\t{type.Accessibility} class {type.ClassName} : ComplexObject");
            builder.AppendLine("\t{");

            AppendField(builder, type.ObjectType);
            
            if (type.Fields?.Count>0)
            {
                BuildMethod(builder, type.ObjectType, type.MethodType, type.Fields.ToArray());
            }
            builder.AppendLine("\t}");
        }

        /// <summary>
        /// Append fields for special type.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="objectType"></param>
        private static void AppendField(StringBuilder builder, ComplexObjectType objectType)
        {
            bool append = true;
            switch (objectType)
            {
                case ComplexObjectType.SwitchField:
                    builder.AppendLine("\t\tpublic uint SwitchField;");
                    break;
                case ComplexObjectType.OptionalField:
                    builder.AppendLine("\t\tpublic uint EncodingMask;");
                    break;
                default:
                    append = false;
                    break;
            }

            if (append) builder.AppendLine();
        }

        /// <summary>
        /// Build encode and decode method
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="objectType"></param>
        /// <param name="methodType"></param>
        /// <param name="fields"></param>
        public static void BuildMethod(StringBuilder builder, ComplexObjectType objectType, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            BuildEncode(builder, objectType,methodType, fields);
            builder.AppendLine();
            BuildDecode(builder, objectType,methodType, fields);
            if(objectType== ComplexObjectType.OptionalField)
            {
                builder.AppendLine();
                BuildWithOptionalFields(builder, fields);
            }
        }

        /// <summary>
        /// Build decode method
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="objectType"></param>
        /// <param name="methodType"></param>
        /// <param name="fields"></param>
        public static void BuildDecode(StringBuilder builder, ComplexObjectType objectType, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            builder.AppendLine("\t\tpublic override void Decode(IDecoder decoder)");
            builder.AppendLine("\t\t{");
            builder.AppendLine("\t\t\tbase.Decode(decoder);");

            switch (objectType)
            {
                case ComplexObjectType.EncodeableObject:
                    BuildDecodeGeneric(builder, methodType, fields);
                    break;
                case ComplexObjectType.SwitchField:
                    BuildDecodeSwitchFields(builder, methodType, fields);
                    break;
                case ComplexObjectType.OptionalField:
                    BuildDecodeOptionalFields(builder, methodType, fields);
                    break;
                default:
                    break;
            }

            builder.AppendLine("\t\t}");
        }

        /// <summary>
        /// Build encode method
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="objectType"></param>
        /// <param name="methodType"></param>
        /// <param name="fields"></param>
        public static void BuildEncode(StringBuilder builder, ComplexObjectType objectType, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            builder.AppendLine("\t\tpublic override void Encode(IEncoder encoder)");
            builder.AppendLine("\t\t{");
            builder.AppendLine("\t\t\tbase.Encode(encoder);");

            switch (objectType)
            {
                case ComplexObjectType.EncodeableObject:
                    BuildEncodeGeneric(builder, methodType, fields);
                    break;
                case ComplexObjectType.SwitchField:
                    BuildEncodeSwitchFields(builder, methodType, fields);
                    break;
                case ComplexObjectType.OptionalField:
                    BuildEncodeOptionalFields(builder, methodType, fields);
                    break;
                default:
                    break;
            }

            builder.AppendLine("\t\t}");
        }

        /// <summary>
        /// Parse source code to <seealso cref="CodeGeneratorMetadata"/>
        /// </summary>
        /// <param name="text">the source code</param>
        /// <param name="file">the source file path</param>
        /// <returns></returns>
        public static CodeGeneratorMetadata ParseText(string text,string file=null)
        {
            CodeGeneratorMetadata metadata = new() { SourceFile = file };

            var tree = SyntaxFactory.ParseSyntaxTree(text);
            var root = tree.GetCompilationUnitRoot();

            List<ClassDeclarationSyntax> list = new();
            foreach (var member in root.Members)
            {
                if (member is not NamespaceDeclarationSyntax @namespace) continue;
                foreach (var item in @namespace.Members)
                {
                    if (ValidateSyntaxNode(item) is { } cls)
                    {
                        list.Add(cls);
                    }
                }
            }

            if (list.Count == 0) return null;
            byte usings = 0;
            bool optional = false;
            for (var i = 0; i < list.Count; i++)
            {
                var cls = list[i];
                var attribute = cls
                    .AttributeLists
                    .SelectMany(x => x.Attributes)
                    .FirstOrDefault(x => HasAttributeName(x.Name.ToString()));
                var methodTypeAttribute = attribute
                    ?.ArgumentList
                    ?.Arguments
                    .Select(x => x.ToString())
                    .FirstOrDefault(x => x.StartsWith("EncodingMethodType."));
                var objectTypeAttribute = attribute
                    ?.ArgumentList
                    ?.Arguments
                    .Select(x => x.ToString())
                    .FirstOrDefault(x => x.StartsWith("ComplexObjectType."));

                if (!Enum.TryParse(methodTypeAttribute?.Split('.')[1], out EncodingMethodType methodType))
                {
                    methodType = EncodingMethodType.Factory;
                }

                if (!Enum.TryParse(objectTypeAttribute?.Split('.')[1], out ComplexObjectType objectType))
                {
                    objectType = ComplexObjectType.EncodeableObject;
                }
                
                if (!optional && objectType== ComplexObjectType.OptionalField) optional = true;
                if (methodType == EncodingMethodType.Factory && usings == 0) usings = 1;
                if (methodType == EncodingMethodType.Extension && usings == 0) usings = 2;
                if ((methodType == EncodingMethodType.Factory && usings == 2) ||
                    (methodType == EncodingMethodType.Extension && usings == 1)) usings = 3;

                List<FieldMetadata> fields = new();
                foreach (var member in cls.Members)
                {
                    if (member is not FieldDeclarationSyntax field || !CheckFieldModifiers(field.Modifiers)) continue;
                    SwitchFieldData switchFieldAttribute = null;
                    OptionalFieldData optionalFieldAttribute = null;
                    FieldType fieldType = FieldType.Generic;

                    switch (objectType)
                    {
                        case ComplexObjectType.SwitchField:
                            {
                                fieldType = FieldType.Switch;
                                // SwitchFieldAttribute
                                var attr = field
                                    ?.AttributeLists
                                    .Where(x=>x.Attributes!=null)
                                    .SelectMany(x => x.Attributes)
                                    .FirstOrDefault(x => HasAttribute(x.Name.ToString(), "SwitchField"));

                                if(attr==null)
                                {
                                    throw new NotImplementedException($"The type {cls.Identifier} field '{field}' not found SwitchField attribute");
                                }

                                var val = attr
                                    ?.ArgumentList
                                    .Arguments
                                    .Select(x => x.ToString())
                                    .ToArray();
                                switchFieldAttribute = new SwitchFieldData(val);
                            }
                            break;
                        case ComplexObjectType.OptionalField:
                            {
                                // OptionalFieldAttribute
                                var attr = field
                                    .AttributeLists
                                    .SelectMany(x => x.Attributes)
                                    .FirstOrDefault(x => HasAttribute(x.Name.ToString(), "OptionalField"));

                                if(attr!=null)
                                {
                                    fieldType = FieldType.Optional;
                                    var val = attr
                                        ?.ArgumentList
                                        .Arguments
                                        .Select(x => x.ToString())
                                        .FirstOrDefault();

                                    optionalFieldAttribute = new OptionalFieldData(val);
                                }
                            }
                            break;
                        default:
                            break;
                    }

                    foreach (var variable in field.Declaration.Variables)
                    {
                        FieldMetadata fieldMetadata = new(variable.Identifier.ToString(), fieldType)
                        {
                            SwitchField = switchFieldAttribute,
                            OptionalField = optionalFieldAttribute
                        };
                        fields.Add(fieldMetadata);
                    }
                }
                if (fields.Count == 0) continue;

                CodeGeneratorMetadataType metadataType = new()
                {
                    Accessibility = cls.Modifiers.ToString(),
                    ClassName = cls.Identifier.ToString(),
                    ObjectType= objectType,
                    MethodType = methodType,
                    Fields = fields,
                };

                metadata.Types.Add(metadataType);
            }

            root = root
                .AddUsings(
                    SyntaxFactory
                        .UsingDirective(SyntaxFactory.ParseName("GodSharp.Extensions.Opc.Ua.Types"))
                        .NormalizeWhitespace(),
                    SyntaxFactory
                        .UsingDirective(SyntaxFactory.ParseName("Opc.Ua"))
                        .NormalizeWhitespace()
                );

            if (usings >=2)
            {
                root = root
                    .AddUsings(
                        SyntaxFactory
                            .UsingDirective(SyntaxFactory.ParseName("GodSharp.Extensions.Opc.Ua.Types.Encodings"))
                            .NormalizeWhitespace()
                    );
            }

            if (usings !=2)
            {
                root = root
                    .AddUsings(
                        SyntaxFactory
                            .UsingDirective(
                                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                                null,
                                SyntaxFactory.ParseName("GodSharp.Extensions.Opc.Ua.Types.Encodings.EncodingFactory")
                            )
                            .NormalizeWhitespace()
                    );
            }

            if (optional)
            {
                root = root
                    .AddUsings(
                        SyntaxFactory
                            .UsingDirective(SyntaxFactory.ParseName("GodSharp.Extensions.Opc.Ua.Utilities"))
                            .NormalizeWhitespace(),
                        SyntaxFactory
                            .UsingDirective(SyntaxFactory.ParseName("System.Linq.Expressions"))
                            .NormalizeWhitespace(),
                        SyntaxFactory
                            .UsingDirective(SyntaxFactory.ParseName("System"))
                            .NormalizeWhitespace()
                    );
            }

            metadata.Usings = root.Usings.Select(x => x.ToString()).Distinct().ToList();
            metadata.Namespace = root
                .DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault()
                ?.Name
                .ToString();

            if (!string.IsNullOrWhiteSpace(file))
            {
                metadata.OutputFile = Path.Combine(
                        Path.GetDirectoryName(file)!,
                        $"{Path.GetFileNameWithoutExtension(file)}{Suffix}"
                    ); 
            }

            return metadata;
        }

        /// <summary>
        /// Parse file to <seealso cref="CodeGeneratorMetadata"/>
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static CodeGeneratorMetadata ParseFile(string file)
            => ParseText(File.ReadAllText(file), file);

        private static void BuildDecodeGeneric(StringBuilder builder, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < fields.Length; i++)
            {
                switch (methodType)
                {
                    case EncodingMethodType.Factory:
                        builder.AppendLine($"\t\t\tEncoding.Read(decoder, ref {fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                    case EncodingMethodType.Extension:
                        builder.AppendLine($"\t\t\tdecoder.Read(ref {fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                }
            }
        }

        private static void BuildEncodeGeneric(StringBuilder builder, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < fields.Length; i++)
            {
                switch (methodType)
                {
                    case EncodingMethodType.Factory:
                        builder.AppendLine($"\t\t\tEncoding.Write(encoder, {fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                    case EncodingMethodType.Extension:
                        builder.AppendLine($"\t\t\tencoder.Write({fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                }
            }
        }

        private static void BuildDecodeSwitchFields(StringBuilder builder, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            builder.AppendLine("\t\t\tSwitchField = decoder.ReadUInt32(\"SwitchField\");");
            builder.AppendLine("\t\t\tswitch (SwitchField)");
            builder.AppendLine("\t\t\t{");
            for (int i = 0; i < fields.Length; i++)
            {
                foreach (var item in fields[i].SwitchField.Values)
                {
                    builder.AppendLine($"\t\t\t\tcase {item}:");
                }
                switch (methodType)
                {
                    case EncodingMethodType.Factory:
                        builder.AppendLine($"\t\t\t\t\tEncoding.Read(decoder,ref {fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                    case EncodingMethodType.Extension:
                        builder.AppendLine($"\t\t\t\t\tdecoder.Read(ref {fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                }
                builder.AppendLine("\t\t\t\t\tbreak;");
            }
            builder.AppendLine("\t\t\t\tdefault:");
            builder.AppendLine("\t\t\t\t\tbreak;");
            builder.AppendLine("\t\t\t}");
        }

        private static void BuildEncodeSwitchFields(StringBuilder builder, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            builder.AppendLine("\t\t\tencoder.WriteUInt32(\"SwitchField\",SwitchField);");
            builder.AppendLine("\t\t\tswitch (SwitchField)");
            builder.AppendLine("\t\t\t{");
            for (int i = 0; i < fields.Length; i++)
            {
                foreach (var item in fields[i].SwitchField.Values)
                {
                    builder.AppendLine($"\t\t\t\tcase {item}:");
                }
                switch (methodType)
                {
                    case EncodingMethodType.Factory:
                        builder.AppendLine($"\t\t\t\t\tEncoding.Write(encoder, {fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                    case EncodingMethodType.Extension:
                        builder.AppendLine($"\t\t\t\t\tencoder.Write({fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                }
                builder.AppendLine("\t\t\t\t\tbreak;");
            }
            builder.AppendLine("\t\t\t\tdefault:");
            builder.AppendLine("\t\t\t\t\tbreak;");
            builder.AppendLine("\t\t\t}");
        }

        private static void BuildDecodeOptionalFields(StringBuilder builder, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            builder.AppendLine("\t\t\tEncodingMask = decoder.ReadUInt32(\"EncodingMask\");");

            for (int i = 0; i < fields.Length; i++)
            {
                var attribute = fields[i].OptionalField;

                if (attribute == null)
                {
                    switch (methodType)
                    {
                        case EncodingMethodType.Factory:
                            builder.AppendLine($"\t\t\tEncoding.Read(decoder,ref {fields[i].Name}, nameof({fields[i].Name}));");
                            break;
                        case EncodingMethodType.Extension:
                            builder.AppendLine($"\t\t\tdecoder.Read(ref {fields[i].Name}, nameof({fields[i].Name}));");
                            break;
                    }
                    continue;
                }

                builder.AppendLine($"\t\t\tif (({attribute.Mask} & EncodingMask) != 0)");
                builder.AppendLine("\t\t\t{");
                switch (methodType)
                {
                    case EncodingMethodType.Factory:
                        builder.AppendLine($"\t\t\t\tEncoding.Read(decoder,ref {fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                    case EncodingMethodType.Extension:
                        builder.AppendLine($"\t\t\t\tdecoder.Read(ref {fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                }
                builder.AppendLine("\t\t\t}");
            }
        }

        private static void BuildEncodeOptionalFields(StringBuilder builder, EncodingMethodType methodType, params FieldMetadata[] fields)
        {
            builder.AppendLine("\t\t\tencoder.WriteUInt32(\"EncodingMask\",EncodingMask);");

            for (int i = 0; i < fields.Length; i++)
            {
                OptionalFieldData attribute = fields[i].OptionalField;

                if (attribute == null)
                {
                    switch (methodType)
                    {
                        case EncodingMethodType.Factory:
                            builder.AppendLine($"\t\t\tEncoding.Write(encoder,{fields[i].Name}, nameof({fields[i].Name}));");
                            break;
                        case EncodingMethodType.Extension:
                            builder.AppendLine($"\t\t\tencoder.Write({fields[i].Name}, nameof({fields[i].Name}));");
                            break;
                    }
                    continue;
                }

                builder.AppendLine($"\t\t\tif (({attribute.Mask} & EncodingMask) != 0)");
                builder.AppendLine("\t\t\t{");
                switch (methodType)
                {
                    case EncodingMethodType.Factory:
                        builder.AppendLine($"\t\t\t\tEncoding.Write(encoder,{fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                    case EncodingMethodType.Extension:
                        builder.AppendLine($"\t\t\t\tencoder.Write({fields[i].Name}, nameof({fields[i].Name}));");
                        break;
                }
                builder.AppendLine("\t\t\t}");
            }
        }

        private static void BuildWithOptionalFields(StringBuilder builder, params FieldMetadata[] fields)
        {
            builder.AppendLine("\t\tpublic OptionalFields ResetMask()");
            builder.AppendLine("\t\t{");
            builder.AppendLine("\t\t\tEncodingMask = 0;");
            builder.AppendLine("\t\t\treturn this;");
            builder.AppendLine("\t\t}");
            builder.AppendLine();
            builder.AppendLine("\t\tpublic OptionalFields WithOptionalField<TMember>(Expression<Func<OptionalFields, TMember>> predicate) => WithOptionalField(ExpressionHelper.GetMemberName(predicate.Body));");
            builder.AppendLine();
            builder.AppendLine("\t\tpublic OptionalFields WithOptionalField(string field)");
            builder.AppendLine("\t\t{");
            builder.AppendLine("\t\t\tswitch (field)");
            builder.AppendLine("\t\t\t{");

            for (int i = 0; i < fields.Length; i++)
            {
                OptionalFieldData attribute = fields[i].OptionalField;

                if (attribute == null) continue;

                builder.AppendLine($"\t\t\t\tcase \"{fields[i].Name}\":");
                builder.AppendLine($"\t\t\t\t\tEncodingMask |= {attribute.Mask};");
                builder.AppendLine("\t\t\t\t\tbreak;");
            }
            builder.AppendLine("\t\t\t}");
            builder.AppendLine("\t\t\treturn this;");
            builder.AppendLine("\t\t}");
        }

        //private static uint GetValue(string str)
        //{
        //    uint result;

        //    if (str.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        //    {
        //        result = Convert.ToUInt32(str.Substring(2), 2);
        //    }
        //    else if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        //    {
        //        result = Convert.ToUInt32(str.Substring(2), 16);
        //    }
        //    else
        //    {
        //        result = Convert.ToUInt32(str);
        //    }

        //    return result;
        //}

        //public static ComplexObjectMetadata Get(Type type)
        //{
        //    var mt = GetEncodingMethodType(type);
        //    if (mt == null) return null;

        //    return new ComplexObjectMetadata()
        //    {
        //        ClassType = type,
        //        Accessibility = type.IsPublic ? "public" : "public",
        //        MethodType = mt.Value,
        //        Fields = type.GetFields()
        //    };
        //}

        //public static EncodingMethodType? GetEncodingMethodType(Type type)
        //{
        //    var attributes = type.GetCustomAttributes(typeof(ComplexObjectGeneratorAttribute), false);
        //    if(attributes==null||attributes.Length==0) return null;
        //    return (attributes.First() as ComplexObjectGeneratorAttribute).MethodType;
        //}
    }
}
