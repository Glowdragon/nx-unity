using System;
using System.Collections.Generic;

namespace NxUnity
{
  [Serializable]
  public class PackageManifest
  {
    public SerializableDictionary dependencies;
    public List<ScopedRegistry> scopedRegistries;

    [Serializable]
    public class SerializableDictionary : Dictionary<string, string> { }

    [Serializable]
    public class ScopedRegistry
    {
      public string name;
      public string url;
      public List<string> scopes;
    }
  }
}
