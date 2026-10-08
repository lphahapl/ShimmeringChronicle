using System; using System.IO; using System.Linq; using System.Collections.Generic; using UnityEditor; using UnityEngine;
public static class PrepareMossstoneV2
{
 public static void Run()
 {
  if(!Application.dataPath.Replace('\\','/').EndsWith("/Temp/MossstoneV2Build/Assets"))throw new InvalidOperationException("Fixture-only bootstrap: do not run against the main project.");
  var scripts=new Dictionary<string,MonoScript>();
  foreach(var path in new[]{"Assets/Plugins/UnityEngine.UI.dll","Assets/Plugins/Unity.TextMeshPro.dll"})
  foreach(var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<MonoScript>())if(m.GetClass()!=null)scripts[m.GetClass().Name]=m;
  var changes=new Dictionary<string,string>();var report=new List<string>();
  foreach(var line in File.ReadAllLines("source-script-map.tsv"))
  {
   var row=line.Split('\t');if(row.Length!=2||!scripts.TryGetValue(row[1],out var script))continue;
   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script,out string guid,out long local);
   string oldRef="{fileID: 11500000, guid: "+row[0]+", type: 3}",newRef="{fileID: "+local+", guid: "+guid+", type: 3}";
   changes[oldRef]=newRef;report.Add(newRef+"\t"+oldRef);
  }
  foreach(var path in Directory.GetFiles("Assets","*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".prefab")||p.EndsWith(".asset")))
  {var s=File.ReadAllText(path);var original=s;foreach(var pair in changes)s=s.Replace(pair.Key,pair.Value);if(s!=original)File.WriteAllText(path,s);}
  File.WriteAllLines("script-remap.tsv",report);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  BuildMossstoneSentinelV2.Build();
 }
}
