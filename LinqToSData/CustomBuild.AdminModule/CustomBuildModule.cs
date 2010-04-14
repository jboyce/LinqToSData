using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sage.Platform.Application;
using Sage.Platform.Extensibility;
using Sage.Platform.Orm.CodeGen;
using Sage.Platform.Application.UI;
using Sage.Platform.Extensibility.Services;
using Sage.Platform;
using Sage.Platform.Configuration;
using Sage.Platform.Application.Modularity;
using System.ComponentModel;

namespace CustomBuild.AdminModule
{
    [ModuleDependency("Sage.Platform.AdminModule")]
    public class CustomBuildModule : ModuleInit<UIWorkItem>, IModuleConfigurationProvider
    {
        private readonly string _buildCommandUrl = "cmd://PlatformAdminModule/BuildPackage/" + typeof(SDataClientEntityDeploymentPackage).GUID;
        private bool _buildCommandInitialized = false;

        protected override void Load()
        {
            base.Load();
            RegisterSDataEntityDeploymentPackage();
            ParentWorkItem.Services.Get<IModuleLoaderService>().ModuleLoaded += CustomBuildModule_ModuleLoaded;
        }

        void CustomBuildModule_ModuleLoaded(object sender, ItemEventArgs<IModuleInfo> e)
        {
            if (!_buildCommandInitialized)
            {
                var platformAdminModule = ApplicationContext.Current.Modules.Get<Sage.Platform.AdminModule.AdminModuleInit>();
                if (platformAdminModule != null)
                {
                    ModuleWorkItem.Commands[_buildCommandUrl].ExecuteAction += platformAdminModule.BuildPackageCommand;                
                    _buildCommandInitialized = true;
                }
            }
        }

        private void RegisterSDataEntityDeploymentPackage()
        {
            var service = (Platforms)ExtensionManager.Default.GetService(typeof(Platforms));

            RegisteredPlatform platform;
            service.TryGetPlatform(PlatformGuids.WebGuid, out platform);
            platform.DeploymentPackages.RegisterDeploymentPackage(typeof(SDataClientEntityDeploymentPackage));
        }

        private static UIElementConfiguration CreateMenuItemElement(string uri, string text, string commandUri)
        {
            var UIElement = new UIElementConfiguration();
            UIElement.Uri = uri;
            UIElement.Properties = new PropertyConfigurationCollection();
            UIElement.Properties.Add(new PropertyConfiguration("Text", text));
            UIElement.TypeName = "ToolStripMenuItem";
            UIElement.Command = commandUri;

            return UIElement;
        }

        #region IModuleConfigurationProvider Members

        ModuleConfiguration IModuleConfigurationProvider.GetConfiguration()
        {
            var moduleConfig = new ModuleConfiguration();
            moduleConfig.UIElements = new List<object>();

            var buildPackagesMenu = new UIExtensionSiteConfiguration();
            buildPackagesMenu.Uri = "mnu://MainMenu/Build/Packages";
            buildPackagesMenu.ChildElements = new ChildElementCollection();

            var attrib = (DisplayNameAttribute)Attribute.GetCustomAttribute(
                typeof(SDataClientEntityDeploymentPackage), 
                typeof(DisplayNameAttribute));
            string menuText = attrib.DisplayName;

            UIElementConfiguration packageItem = CreateMenuItemElement(
                "mnu://MainMenu/Build/Packages/" + typeof(SDataClientEntityDeploymentPackage).GUID,
                menuText,
                _buildCommandUrl);
            buildPackagesMenu.ChildElements.Add(packageItem);            

            moduleConfig.UIElements.Add(buildPackagesMenu);
            
            return moduleConfig;
        }

        #endregion
    }
}
