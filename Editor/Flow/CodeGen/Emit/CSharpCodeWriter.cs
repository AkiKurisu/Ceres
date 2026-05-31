using System;
using System.IO;
using System.Text;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class CSharpCodeWriter
    {
        private readonly StringBuilder _builder = new();

        private int _indent;

        public void Indent()
        {
            _indent++;
        }

        public void Unindent()
        {
            _indent = Math.Max(0, _indent - 1);
        }

        public void WriteLine()
        {
            _builder.AppendLine();
        }

        public void WriteLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                _builder.AppendLine();
                return;
            }

            _builder.Append(new string(' ', _indent * 4));
            _builder.AppendLine(line);
        }

        public void WriteRawBlock(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            using var reader = new StringReader(code);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                {
                    _builder.AppendLine();
                }
                else
                {
                    WriteLine(line);
                }
            }
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}
