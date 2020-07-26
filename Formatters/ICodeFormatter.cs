using System;
using System.Collections.Generic;
using System.Text;

namespace Dprint.Plugins.Roslyn.Formatters
{
    public interface ICodeFormatter
    {
        public bool ShouldFormat(string filePath);
        public string FormatText(string text);
    }
}
