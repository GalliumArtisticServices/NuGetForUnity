namespace NugetForUnity
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.Serialization.Json;
    using System.Xml.Linq;
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Represents a NuGet Package Source (a "server").
    /// </summary>
    public class NugetPackageSource
    {
        /// <summary>
        /// Gets or sets the name of the package source.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the path of the package source.
        /// </summary>
        public string SavedPath { get; set; }

        public int ProtocolVersion { get; set; }

        /// <summary>
        /// Gets path, with the values of environment variables expanded.
        /// </summary>
        public string ExpandedPath
        {
            get
            {
                string path = Environment.ExpandEnvironmentVariables(SavedPath);
                if (!path.StartsWith("http") && path != "(Aggregate source)" && !Path.IsPathRooted(path))
                {
                    path = Path.Combine(Path.GetDirectoryName(NugetHelper.NugetConfigFilePath), path);
                }

                return path;
            }
        }

        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the password used to access the feed. Null indicates that no password is used.
        /// </summary>
        public string SavedPassword { get; set; }

        /// <summary>
        /// Gets password, with the values of environment variables expanded.
        /// </summary>
        public string ExpandedPassword
        {
            get
            {
                return SavedPassword != null ? Environment.ExpandEnvironmentVariables(SavedPassword) : null;
            }
        }

        public bool HasPassword
        {
            get { return SavedPassword != null; }

            set
            {
                if (value)
                {
                    if (SavedPassword == null)
                    {
                        SavedPassword = string.Empty; // Initialize newly-enabled password to empty string.
                    }
                }
                else
                {
                    SavedPassword = null; // Clear password to null when disabled.
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicated whether the path is a local path or a remote path.
        /// </summary>
        public bool IsLocalPath { get; private set; }

        /// <summary>
        /// Gets or sets a value indicated whether this source is enabled or not.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NugetPackageSource"/> class.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="path"></param>
        /// <param name="protocolVersion"></param>
        public NugetPackageSource(string name, string path, int protocolVersion)
        {
            Name = name;
            SavedPath = path;
            IsLocalPath = !ExpandedPath.StartsWith("http");
            IsEnabled = true;
            ProtocolVersion = protocolVersion;
        }

        /// <summary>
        /// Gets a NugetPackage from the NuGet server that matches (or is in range of) the <see cref="NugetPackageIdentifier"/> given.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        public List<NugetPackage> FindPackagesById(NugetPackageIdentifier package)
        {
            List<NugetPackage> foundPackages = null;

            if (IsLocalPath)
            {
                if (!package.HasVersionRange)
                {
                    string localPackagePath = Path.Combine(ExpandedPath, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));
                    if (File.Exists(localPackagePath))
                    {
                        NugetPackage localPackage = NugetPackage.FromNupkgFile(localPackagePath);
                        foundPackages = new List<NugetPackage> { localPackage };
                    }
                    else { foundPackages = new List<NugetPackage>(); }
                }
                else
                {
                    // TODO: Optimize to no longer use GetLocalPackages, since that loads the .nupkg itself
                    foundPackages = GetLocalPackages(package.Id, true, true);
                }
            }
            else
            {
                string url = null;

                if (ProtocolVersion == 2)
                {
                    // See here: http://www.odata.org/documentation/odata-version-2-0/uri-conventions/
                    url = string.Format("{0}FindPackagesById()?id='{1}&$orderby=Id desc'", ExpandedPath, package.Id);
                }
                else
                {
                    url = string.Format("{0}query?q={1}", ExpandedPath, package.Id);
                }
                
                // Are we looking for a specific package?
                if (!package.HasVersionRange)
                {
                    if (ProtocolVersion == 2)
                    {
                        url = string.Format("{0}&$filter=Version eq '{1}'", url, package.Version);
                    }
                }

                try
                {
                    foundPackages = GetPackagesFromUrl(url, UserName, ExpandedPassword);
                }
                catch (Exception e)
                {
                    foundPackages = new List<NugetPackage>();
                    Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e.ToString());
                }
            }

            if (foundPackages != null)
            {
                // Return all the packages in the range of versions specified by 'package'.
                foundPackages.RemoveAll(p => !package.InRange(p));
                foundPackages.Sort();

                foreach (NugetPackage foundPackage in foundPackages)
                {
                    foundPackage.PackageSource = this;
                }
            }

            return foundPackages;
        }

        /// <summary>
        /// Gets a NugetPackage from the NuGet server that matches (or is in range of) the <see cref="NugetPackageIdentifier"/> given.
        /// </summary>
        /// <param name="package">The <see cref="NugetPackageIdentifier"/> containing the ID and Version of the package to get.</param>
        /// <returns>The retrieved package, if there is one.  Null if no matching package was found.</returns>
        public NugetPackage GetSpecificPackage(NugetPackageIdentifier package)
        {
            if (ProtocolVersion == 2)
            {
                if (package.HasVersionRange)
                {
                    return FindPackagesById(package).FirstOrDefault();
                }
            }

            if (IsLocalPath)
            {
                string localPackagePath = Path.Combine(ExpandedPath, string.Format("./{0}.{1}.nupkg", package.Id, package.Version));
                if (File.Exists(localPackagePath))
                {
                    NugetPackage localPackage = NugetPackage.FromNupkgFile(localPackagePath);
                    return localPackage;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                string url;
                if (ProtocolVersion == 2)
                {
                    url = string.Format("{0}Packages(Id='{1}',Version='{2},$orderby=Id desc')", ExpandedPath, package.Id, package.Version);
                }
                else
                {
                    if (Name.Contains("Nuget"))
                    {
                        url = string.Format("{0}query?q={1}", ExpandedPath, package.Id);
                    }
                    else
                    { 
                        url = string.Format("{0}{1}/{2}.json", ExpandedPath, package.Id, package.MinimumVersion);
                    }
                }

                try
                {
                    Version minVer = new Version(package.MinimumVersion);
                    NugetPackage r = null;

                    if (ProtocolVersion == 2 || Name.Contains("Nuget"))
                    {
                        List<NugetPackage> packages = GetPackagesFromUrl(url, UserName, ExpandedPassword);
                        for(int i = 0; i < packages.Count; ++i)
                        {
                            if(!packages[i].Id.Equals(package.Id))
                            {
                                continue;
                            }

                            Version pkgVer = new Version(packages[i].MinimumVersion);
                            
                            if(pkgVer == minVer)
                            {
                                return packages[i];
                            }
                            else if (pkgVer > minVer)
                            {
                                // Closest match
                                r = packages[i];
                            }
                        }

                        if (r != null)
                        {
                            Debug.LogWarning("Unable to find exact version match.");
                        }
                        else
                        {
                            Debug.LogError("Unable to find version");
                        }

                        return r;
                    }
                    else
                    {
                        List<NugetPackage> packages = GetPackagesFromExplicitUrl(url, UserName, ExpandedPassword);
                        for (int i = 0; i < packages.Count; ++i)
                        {
                            if (!packages[i].Id.Equals(package.Id))
                            {
                                continue;
                            }

                            Version pkgVer = new Version(packages[i].MinimumVersion);

                            if (pkgVer == minVer)
                            {
                                return packages[i];
                            }
                            else if (pkgVer > minVer)
                            {
                                // Closest match
                                r = packages[i];
                            }
                        }

                        if (r != null)
                        {
                            Debug.LogWarning("Unable to find exact version match.");
                        }
                        else
                        {
                            Debug.LogError("Unable to find version");
                        }

                        return r;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("Unable to retrieve package from {0}\n{1}", url, e.ToString());
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets a list of NuGetPackages from this package source.
        /// This allows searching for partial IDs or even the empty string (the default) to list ALL packages.
        /// 
        /// NOTE: See the functions and parameters defined here: https://www.nuget.org/api/v2/$metadata
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        public List<NugetPackage> Search(string searchTerm = "", bool includeAllVersions = false, bool includePrerelease = false, int numberToGet = 15, int numberToSkip = 0)
        {
            if (IsLocalPath)
            {
                return GetLocalPackages(searchTerm, includeAllVersions, includePrerelease, numberToGet, numberToSkip);
            }

            //Example URL: "http://www.nuget.org/api/v2/Search()?$filter=IsLatestVersion&$orderby=Id&$skip=0&$top=30&searchTerm='newtonsoft'&targetFramework=''&includePrerelease=false";

            string url = ExpandedPath;

            if(ProtocolVersion == 2)
            {
                // call the search method
                url += "Search()?";
            }
            else
            {
                url = string.Format("{0}query?q={1}", url, string.IsNullOrEmpty(searchTerm) ? "gx42" : searchTerm);
            }

            // filter results
            if (!includeAllVersions)
            {
                if (ProtocolVersion == 2)
                {
                    if (!includePrerelease)
                    {
                        url += "$filter=IsLatestVersion&";
                    }
                    else
                    {
                        url += "$filter=IsAbsoluteLatestVersion&";
                    }
                }
                else
                {
                    url = string.Format("{0}&prerelease={1}", url, includePrerelease);
                }
            }

            // skip a certain number of entries
            url = string.Format("{0}&skip={1}", url, numberToSkip);

            if (ProtocolVersion == 2)
            {
                // order results
                url += "$orderby=DownloadCount desc&";
            
                // show a certain number of entries
                url += string.Format("$top={0}&", numberToGet);

                // apply the search term
                url += string.Format("searchTerm='{0}'&", searchTerm);

                // apply the target framework filters
                url += "targetFramework=''&";

                // should we include prerelease packages?
                url += string.Format("includePrerelease={0}", includePrerelease.ToString().ToLower());
            }

            try
            {
                return GetPackagesFromUrl(url, UserName, ExpandedPassword);
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e.ToString());
                return new List<NugetPackage>();
            }
        }

        /// <summary>
        /// Gets a list of all available packages from a local source (not a web server) that match the given filters.
        /// </summary>
        /// <param name="searchTerm">The search term to use to filter packages. Defaults to the empty string.</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="numberToGet">The number of packages to fetch.</param>
        /// <param name="numberToSkip">The number of packages to skip before fetching.</param>
        /// <returns>The list of available packages.</returns>
        private List<NugetPackage> GetLocalPackages(string searchTerm = "", bool includeAllVersions = false, bool includePrerelease = false, int numberToGet = 15, int numberToSkip = 0)
        {
            List<NugetPackage> localPackages = new List<NugetPackage>();

            if (numberToSkip != 0)
            {
                // we return the entire list the first time, so no more to add
                return localPackages;
            }

            string path = ExpandedPath;

            if (Directory.Exists(path))
            {
                string[] packagePaths = Directory.GetFiles(path, string.Format("*{0}*.nupkg", searchTerm));

                foreach (var packagePath in packagePaths)
                {
                    var package = NugetPackage.FromNupkgFile(packagePath);
                    package.PackageSource = this;

                    if (package.IsPrerelease && !includePrerelease)
                    {
                        // if it's a prerelease package and we aren't supposed to return prerelease packages, just skip it
                        continue;
                    }

                    if (includeAllVersions)
                    {
                        // if all versions are being included, simply add it and move on
                        localPackages.Add(package);
                        //LogVerbose("Adding {0} {1}", package.Id, package.Version);
                        continue;
                    }

                    var existingPackage = localPackages.FirstOrDefault(x => x.Id == package.Id);
                    if (existingPackage != null)
                    {
                        // there is already a package with the same ID in the list
                        if (existingPackage < package)
                        {
                            // if the current package is newer than the existing package, swap them
                            localPackages.Remove(existingPackage);
                            localPackages.Add(package);
                        }
                    }
                    else
                    {
                        // there is no package with the same ID in the list yet
                        localPackages.Add(package);
                    }
                }
            }
            else
            {
                Debug.LogErrorFormat("Local folder not found: {0}", path);
            }

            return localPackages;
        }

        /// <summary>
        /// Builds a list of NugetPackages from the XML returned from the HTTP GET request issued at the given URL.
        /// Note that NuGet uses an Atom-feed (XML Syndicaton) superset called OData.
        /// See here http://www.odata.org/documentation/odata-version-2-0/uri-conventions/
        /// </summary>
        /// <param name="url"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private List<NugetPackage> GetPackagesFromUrl(string url, string username, string password)
        {
            NugetHelper.LogVerbose("Getting packages from: {0}", url);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<NugetPackage> packages = new List<NugetPackage>();

            // Mono doesn't have a Certificate Authority, so we have to provide all validation manually.  Currently just accept anything.
            // See here: http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https

            // remove all handlers
            ServicePointManager.ServerCertificateValidationCallback = null;

            // add anonymous handler
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, policyErrors) => true;

            using (Stream responseStream = NugetHelper.RequestUrl(url, username, password, timeOut: 5000))
            {
                if(responseStream == null)
                {
                    return packages;
                }

                if (ProtocolVersion == 2)
                {
                    using (StreamReader streamReader = new StreamReader(responseStream))
                    {
                        packages = NugetODataResponse.Parse(XDocument.Load(streamReader));
                        foreach (var package in packages)
                        {
                            package.PackageSource = this;
                        }
                    }
                }
                else
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(NugetJsonResponse));
                    NugetJsonResponse response = ser.ReadObject(responseStream) as NugetJsonResponse;

                    for(int i = 0; i < response.Data.Length; ++i)
                    {
                        NugetPackage package = response.Data[i].ToNugetPackage();
                        package.PackageSource = this;

                        if (!string.IsNullOrEmpty(response.Data[i].IconUrl))
                        {
                            package.Icon = NugetHelper.DownloadImage(response.Data[i].IconUrl);
                        }

                        // Get actual downloadUrl
                        Stream catalogStream = NugetHelper.RequestUrl(package.DownloadUrl, username, password, timeOut: 5000);

                        if (package.DownloadUrl.Contains("github.com"))
                        {
                            ser = new DataContractJsonSerializer(typeof(NugetPackageCatalog<CatalogEntry>));
                            NugetPackageCatalog<CatalogEntry> catalog = ser.ReadObject(catalogStream) as NugetPackageCatalog<CatalogEntry>; 
                            package.DownloadUrl = catalog.PackageContent;
                        }
                        else
                        {
                            ser = new DataContractJsonSerializer(typeof(NugetPackageCatalog<string>));
                            NugetPackageCatalog<string> catalog = ser.ReadObject(catalogStream) as NugetPackageCatalog<string>;

                            if(package.Dependencies.Count == 0 && catalog.CatalogEntry.GetType() == typeof(string))
                            {
                                // TODO: Pull the dependencies from the catalog.CatalogEntry
                                Stream c = NugetHelper.RequestUrl(catalog.CatalogEntry, username, password, timeOut: 5000);

                                ser = new DataContractJsonSerializer(typeof(NugetCatalogEntry));
                                NugetCatalogEntry cat = ser.ReadObject(c) as NugetCatalogEntry;

                                NugetPackageCatalog<NugetCatalogEntry> newCatalog = new NugetPackageCatalog<NugetCatalogEntry>()
                                {
                                    Id = catalog.Id,
                                    PackageContent = catalog.PackageContent
                                };

                                newCatalog.CatalogEntry = cat;

                                package.Dependencies = cat.ToFrameworkGroups();
                            }

                            package.DownloadUrl = catalog.PackageContent;
                        }

                        packages.Add(package);
                    }
                }
            }

            stopwatch.Stop();
            NugetHelper.LogVerbose("Retreived {0} packages in {1} ms", packages.Count, stopwatch.ElapsedMilliseconds);

            return packages;
        }

        private List<NugetPackage> GetPackagesFromExplicitUrl(string url, string username, string password)
        {
            NugetHelper.LogVerbose("Getting packages from: {0}", url);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            List<NugetPackage> packages = new List<NugetPackage>();

            // Mono doesn't have a Certificate Authority, so we have to provide all validation manually.  Currently just accept anything.
            // See here: http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https

            // remove all handlers
            ServicePointManager.ServerCertificateValidationCallback = null;

            // add anonymous handler
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, policyErrors) => true;

            using (Stream responseStream = NugetHelper.RequestUrl(url, username, password, timeOut: 5000))
            {
                if(responseStream == null)
                {
                    return packages;
                }

                if (ProtocolVersion == 2)
                {
                    using (StreamReader streamReader = new StreamReader(responseStream))
                    {
                        packages = NugetODataResponse.Parse(XDocument.Load(streamReader));
                        foreach (var package in packages)
                        {
                            package.PackageSource = this;
                        }
                    }
                }
                else
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(NugetPackageCatalog<CatalogEntry>));
                    NugetPackageCatalog<CatalogEntry> catalog = ser.ReadObject(responseStream) as NugetPackageCatalog<CatalogEntry>;

                    NugetPackage package = catalog.CatalogEntry.ToNugetPackage();
                    package.PackageSource = this;

                    packages.Add(package);
                }
            }

            stopwatch.Stop();
            NugetHelper.LogVerbose("Retreived {0} packages in {1} ms", packages.Count, stopwatch.ElapsedMilliseconds);

            return packages;
        }

        /// <summary>
        /// Gets a list of available packages from a local source (not a web server) that are upgrades for the given list of installed packages.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages to use to find updates.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <returns>A list of all updates available.</returns>
        private List<NugetPackage> GetLocalUpdates(IEnumerable<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false)
        {
            List<NugetPackage> updates = new List<NugetPackage>();

            var availablePackages = GetLocalPackages(string.Empty, includeAllVersions, includePrerelease);
            foreach (var installedPackage in installedPackages)
            {
                foreach (var availablePackage in availablePackages)
                {
                    if (installedPackage.Id == availablePackage.Id)
                    {
                        if (installedPackage < availablePackage)
                        {
                            updates.Add(availablePackage);
                        }
                    }
                }
            }

            return updates;
        }

        /// <summary>
        /// Queries the source with the given list of installed packages to get any updates that are available.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        public List<NugetPackage> GetUpdates(IEnumerable<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "")
        {
            if (IsLocalPath)
            {
                return GetLocalUpdates(installedPackages, includePrerelease, includeAllVersions);
            }

            List<NugetPackage> updates = new List<NugetPackage>();

            // check for updates in groups of 10 instead of all of them, since that causes servers to throw errors for queries that are too long
            for (int i = 0; i < installedPackages.Count(); i += 10)
            {
                var packageGroup = installedPackages.Skip(i).Take(10);

                string packageIds = string.Empty;
                string versions = string.Empty;

                foreach (var package in packageGroup)
                {
                    if (string.IsNullOrEmpty(packageIds))
                    {
                        packageIds += package.Id;
                    }
                    else
                    {
                        packageIds += "|" + package.Id;
                    }

                    if (string.IsNullOrEmpty(versions))
                    {
                        versions += package.Version;
                    }
                    else
                    {
                        versions += "|" + package.Version;
                    }
                }

                string url = string.Format("{0}GetUpdates()?packageIds='{1}'&versions='{2}'&includePrerelease={3}&includeAllVersions={4}&targetFrameworks='{5}'&versionConstraints='{6}'", ExpandedPath, packageIds, versions, includePrerelease.ToString().ToLower(), includeAllVersions.ToString().ToLower(), targetFrameworks, versionContraints);

                try
                {
                    var newPackages = GetPackagesFromUrl(url, UserName, ExpandedPassword);
                    updates.AddRange(newPackages);
                }
                catch (System.Exception e)
                {
                    WebException webException = e as WebException;
                    HttpWebResponse webResponse = webException != null ? webException.Response as HttpWebResponse : null;
                    if (webResponse != null && webResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        // Some web services, such as VSTS don't support the GetUpdates API. Attempt to retrieve updates via FindPackagesById.
                        NugetHelper.LogVerbose("{0} not found. Falling back to FindPackagesById.", url);
                        return GetUpdatesFallback(installedPackages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
                    }

                    Debug.LogErrorFormat("Unable to retrieve package list from {0}\n{1}", url, e.ToString());
                }
            }

            // sort alphabetically, then by version descending
            updates.Sort(delegate (NugetPackage x, NugetPackage y)
            {
                if (x.Id == y.Id)
                    return -1 * x.CompareVersion(y.Version);
                else
                    return x.Id.CompareTo(y.Id);
            });

#if TEST_GET_UPDATES_FALLBACK
            // Enable this define in order to test that GetUpdatesFallback is working as intended. This tests that it returns the same set of packages
            // that are returned by the GetUpdates API. Since GetUpdates isn't available when using a Visual Studio Team Services feed, the intention
            // is that this test would be conducted by using nuget.org's feed where both paths can be compared.
            List<NugetPackage> updatesReplacement = GetUpdatesFallback(installedPackages, includePrerelease, includeAllVersions, targetFrameworks, versionContraints);
            ComparePackageLists(updates, updatesReplacement, "GetUpdatesFallback doesn't match GetUpdates API");
#endif

            return updates;
        }

        private static void ComparePackageLists(List<NugetPackage> updates, List<NugetPackage> updatesReplacement, string errorMessageToDisplayIfListsDoNotMatch)
        {
            System.Text.StringBuilder matchingComparison = new System.Text.StringBuilder();
            System.Text.StringBuilder missingComparison = new System.Text.StringBuilder();
            foreach (NugetPackage package in updates)
            {
                if (updatesReplacement.Contains(package))
                {
                    matchingComparison.Append(matchingComparison.Length == 0 ? "Matching: " : ", ");
                    matchingComparison.Append(package.ToString());
                }
                else
                {
                    missingComparison.Append(missingComparison.Length == 0 ? "Missing: " : ", ");
                    missingComparison.Append(package.ToString());
                }
            }
            System.Text.StringBuilder extraComparison = new System.Text.StringBuilder();
            foreach (NugetPackage package in updatesReplacement)
            {
                if (!updates.Contains(package))
                {
                    extraComparison.Append(extraComparison.Length == 0 ? "Extra: " : ", ");
                    extraComparison.Append(package.ToString());
                }
            }
            if (missingComparison.Length > 0 || extraComparison.Length > 0)
            {
                Debug.LogWarningFormat("{0}\n{1}\n{2}\n{3}", errorMessageToDisplayIfListsDoNotMatch, matchingComparison, missingComparison, extraComparison);
            }
        }

        /// <summary>
        /// Some NuGet feeds such as Visual Studio Team Services do not implement the GetUpdates function.
        /// In that case this fallback function can be used to retrieve updates by using the FindPackagesById function.
        /// </summary>
        /// <param name="installedPackages">The list of currently installed packages.</param>
        /// <param name="includePrerelease">True to include prerelease packages (alpha, beta, etc).</param>
        /// <param name="includeAllVersions">True to include older versions that are not the latest version.</param>
        /// <param name="targetFrameworks">The specific frameworks to target?</param>
        /// <param name="versionContraints">The version constraints?</param>
        /// <returns>A list of all updates available.</returns>
        private List<NugetPackage> GetUpdatesFallback(IEnumerable<NugetPackage> installedPackages, bool includePrerelease = false, bool includeAllVersions = false, string targetFrameworks = "", string versionContraints = "")
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Debug.Assert(string.IsNullOrEmpty(targetFrameworks) && string.IsNullOrEmpty(versionContraints)); // These features are not supported by this version of GetUpdates.

            List<NugetPackage> updates = new List<NugetPackage>();
            foreach (NugetPackage installedPackage in installedPackages)
            {
                string versionRange = string.Format("({0},)", installedPackage.Version); // Minimum of Current ID (exclusive) with no maximum (exclusive).
                NugetPackageIdentifier id = new NugetPackageIdentifier(installedPackage.Id, versionRange);
                List<NugetPackage> packageUpdates = FindPackagesById(id);

                if (!includePrerelease) { packageUpdates.RemoveAll(p => p.IsPrerelease); }
                if( packageUpdates.Count == 0 ) { continue; }

                int skip = includeAllVersions ? 0 : packageUpdates.Count - 1;
                updates.AddRange(packageUpdates.Skip(skip));
            }

            NugetHelper.LogVerbose("NugetPackageSource.GetUpdatesFallback took {0} ms", stopwatch.ElapsedMilliseconds);
            return updates;
        }
    }
}
