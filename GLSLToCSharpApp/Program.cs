using System.Text.RegularExpressions;
using System.Text;

namespace GLSLToCSharpApp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.Write("document path : ");
            string? documentPath = Console.ReadLine();

            if (!string.IsNullOrEmpty(documentPath))
            { 
                Save(documentPath.Replace("\"", ""));
            }
        }

        private static void Save(string documentPath)
        {
            string extension = Path.GetExtension(documentPath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(documentPath);

            if (extension.Equals(".vert", StringComparison.OrdinalIgnoreCase)
             || extension.Equals(".frag", StringComparison.OrdinalIgnoreCase))
            {
                // Capitalize the first letter of the class name
                string className = char.ToUpper(fileNameWithoutExtension[0]) + fileNameWithoutExtension[1..] + "Shader";

                string newCsFilePath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, className + ".cs");

                // Read file content
                string content = File.ReadAllText(documentPath);

                if (extension.Equals(".vert", StringComparison.OrdinalIgnoreCase))
                {
                    content += File.ReadAllText(documentPath.Replace(".vert", ".frag"));
                }
                else
                {
                    content += File.ReadAllText(documentPath.Replace(".frag", ".vert"));
                }

                // Parse the GLSL file for uniforms
                var uniforms = ParseUniforms(content);

                // Update the C# file with the generated class
                UpdateCSharpFile(newCsFilePath, uniforms, className, fileNameWithoutExtension);
            }
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

                uniforms.Add(new Uniform { Type = type, Name = name, IsArray = !string.IsNullOrEmpty(size) });
            }

            return uniforms;
        }

        private static void UpdateCSharpFile(string filePath, List<Uniform> uniforms, string className, string fileNameWithoutExtension)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// This file is auto-generated and any change will be overwritten on the next update.");
            sb.AppendLine();
            sb.AppendLine("namespace Kotono.Graphics.Shaders");
            sb.AppendLine("{");
            sb.AppendLine($"    internal partial class {className} : Shader");
            sb.AppendLine("    {");
            sb.AppendLine($"        private {className}() : base(\"{fileNameWithoutExtension}\") {{ }}");
            sb.AppendLine();
            sb.AppendLine($"        private static readonly global::System.Lazy<{className}> _instance = new(() => new());");
            sb.AppendLine();
            sb.AppendLine($"        internal static {className} Instance => _instance.Value;");

            foreach (var uniform in uniforms)
            {
                string uniformName = char.ToUpper(uniform.Name[0]) + uniform.Name[1..];

                sb.AppendLine();

                if (uniform.IsArray)
                {
                    sb.AppendLine($"        internal void Set{uniformName}({GlslToCSharpType(uniform.Type)}[] {uniform.Name}) {{ for (int i = 0; i < {uniform.Name}.Length; i++) Set{GlslToShaderMethod(uniform.Type)}($\"{uniform.Name}[{{i}}]\", {uniform.Name}[i]); }}");
                }
                else
                {
                    sb.AppendLine($"        internal void Set{uniformName}({GlslToCSharpType(uniform.Type)} {uniform.Name}) => Set{GlslToShaderMethod(uniform.Type)}(\"{uniform.Name}\", {uniform.Name});");
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
    }
}