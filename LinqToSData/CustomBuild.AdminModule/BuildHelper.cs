using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Sage.Platform.Extensibility.Interfaces;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Orm.Entities;
using Sage.Platform.Projects.Interfaces;

namespace CustomBuild.AdminModule
{
    internal static class BuildHelper
    {
        internal static bool IsEntityNewer(BuildType buildType, DateTime baseDate, OrmEntity entity)
        {
            if (buildType == BuildType.BuildAll)
                return true;

            entity.FilePath.Refresh();

            if (baseDate < entity.FilePath.LastWriteTimeUtc)
                return true;

            //check all business rule methods and events
            IFileInfo[] methods = entity.FilePath.Directory.GetFiles(EntityModelUrlConstants.QRY_ALL_METHODS);
            foreach (IFileInfo methodFile in methods)
            {
                if (baseDate < methodFile.LastWriteTimeUtc)
                    return true;
            }

            if (IsNewer(entity.ParentEntities, baseDate))
                return true;

            if (IsNewer(entity.ChildEntities, baseDate))
                return true;

            if (IsNewer(entity.ExtensionEntities, baseDate))
                return true;

            return false;
        }

        private static bool IsNewer<T>(IEnumerable<T> items, DateTime baseDate)
            where T : IModelItem
        {
            foreach (T t in items)
            {
                if (t.FilePath == null)
                    continue;

                t.FilePath.Refresh();
                if (baseDate < t.FilePath.LastWriteTimeUtc)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Writes the resource to the specified path on the local file system.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="resourceName"></param>
        /// <remarks>Automatically prepends <c>Sage.Platform.Orm.CodeGen.Templates.</c> to <paramref name="resourceName"/>.</remarks>
        public static void ResourceWriter(string path, string resourceName)
        {
            var resourcesClass = new FileInfo(path);

            using (var resourcesSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Sage.Platform.Orm.CodeGen.Templates." + resourceName))
            using (Stream sw = resourcesClass.Open(FileMode.Create))
            {
                var buf = new byte[4096];
                int i;
                while ((i = resourcesSource.Read(buf, 0, buf.Length)) != 0)
                {
                    sw.Write(buf, 0, i);
                }
            }
        }
    }
}