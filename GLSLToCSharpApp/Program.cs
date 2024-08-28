using System.Text;
using System.Text.RegularExpressions;

namespace GLSLToCSharpApp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.Write("shaders directory : ");
            string? shadersDirectory = Console.ReadLine();

            if (string.IsNullOrEmpty(shadersDirectory))
            {
                return;
            }

            foreach (var directory in Directory.GetDirectories(shadersDirectory.Replace("\"", "")))
            {
                Save(directory);
            }
        }

        private static void Save(string directoryPath)
        {
            string shaderName = Path.GetFileName(directoryPath);
            string documentPath = Path.Combine(directoryPath, shaderName);

            // Capitalize the first letter of the class name
            string className = UpperFirstChar(shaderName) + "Shader";

            string newCsFilePath = Path.Combine(directoryPath, className + ".cs");

            string content = File.ReadAllText(documentPath + ".vert");
            content += File.ReadAllText(documentPath + ".frag");

            // Parse the GLSL file for uniforms
            var uniforms = ParseUniforms(content);

            // Parse the GLSL file for vertex attributes
            var vertexAttributes = ParseVertexAttributes(content);

            // Update the C# file with the generated class
            UpdateCSharpFile(newCsFilePath, uniforms, vertexAttributes, className, shaderName);
        }

        private static List<Uniform> ParseUniforms(string glslCode)
        {
            var uniforms = new List<Uniform>();
            var regex = new Regex(@"uniform\s+(?<type>\w+)\s+(?<name>\w+)(\[(?<size>\w*)\])?;", RegexOptions.Compiled);
            var matches = regex.Matches(glslCode);

            foreach (Match match in matches)
            {
                var type = match.Groups["type"].Value;
                var name = match.Groups["name"].Value;
                var size = match.Groups["size"].Value;

                uniforms.Add(new Uniform
                {
                    Type = type,
                    Name = name,
                    IsArray = !string.IsNullOrEmpty(size)
                });
            }

            return uniforms;
        }

        private static List<VertexAttribute> ParseVertexAttributes(string glslCode)
        {
            var attributes = new List<VertexAttribute>();
            var regex = new Regex(@"layout\s*\(\s*location\s*=\s*(?<location>\d+)\s*\)\s*in\s+(?<type>\w+)\s+(?<name>\w+);", RegexOptions.Compiled);
            var matches = regex.Matches(glslCode);

            foreach (Match match in matches)
            {
                var location = int.Parse(match.Groups["location"].Value);
                var type = match.Groups["type"].Value;
                var name = match.Groups["name"].Value;

                attributes.Add(new VertexAttribute
                {
                    Location = location,
                    Type = type,
                    Name = name
                });
            }

            return attributes;
        }

        private static void UpdateCSharpFile(string filePath, List<Uniform> uniforms, List<VertexAttribute> attributes, string className, string shaderName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// This file is auto-generated and any change will be overwritten on the next update.");
            sb.AppendLine();
            sb.AppendLine("namespace Kotono.Graphics.Shaders");
            sb.AppendLine("{");
            sb.AppendLine($"    internal partial class {className} : Shader");
            sb.AppendLine("    {");
            sb.AppendLine($"        private {className}() : base(\"{shaderName}\") {{ }}");
            sb.AppendLine();
            sb.AppendLine($"        private static readonly global::System.Lazy<{className}> _instance = new(() => new());");
            sb.AppendLine();
            sb.AppendLine($"        internal static {className} Instance => _instance.Value;");

            int stride = attributes.Select(a => GetGlslTypeSize(a.Type)).Sum();
            int offset = 0;

            foreach (var attribute in attributes)
            {
                sb.AppendLine();

                int size = GetGlslTypeSize(attribute.Type);

                sb.AppendLine($"        private static void Set{UpperFirstChar(attribute.Name)}() => SetVertexAttributeData({attribute.Location}, {GetGlslTypeNumberOfValues(attribute.Type)}, {GetGlslVertexAttribPointerType(attribute.Type)}, {stride}, {offset});");

                offset += size;
            }

            sb.AppendLine();
            sb.AppendLine($"        internal override void SetVertexAttributesData() {{ {string.Concat(attributes.Select(a => $"Set{UpperFirstChar(a.Name)}(); "))}}}");

            foreach (var uniform in uniforms)
            {
                sb.AppendLine();

                if (uniform.IsArray)
                {
                    sb.AppendLine($"        internal void Set{UpperFirstChar(uniform.Name)}({GlslToCSharpType(uniform.Type)}[] {uniform.Name}) {{ for (int i = 0; i < {uniform.Name}.Length; i++) Set{GlslToShaderMethod(uniform.Type)}($\"{uniform.Name}[{{i}}]\", {uniform.Name}[i]); }}");
                }
                else
                {
                    sb.AppendLine($"        internal void Set{UpperFirstChar(uniform.Name)}({GlslToCSharpType(uniform.Type)} {uniform.Name}) => Set{GlslToShaderMethod(uniform.Type)}(\"{uniform.Name}\", {uniform.Name});");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString());
        }

        private static string GlslToCSharpType(string glslType)
        {
            return glslType switch
            {
                "Material" => "global::Kotono.Graphics.Material",
                "vec3" => "global::Kotono.Utils.Coordinates.Vector",
                "vec4" => "global::Kotono.Utils.Color",
                "mat4" => "global::OpenTK.Mathematics.Matrix4",
                "DirectionalLight" => "global::Kotono.Graphics.Objects.Lights.DirectionalLight",
                "PointLight" => "global::Kotono.Graphics.Objects.Lights.PointLight",
                _ => glslType
            };
        }

        private static string GlslToShaderMethod(string glslType)
        {
            return glslType switch
            {
                "int" => "Int",
                "float" => "Float",
                "bool" => "Bool",
                "Material" => "Material",
                "vec3" => "Vector",
                "vec4" => "Color",
                "mat4" => "Matrix4",
                _ => glslType
            };
        }

        private static int GetGlslTypeSize(string glslType)
        {
            return glslType switch
            {
                "int" => sizeof(int),
                "bool" => sizeof(bool),
                "float" => sizeof(float),
                "vec2" => sizeof(float) * 2,
                "vec3" => sizeof(float) * 3,
                "vec4" => sizeof(float) * 4,
                _ => 0
            };
        }

        private static int GetGlslTypeNumberOfValues(string glslType)
        {
            return glslType switch
            {
                "int" => 1,
                "bool" => 1,
                "float" => 1,
                "vec2" => 2,
                "vec3" => 3,
                "vec4" => 4,
                _ => 0
            };
        }

        private static string GetGlslVertexAttribPointerType(string glslType)
        {
            return glslType switch
            {
                "float" => "global::OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float",
                "vec2" => "global::OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float",
                "vec3" => "global::OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float",
                "vec4" => "global::OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float",
                _ => ""
            };
        }

        private static string UpperFirstChar(string value) => char.ToUpper(value[0]) + value[1..];
    }
}