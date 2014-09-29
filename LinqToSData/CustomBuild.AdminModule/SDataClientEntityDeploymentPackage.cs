using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Sage.Platform;
using Sage.Platform.Application.Services;
using Sage.Platform.Configuration.Properties;
using Sage.Platform.Extensibility;
using Sage.Platform.Extensibility.Services;
using Sage.Platform.Extensibility.Utility;
using Sage.Platform.FileSystem;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Orm.CodeGen;
using Sage.Platform.Orm.CodeGen.Localization;
using System;
using System.Runtime.InteropServices;
using Sage.Platform.Orm.Entities;
using Sage.Platform.Projects;
using Sage.Platform.Threading;
using Sage.Platform.Extensibility.Interfaces;
using System.ComponentModel;
using Microsoft.Build.BuildEngine;

namespace CustomBuild.AdminModule
{
    [Guid("D8E19731-02FC-43f7-A89B-49BE02EE78F3")]
    [DisplayName("SData Client Entities")]
    public class SDataClientEntityDeploymentPackage : MSBuildDeploymentPackage
    {
        public const string c_AssemblyName = "Sage.SData.Client.Entities.dll";

        public override string ProjectDirectory
        {
            get { return "SDataEntities"; }
        }

        protected override void GenerateInternal(OperationStatus op, BuildType buildType)
        {
            if (OrmPackages.Length == 0) return;

            EnsureLibraries();

            IFileInfo projectInfoPath = FileSystem.GetFileInfo(Path.Combine(FullProjectDirectory, "project.info.xml"));
            ProjectInfo local = ProjectInfo.Load(projectInfoPath);
            if (local == null || local.InstanceId != Project.InstanceId)
            {
                if (buildType == BuildType.Build)
                    Logging.BuildLog.Info("Full build required");

                buildType = BuildType.BuildAll;
                local = new ProjectInfo { InstanceId = Project.InstanceId };

                if (projectInfoPath.Directory.Exists)
                {
                    foreach (var file in projectInfoPath.Directory.GetFiles())
                    {
                        file.Delete();
                    }
                }
            }

            CopyEntityInterfacesAssembly();

            string path = FullProjectDirectory;

            var generatorList = new List<BaseEntityGenerator>();

            // generate entities
            BaseEntityGenerator baseGen = new SDataClientEntityCodeGenerator();
            baseGen.Initialize(WorkingDirectoryPath);
            generatorList.Add(baseGen);

            // Ensure all paths are created
            foreach (var gen in generatorList)
            {
                string relPath = Path.Combine(path, gen.RelativePath);
                if (!Directory.Exists(relPath))
                    Directory.CreateDirectory(relPath);
            }

            var model = (OrmModel)OrmPackages[0].Model;

            var engine = new Engine { DefaultToolsVersion = "3.5" };
            bool atLeastOneFileWasGenerated = false;
            var buildItemFiles = new List<BuildItemFileInfo>();

            foreach (var entity in OrmEntity.GetAll(model.Project).Where(item => item.Generate && item.GenerateSDataFeed))
            {
                if (!op.CanContinue)
                    return;

                foreach (var generator in generatorList)
                {
                    string fullPath = Path.Combine(Path.Combine(path, generator.RelativePath), generator.FormatFileName(entity.Name));
                    var fi = new FileInfo(fullPath);

                    if (BuildHelper.IsEntityNewer(buildType, fi.LastWriteTimeUtc, entity))
                    {
                        using (var writer = new StreamWriter(fullPath))
                        {
                            generator.Generate(entity, writer);
                        }
                        atLeastOneFileWasGenerated = true;
                    }

                    buildItemFiles.Add(new BuildItemFileInfo(generator, fullPath));
                }
            }

            if (atLeastOneFileWasGenerated)
            {
                BuildProject(buildItemFiles, engine);
            }

            if (op.CanContinue)
            {
                local.Save(projectInfoPath);
            }
        }

        /// <summary>
        /// Copies the entity interfaces assembly and it's associated satellite assemblies.
        /// </summary>
        private void CopyEntityInterfacesAssembly()
        {
            var service = (Platforms)ExtensionManager.Default.GetService(typeof(Platforms));

            RegisteredPlatform commonPlatform;
            if (service.TryGetPlatform(PlatformGuids.CommonGuid, out commonPlatform))
            {
                try
                {
                    string srcPath = Project.CommonPaths["DEPLOYMENTPATH"] + commonPlatform.Platform.RootDeploymentPath + "\\bin\\" + "Sage.Entity.Interfaces.dll";
                    string dstPath = Path.Combine(LibraryPath, "Sage.Entity.Interfaces.dll");

                    FSFile.Copy(Project.Drive.GetFileInfo(srcPath), FileSystem.GetFileInfo(dstPath), true);
                }
                catch (Exception ex)
                {
                    // couldn't copy from VFS so try local output folder
                    if ((ex.InnerException is InvalidOperationException && ex.Source == "Sage.Platform.VirtualFileSystem") ||
                        ex is DirectoryNotFoundException)
                    {
                        string path = Sage.Platform.Extensibility.BuildSettings.CurrentBuildSettings.SolutionFolder;
                        path = Path.Combine(path, "interfaces\\bin\\sage.entity.interfaces.dll");
                        if (File.Exists(path))
                            File.Copy(path, Path.Combine(LibraryPath, "Sage.Entity.Interfaces.dll"), true);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private void BuildProject(IEnumerable<BuildItemFileInfo> buildItemFiles, Engine engine)
        {
            Microsoft.Build.BuildEngine.Project proj = engine.CreateNewProject();

            engine.RegisterLogger(new Log4NetMSBuildLogger(engine, proj));
            engine.RegisterLogger(new BuildLogMSBuildLogger(engine, proj));
            AssemblyName = Path.GetFileNameWithoutExtension(c_AssemblyName);
            proj.FullFileName = Path.Combine(LibraryPath, String.Format("{0}.csproj", AssemblyName));
            ConfigureProject(proj);

            BuildItemGroup items = proj.AddNewItemGroup();

            foreach (BuildItemFileInfo buildItemFile in buildItemFiles)
            {
                AddFileToProject(items, buildItemFile.Path, buildItemFile.Generator.MSBuildItemType);

                if (buildItemFile.Generator.DeployFile)
                    AddOutputFile(buildItemFile.Path);
            }

            proj.Save(Path.Combine(FullProjectDirectory, AssemblyName + ".csproj"));

            if (!engine.BuildProject(proj))
                throw new BuildException(String.Format(CultureInfo.CurrentUICulture, "Failed to build {0}", c_AssemblyName));

            string asmFileName = Path.Combine(OutputPath, c_AssemblyName);
            AddOutputFile(asmFileName);

            // deploy the associated .pdb, if it exists.
            string pdbFileName = Path.ChangeExtension(asmFileName, ".pdb");
            if (File.Exists(pdbFileName))
                AddOutputFile(pdbFileName);
        }

        protected override void ConfigureProject(Microsoft.Build.BuildEngine.Project proj)
        {
            base.ConfigureProject(proj);

            // Platform libraries
            AddReference("Sage.Platform", Path.Combine(SystemLibraryPath, "Sage.Platform.dll"), false, false);
            AddReference("Sage.Platform.Application", Path.Combine(SystemLibraryPath, "Sage.Platform.Application.dll"), false, false);

            // SalesLogix libraries
            AddReference("sage.entity.interfaces.dll", Path.Combine(LibraryPath, "sage.entity.interfaces.dll"), false, false);

            // SData libraries
            AddReference("Sage.Integration.Server.Model", Path.Combine(SystemLibraryPath, "Sage.Integration.Server.Model.dll"), false, false);

            AddReference("SDataLinqProvider", Path.Combine(SystemLibraryPath, "SDataLinqProvider.dll"), false, false);

            // Base Class Library
            References.AddNewItem("Reference", "System");
            References.AddNewItem("Reference", "System.Data");
            References.AddNewItem("Reference", "System.Xml");
            References.AddNewItem("Reference", "System.Web");
        }
    }
}