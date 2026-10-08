from pathlib import Path
import re, shutil
root=Path.cwd(); fixture=root/'Temp/MossstoneV2Build'
for src in (root/'Assets/Scripts').rglob('*'):
    if not src.is_file(): continue
    dst=fixture/src.relative_to(root); dst.parent.mkdir(parents=True,exist_ok=True)
    if src.suffix=='.cs':
        raw=src.read_bytes()
        try: text=raw.decode('utf-8-sig')
        except UnicodeDecodeError: text=raw.decode('gbk')
        text=re.sub(r'^using (?:static UnityEngine.Rendering.STP|UnityEditor.ShaderGraph.Internal);\s*$', '',text,flags=re.M)
        dst.write_text(text,encoding='utf-8')
    else: shutil.copy2(src,dst)
for name in ['BuildMossstoneSentinelV2.cs','StanceAndStrideSentinel.cs','ReviseReinforcedSentinel.cs','ValidateMossstoneV2Play.cs','PreviewMossstoneV2.cs']:
    shutil.copy2(root/'Tools/Enemies/MossstoneSentinelV2'/name,fixture/'Assets/Editor'/name)
print('Synced current gameplay sources and stance tools to isolated fixture.')
