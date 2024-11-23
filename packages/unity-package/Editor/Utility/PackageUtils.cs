using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor.PackageManager;
using UnityEngine;

namespace NxUnity
{
  public static class PackageUtils
  {
    public static bool Verbose = true;

    public static PackageCollection GetInstalledPackages()
    {
      var request = Client.List();
      while (!request.IsCompleted)
      {
      }
      // Debug.Log("The project depends on these packages: " + string.Join(", ", request.Result.Select(p => p.name)));
      return request.Result;
    }

    public static bool IsPackageInstalled(PackageCollection packageCollection, string packageName)
    {
      return packageCollection.Any(p => p.name == packageName);
    }

    public static bool InstallPackage(string packageName, string packageEntry)
    {
      var packageIdentifier = $"{packageName}@{packageEntry}";

      // Handle local packages
      var isLocalPackage = packageEntry.StartsWith("file:");
      if (isLocalPackage)
      {
        var packagePath = packageEntry["file:".Length..];
        if (!Path.IsPathRooted(packagePath))
        {
          var absolutePackagePath = NxUtils.GetWorkspaceRoot() + "/" + packagePath;
          var manifestDirectoryPath = NxUtils.GetWorkspaceRoot() + "/" + NxUtils.GetProjectRoot() + "/Packages";
          packagePath = Path.GetRelativePath(manifestDirectoryPath, absolutePackagePath).Replace('\\', '/');
          packageIdentifier = $"{packageName}@file:{packagePath}";
        }
      }

      LogVerbose($"Attempting to install package: {packageIdentifier}");

      var addRequest = Client.Add(packageIdentifier);
      while (!addRequest.IsCompleted)
      {
        Thread.Sleep(100);
      }

      if (addRequest.Status == StatusCode.Success)
      {
        LogVerbose($"Package {packageIdentifier} installed successfully.");
        return true;
      }

      if (isLocalPackage)
      {
        Debug.LogError($"Package {packageIdentifier} could not be installed: " + addRequest.Error.message);
        return false;
      }

      LogVerbose($"Package {packageIdentifier} could not be installed. Trying to install via OpenUPM CLI.");
      try
      {
        InstallPackageViaOpenUPMCLI(packageIdentifier);
        LogVerbose($"Package {packageIdentifier} installed successfully via OpenUPM CLI.");
        return true;
      }
      catch (Exception ex)
      {
        Debug.LogError(
          $"Exception occurred while installing package {packageIdentifier} via OpenUPM CLI: {ex.Message}");
        return false;
      }
    }

    public static void InstallPackageViaOpenUPMCLI(string packageIdentifier)
    {
      using (var process = new System.Diagnostics.Process())
      {
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = $"/C npx openupm add {packageIdentifier}";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = Path.Join(NxUtils.GetWorkspaceRoot(), NxUtils.GetProjectRoot());
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // Debug.Log("Working directory: " + process.StartInfo.WorkingDirectory);
        // Debug.Log("Executing command: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
        // Debug.Log("Output: " + output);

        if (process.ExitCode != 0)
        {
          throw new Exception($"Failed to install package {packageIdentifier} via OpenUPM CLI: {error}");
        }
      }
    }

    public static bool RemovePackage(string packageName)
    {
      LogVerbose($"Removing package: {packageName}");
      var removeRequest = Client.Remove(packageName);
      while (!removeRequest.IsCompleted)
      {
        Thread.Sleep(100);
      }
      if (removeRequest.Status == StatusCode.Failure)
      {
        Debug.LogError($"Package {packageName} could not be removed");
        return false;
      }

      LogVerbose($"Package {packageName} removed successfully");
      return true;
    }

    public static bool IsPackageInOpenUpmRegistryScope(string packageName)
    {
      // Load manifest.json and check if the package is in the scoped registries
      var manifestPath = $"{NxUtils.GetWorkspaceRoot()}/{NxUtils.GetProjectRoot()}/Packages/manifest.json";
      // Debug.Log($"Checking if scoped registry {packageName} is in: {manifestPath}");
      if (!File.Exists(manifestPath))
      {
        return false;
      }

      var manifestContent = File.ReadAllText(manifestPath);
      var manifest = JsonConvert.DeserializeObject<PackageManifest>(manifestContent);
      return manifest.scopedRegistries.Any(r => r.scopes.Contains(packageName));
    }

    public static bool AddPackageToOpenUpmRegistryScopes(string packageName)
    {
      var manifestPath = Path.Join(NxUtils.GetWorkspaceRoot(), NxUtils.GetProjectRoot(), "Packages/manifest.json");
      if (!File.Exists(manifestPath))
      {
        Debug.LogError("manifest.json not found");
        return false;
      }

      var manifestContent = File.ReadAllText(manifestPath);
      var manifest = JsonConvert.DeserializeObject<PackageManifest>(manifestContent);

      var openUpmRegistry = manifest.scopedRegistries.FirstOrDefault(r => r.name == "package.openupm.com");
      if (openUpmRegistry == null)
      {
        openUpmRegistry = new PackageManifest.ScopedRegistry
        {
          name = "package.openupm.com",
          url = "https://package.openupm.com",
          scopes = new List<string>()
        };
        manifest.scopedRegistries.Add(openUpmRegistry);
      }

      if (!openUpmRegistry.scopes.Contains(packageName))
      {
        openUpmRegistry.scopes.Add(packageName);
        var updatedManifestContent = JsonConvert.SerializeObject(manifest);
        File.WriteAllText(manifestPath, updatedManifestContent);
        return true;
      }

      return false;
    }

    private static void LogVerbose(string message)
    {
      if (Verbose)
      {
        Debug.Log(message);
      }
    }
  }
}
