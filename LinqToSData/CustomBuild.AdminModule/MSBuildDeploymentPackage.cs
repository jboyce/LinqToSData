using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.BuildEngine;
using Sage.Platform.Extensibility;
using Sage.Platform.Orm.CodeGen;
using Sage.Platform.Orm.CodeGen.Properties;
using BuildSettings = Sage.Platform.Extensibility.BuildSettings;

namespace CustomBuild.AdminModule
{
    public abstract class MSBuildDeploymentPackage : DeploymentPackageBase
    {
        protected void EnsureLibraries()
        {
            _systemLibraryPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            _libraryPath = Path.Combine(WorkingDirectory.FullName, "assemblies");
            DirectoryInfo lp = new DirectoryInfo(_libraryPath);
            if (!lp.Exists)
                lp.Create();
        }

        protected void AddFileToProject(BuildItemGroup items, string fullPath, string itemType)
        {
            items.AddNewItem(itemType, fullPath);
        }

        string _libraryPath;

        protected string LibraryPath
        {
            get { return _libraryPath; }
        }

        string _systemLibraryPath;

        protected string SystemLibraryPath
        {
            get { return _systemLibraryPath; }
        }

        string _commonAssembliesPath = @"\common\assemblies";

        /// <summary>
        /// Gets or sets the common assemblies path in the virtual file system.
        /// </summary>
        public string CommonAssembliesPath
        {
            get { return _commonAssembliesPath; }
            set { _commonAssembliesPath = value; }
        }

        protected string OutputPath
        {
            get { return Path.Combine(FullProjectDirectory, "bin"); }
        }

        string _assemblyName = "Sage.SalesLogix.Entities";

        public string AssemblyName
        {
            get { return _assemblyName; }
            set { _assemblyName = value; }
        }

        public abstract string ProjectDirectory { get; }

        public string FullProjectDirectory
        {
            get { return Path.Combine(WorkingDirectoryPath, ProjectDirectory); }
        }

        protected override string Rebase(string file)
        {
            string deployPath = Project.CommonPaths["DEPLOYMENTPATH"] + Platform.RootDeploymentPath;
            string relative = file.Substring(FullProjectDirectory.Length + 1);
            return Path.Combine(deployPath, relative);
        }

        protected string RelativePathToDeploymentPath(string relativePath)
        {
            return Path.Combine(Project.CommonPaths["DEPLOYMENTPATH"] + Platform.RootDeploymentPath, relativePath);
        }

        protected virtual void ConfigureProject(Project proj)
        {
            _globalProperties = proj.AddNewPropertyGroup(false);
            _globalProperties.AddNewProperty("OutputType", "Library");
            _globalProperties.AddNewProperty("AssemblyName", AssemblyName);
            _globalProperties.AddNewProperty("OutputPath", OutputPath);
            _globalProperties.AddNewProperty("Optimize", "true");
            _globalProperties.AddNewProperty("NoWarn", "1591,0168");
            _globalProperties.AddNewProperty("DocumentationFile", string.Concat(OutputPath, "\\", AssemblyName, ".XML"));
            proj.AddNewImport(@"$(MSBuildBinPath)\Microsoft.CSharp.targets", String.Empty);

            _references = proj.AddNewItemGroup();
        }

        BuildPropertyGroup _globalProperties;

        public BuildPropertyGroup GlobalProperties
        {
            get { return _globalProperties; }
        }

        BuildItemGroup _references;

        /// <summary>
        /// Gets the build references.  Created after configure project is called.
        /// </summary>
        /// <value>The references.</value>
        protected BuildItemGroup References
        {
            get { return _references; }
        }

        protected BuildItem AddReference(string reference, string hintPath, bool specificVersion, bool copyLocal)
        {
            // Add References
            BuildItem item = References.AddNewItem("Reference", reference);
            FileInfo file = new FileInfo(hintPath);
            if (!file.Exists)
            {
                hintPath = BuildSettings.CurrentBuildSettings.FindAssembly(file.Name);
                if (String.IsNullOrEmpty(hintPath))
                    throw new BuildException(
                        String.Format("Unable to locate assembly {0} in path '{1}' or it's subdirectories.", reference, file.DirectoryName));
            }

            item.SetMetadata("HintPath", hintPath);
            item.SetMetadata("Private", copyLocal.ToString());
            item.SetMetadata("SpecificVersion", specificVersion.ToString());

            return item;
        }

        protected override bool AutoRemoveWorkingFolder
        {
            get { return false; }
        }

        protected override string WorkingPath
        {
            get { return BuildSettings.CurrentBuildSettings.SolutionFolder; }
        }
    }

    internal class BuildItemFileInfo
    {
        public BaseEntityGenerator Generator { get; private set; }
        public string Path { get; private set; }

        public BuildItemFileInfo(BaseEntityGenerator generator, string path)
        {
            Generator = generator;
            Path = path;
        }
    }
}