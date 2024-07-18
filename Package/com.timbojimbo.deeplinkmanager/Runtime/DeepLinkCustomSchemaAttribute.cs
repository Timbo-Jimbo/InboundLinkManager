using System;

namespace TimboJimbo.DeepLinkManager
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class DeepLinkCustomSchemaAttribute : Attribute
    {
        public readonly string Schema;

        public DeepLinkCustomSchemaAttribute(string schema)
        {
            Schema = schema;
        }
    }
}