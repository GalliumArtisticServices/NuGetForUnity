using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NugetForUnity
{
    [DataContract]
    public class DependencyGroups
    {
        [DataMember(Name = "@id", IsRequired = false)]
        public string Url { get; set; }

        [DataMember(Name = "@type", IsRequired = false)]
        public string Type { get; set; }

        [DataMember(Name = "dependencies", IsRequired = false)]
        public Dependency[] Dependencies { get; set; }

        [DataMember(Name = "targetFramework", IsRequired = false)]
        public string TargetFramework { get; set; }

        public NugetFrameworkGroup ToNugetFrameworkGroup()
        {
            List<NugetPackageIdentifier> dependencies = new List<NugetPackageIdentifier>();
            for (int i = 0; i < Dependencies.Length; ++i)
            {
                dependencies.Add(Dependencies[i].ToNugetPackageIdentifier());
            }

            return new NugetFrameworkGroup()
            {
                TargetFramework = TargetFramework,
                Dependencies = dependencies
            };
        }
    }

    [DataContract]
    public class Dependency
    {
        [DataMember(Name = "@id", IsRequired = true)]
        public string Url { get; set; }

        [DataMember(Name = "@type", IsRequired = true)]
        public string Type { get; set; }

        [DataMember(Name = "id", IsRequired = true)]
        public string Id { get; set; }

        [DataMember(Name = "range", IsRequired = true)]
        public string Range { get; set; }

        public NugetPackageIdentifier ToNugetPackageIdentifier()
        {
            return new NugetPackageIdentifier(Id, Range);
        }
    }

    [DataContract]
    public class VersionData
    {
        [DataMember(Name = "version", IsRequired = false)]
        public string Version { get; set; }

        [DataMember(Name = "downloads", IsRequired = false)]
        public string Downloads { get; set; }

        [DataMember(Name = "@id", IsRequired = false)]
        public string Url { get; set; }
    }

    [DataContract]
    public class NugetData
    {
        [DataMember(Name = "@type", IsRequired = false)]
        public string Type { get; set; }

        [DataMember(Name = "copyright", IsRequired = false)]
        public string Copyright { get; set; }

        [DataMember(Name = "dependencies", IsRequired = false)]
        public Dependency[] Dependencies { get; set; }

        [DataMember(Name = "dependencyGroups", IsRequired = false)]
        public DependencyGroups[] DependencyGroups { get; set; }

        [DataMember(Name = "description", IsRequired = false)]
        public string Description { get; set; }

        [DataMember(Name = "iconUrl", IsRequired = false)]
        public string IconUrl { get; set; }

        [DataMember(Name = "id", IsRequired = false)]
        public string Id { get; set; }

        [DataMember(Name = "isPrerelease", IsRequired = false)]
        public bool IsPrerelease { get; set; }

        [DataMember(Name = "language", IsRequired = false)]
        public string Language { get; set; }

        [DataMember(Name = "licenseUrl", IsRequired = false)]
        public string LicenseUrl { get; set; }

        [DataMember(Name = "requireLicenseAcceptance", IsRequired = false)]
        public bool RequireLicenseAcceptance { get; set; }

        [DataMember(Name = "summary", IsRequired = false)]
        public string Summary { get; set; }

        [DataMember(Name = "title", IsRequired = false)]
        public string Title { get; set; }

        [DataMember(Name = "totalDownloads", IsRequired = false)]
        public int TotalDownloads { get; set; }

        [DataMember(Name = "verified", IsRequired = false)]
        public bool Verified { get; set; }

        [DataMember(Name = "version", IsRequired = false)]
        public string Version { get; set; }

        [DataMember(Name = "versions", IsRequired = false)]
        public VersionData[] Versions { get; set; }

        public NugetPackage ToNugetPackage()
        {
            List<NugetFrameworkGroup> groups = new List<NugetFrameworkGroup>();
            for(int i = 0; i < DependencyGroups?.Length; ++i)
            {
                groups.Add(DependencyGroups[i].ToNugetFrameworkGroup());
            }

            return new NugetPackage()
            {
                Id = Id,
                Version = Version,
                Title = Title,
                Description = Description,
                Summary = Summary,
                LicenseUrl = LicenseUrl,
                DownloadUrl = Versions[Versions.Length - 1].Url,
                DownloadCount = TotalDownloads,
                Dependencies = groups
            };
        }
    }

    [DataContract]
    public class NugetJsonResponse
    {
        [DataMember(Name = "data", IsRequired = false)]
        public NugetData[] Data { get; set; }

        [DataMember(Name = "totalHits", IsRequired = false)]
        public uint TotalHits { get; set; }
    }

    [DataContract]
    public class CatalogEntry
    {
        [DataMember(Name = "@id", IsRequired = false)]
        public string Url { get; set; }

        [DataMember(Name = "packageContent", IsRequired = false)]
        public string PackageContent { get; set; }
       
        [DataMember(Name = "copyright", IsRequired = false)]
        public string Copyright { get; set; }

        [DataMember(Name = "dependencyGroups", IsRequired = false)]
        public DependencyGroups[] DependencyGroups { get; set; }

        [DataMember(Name = "description", IsRequired = false)]
        public string Description { get; set; }

        [DataMember(Name = "iconUrl", IsRequired = false)]
        public string IconUrl { get; set; }

        [DataMember(Name = "id", IsRequired = false)]
        public string Id { get; set; }

        [DataMember(Name = "isPrerelease", IsRequired = false)]
        public bool IsPrerelease { get; set; }

        [DataMember(Name = "language", IsRequired = false)]
        public string Language { get; set; }

        [DataMember(Name = "licenseUrl", IsRequired = false)]
        public string LicenseUrl { get; set; }

        [DataMember(Name = "requireLicenseAcceptance", IsRequired = false)]
        public bool RequireLicenseAcceptance { get; set; }

        [DataMember(Name = "summary", IsRequired = false)]
        public string Summary { get; set; }

        [DataMember(Name = "version", IsRequired = false)]
        public string Version { get; set; }

        public NugetPackage ToNugetPackage()
        {
            List<NugetFrameworkGroup> groups = new List<NugetFrameworkGroup>();
            for (int i = 0; i < DependencyGroups.Length; ++i)
            {
                groups.Add(DependencyGroups[i].ToNugetFrameworkGroup());
            }

            return new NugetPackage()
            {
                Id = Id,
                Version = Version,
                Description = Description,
                Summary = Summary,
                LicenseUrl = LicenseUrl,
                DownloadUrl = PackageContent,
                Dependencies = groups
            };
        }
    }

    [DataContract]
    public class NugetPackageCatalog<T>
    {
        [DataMember(Name = "@id", IsRequired = false)]
        public string Id { get; set; }

        [DataMember(Name = "packageContent", IsRequired = false)]
        public string PackageContent { get; set; }

        [DataMember(Name = "catalogEntry", IsRequired = false)]
        public T CatalogEntry { get; set; }
    }
}