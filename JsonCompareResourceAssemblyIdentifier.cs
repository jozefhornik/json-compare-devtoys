using DevToys.Api;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonCompareDevToy
{
    [Export(typeof(IResourceAssemblyIdentifier))]
    [Name(nameof(JsonCompareResourceAssemblyIdentifier))]
    public sealed class JsonCompareResourceAssemblyIdentifier : IResourceAssemblyIdentifier
    {
        public ValueTask<FontDefinition[]> GetFontDefinitionsAsync()
        {
            return ValueTask.FromResult<FontDefinition[]>([]);
        }
    }
}
