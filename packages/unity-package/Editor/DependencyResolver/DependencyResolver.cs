using UnityEditor;
using UnityEngine;

namespace NxUnity
{
  [InitializeOnLoad]
  public static class DependencyResolver
  {
#if !NX_DISABLED
    static DependencyResolver()
    {
      EditorApplication.projectChanged += ResolveDependencies;
      EditorApplication.focusChanged += OnFocusChange;
      ResolveDependencies();
    }
#endif

    private static void OnFocusChange(bool focus)
    {
      if (focus)
      {
        ResolveDependencies();
      }
    }

    [MenuItem("Tools/Nx Unity/Resolve Dependencies")]
    public static void ResolveDependencies()
    {
      var somethingChanged = false;

      var globalDependenciesDefinition = GlobalDependenciesDefinition.Get();
      var installedPackages = PackageUtils.GetInstalledPackages();

      if (globalDependenciesDefinition.Dependencies.Count > 0)
      {
        // Install packages that are in the global package.json but not in the project manifest.json
        foreach (var globalDependency in globalDependenciesDefinition.Dependencies)
        {
          if (!PackageUtils.IsPackageInstalled(installedPackages, globalDependency.Key))
          {
            if (PackageUtils.InstallPackage(globalDependency.Key, globalDependency.Value))
            {
              somethingChanged = true;
            }
          }
        }
      }
      else
      {
        // Add all installed packages to the global package.json
        foreach (var installedPackage in installedPackages)
        {
          globalDependenciesDefinition.RegisterInstalledPackage(installedPackage);
          somethingChanged = true;
        }
      }

      // Handle packages that are in the project manifest.json but not in the global package.json
      foreach (var installedPackage in installedPackages)
      {
        if (!globalDependenciesDefinition.Dependencies.ContainsKey(installedPackage.name))
        {
          bool add = EditorUtility.DisplayDialog(
            "Dependency Resolution",
            $"{installedPackage.displayName} ({installedPackage.version}) is present in this project's manifest.json but not in the global package.json. Would you like to add this package to the global package.json, or remove it?",
            "Add to global package.json",
            "Remove package");

          if (add)
          {
            globalDependenciesDefinition.RegisterInstalledPackage(installedPackage);
            somethingChanged = true;
          }
          else
          {
            if (PackageUtils.RemovePackage(installedPackage.name))
            {
              somethingChanged = true;
            }
          }
        }
      }

      if (HandleDependenciesOfDependencies())
      {
        somethingChanged = true;
      }

      if (somethingChanged)
      {
        globalDependenciesDefinition.Save();
        AssetDatabase.Refresh();
      }
    }

    private static bool HandleDependenciesOfDependencies()
    {
      var somethingChanged = false;
      var installedPackages = PackageUtils.GetInstalledPackages();
      foreach (var installedPackage in installedPackages)
      {
        if (installedPackage.dependencies.Length > 0)
        {
          // Debug.Log($"Installed package {installedPackage.name} has dependencies: {string.Join(", ", installedPackage.dependencies)}");
          foreach (var dependency in installedPackage.dependencies)
          {
            if (dependency.name.StartsWith("com.unity."))
            {
              continue;
            }
            // if (!PackageUtility.IsPackageInstalled(installedPackages, dependency.name))
            // {
            //   PackageUtility.InstallPackage(dependency.name, dependency.version);
            // }
            if (!PackageUtils.IsPackageInOpenUpmRegistryScope(dependency.name))
            {
              Debug.Log($"Scoped registry for {dependency.name} not found, adding it");
              if (PackageUtils.AddPackageToOpenUpmRegistryScopes(dependency.name))
              {
                somethingChanged = true;
              }
            }
          }
        }
      }
      return somethingChanged;
    }
  }
}
