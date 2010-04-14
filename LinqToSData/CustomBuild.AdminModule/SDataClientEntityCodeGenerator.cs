using System;
using Sage.Platform.Application;
using Sage.Platform.Extensibility;
using Sage.Platform.Orm.CodeGen;
using Sage.Platform.Orm.Entities;
using Sage.Platform.Orm.Services;

namespace CustomBuild.AdminModule
{
    public class SDataClientEntityCodeGenerator : BaseTemplateGenerator
    {
        private ISystemDataTypeToClrMappingCatalog _clrCatalog;

        public override void Initialize(string workingPath)
        {
            base.Initialize(workingPath);
            _clrCatalog = ApplicationContext.Current.Services.Get<ISystemDataTypeToClrMappingCatalog>(true);
        }

        public override string MSBuildItemType
        {
            get { return "Compile"; }
        }

        public override string FormatFileName(string name)
        {
            return String.Format("{0}.cs", name);
        }

        public string ToClrType(OrmEntityProperty prop)
        {
            return _clrCatalog.TryGetDataTypeCSharp(prop);
        }

        protected override CodeTemplate GetOutputTemplate(OrmEntity entity)
        {
            var model = (OrmModel)entity.Model;
            var templates = model.GetCodeTemplates(null, "SDataClientEntity");
            if (templates.Count > 0)
                return templates[0];

            throw new BuildException("Missing sdata code generation template");
        }

    }
}