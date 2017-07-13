using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Measurement
{
    public static class ExecutablePackage
    {
        public static string[] ExecutableExtensions = new string[] { "exe" };
        public static string[] BatchFileExtensions = new string[] { "bat", "cmd" };

        public static void PackToStream(string[] fileNames, string mainExecutable, Stream stream)
        {
            if (fileNames == null) throw new ArgumentNullException("fileNames");

            ZipPackage pkg = (ZipPackage)ZipPackage.Open(stream, FileMode.Create);

            foreach (string f in fileNames)
            {
                Uri uri = PackUriHelper.CreatePartUri(new Uri(Path.GetFileName(f), UriKind.Relative));
                ZipPackagePart p = (ZipPackagePart)pkg.CreatePart(uri, System.Net.Mime.MediaTypeNames.Application.Octet, CompressionOption.Maximum);
                CopyStream(new FileStream(f, FileMode.Open, FileAccess.Read), p.GetStream());
                if (f == mainExecutable)
                    pkg.CreateRelationship(uri, TargetMode.Internal, "http://schemas.openxmlformats.org/package/2006/relationships/meta data/thumbnail");
            }

            pkg.Close();
        }

        /// <summary>
        /// Extracts contents of zip archive to target directory and finds main executable within it.
        /// </summary>
        /// <param name="fileName">Name of zip file</param>
        /// <param name="targetFolder">Foler to extract archive's contents to</param>
        /// <returns>Name of the main executable</returns>
        public static string ExtractZip(string fileName, string targetFolder)
        {
            string localExecutable = null;
            int execCount = 0;

            using (var zip = ZipFile.Read(fileName))
            {
                foreach (var fn in zip.EntryFileNames)
                {
                    var ext = Path.GetExtension(fn).Substring(1);
                    if (ExecutableExtensions.Contains(ext) || BatchFileExtensions.Contains(ext))
                    {
                        ++execCount;
                        localExecutable = Path.Combine(targetFolder, fn);
                    }
                }
                if (execCount == 1)
                {
                    // If zip contains exactly one executable, then it is the one we need.
                    zip.ExtractAll(targetFolder);
                    return localExecutable;
                }
            }

            localExecutable = null;

            // If single executable expectation failed, try to treat zip as a package with main executable name stored within a relationship
            using (Package pkg = Package.Open(fileName, FileMode.Open))
            {
                PackageRelationshipCollection rels = pkg.GetRelationships();
                var relsCount = rels.Count();

                if (relsCount != 1)
                    throw new Exception("Single executable expectation is failed, when interpreting archive as a package relationships appeared incorrect.");

                PackageRelationship main = rels.First();

                var parts = pkg.GetParts();
                foreach (PackagePart part in parts)
                {
                    using (Stream s = part.GetStream(FileMode.Open, FileAccess.Read))
                    {
                        string fn = CreateFilenameFromUri(part.Uri).Substring(1);
                        string targetPath = Path.Combine(targetFolder, fn);
                        using (var fs = new FileStream(targetPath, FileMode.OpenOrCreate))
                        {
                            CopyStream(s, fs);
                        }

                        if (part.Uri == main.TargetUri)
                            localExecutable = targetPath;
                    }
                }
            }

            if (localExecutable == null)
                throw new Exception("Main executable not found in zip.");

            return localExecutable;
        }



        private static string CreateFilenameFromUri(Uri uri)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(uri.OriginalString.Length);
            foreach (char c in uri.OriginalString)
            {
                sb.Append(Array.IndexOf(invalidChars, c) < 0 ? c : '_');
            }
            return sb.ToString();
        }

        private static void CopyStream(Stream source, Stream target)
        {
            source.CopyTo(target);
        }
    }
}
