from pathlib import Path
import re, uuid
root=Path(__file__).resolve().parents[2]
art=root/'Assets/Art/UI/Astral'
template=(root/'Assets/TutorialInfo/Icons/URP.png.meta').read_text()
guids={}
for name in ['panel','slot','hp_track','bar_fill','weapon_001_icon']:
 p=art/(name+'.png.meta')
 guid=re.search(r'guid: (\w+)',p.read_text()).group(1) if p.exists() else uuid.uuid4().hex
 guids[name]=guid
 t=template
 for key,value in {'guid':guid,'spriteMode':'1','textureType':'8','spriteMeshType':'0','filterMode':'1','spriteGenerateFallbackPhysicsShape':'0','spriteBorder':'{x: 16, y: 16, z: 16, w: 16}' if name in ['panel','slot','hp_track'] else '{x: 0, y: 0, z: 0, w: 0}'}.items():
  t=re.sub(r'(?m)^(\s*'+key+r':).*$',lambda m:m[1]+' '+value,t)
 p.write_text(t)
def sprite(name):return '{fileID: 21300000, guid: '+guids[name]+', type: 3}'
def block(s,id):
 m=re.search(r'^--- !u!\d+ &'+str(id)+r'\n.*?(?=^---|\Z)',s,re.M|re.S)
 assert m, id
 return m.group()
def edit(s,id,**fields):
 old=block(s,id);new=old
 for k,v in fields.items():
  new,n=re.subn(r'(?m)^(  '+re.escape(k)+r':).*$',lambda m:m[1]+' '+str(v),new)
  assert n==1,(id,k,n)
 return s.replace(old,new)
def rect(s,id,pos=None,size=None,amin=None,amax=None):
 d={}
 for k,v in [('m_AnchoredPosition',pos),('m_SizeDelta',size),('m_AnchorMin',amin),('m_AnchorMax',amax)]:
  if v is not None:d[k]='{x: %s, y: %s}'%v
 return edit(s,id,**d)
white='{r: 1, g: 1, b: 1, a: 1}'
p=root/'Assets/Prefabs/slot.prefab';s=p.read_text()
s=edit(s,8233696154162933693,m_Sprite=sprite('slot'),m_Color=white,m_Type=1)
s=edit(s,4850547210085134091,m_PreserveAspect=1,m_RaycastTarget=0)
s=rect(s,8932221801574123357,size=(64,64))
s=rect(s,340541375677305854,size=(-10,-10))
s=rect(s,2715991937371327626,pos=(-13,7),size=(30,16))
s=edit(s,9157388446484378802,m_fontSize=12,m_fontSizeBase=12,m_RaycastTarget=0,m_fontColor='{r: 0.96, g: 0.94, b: 0.88, a: 1}')
p.write_text(s)
p=root/'Assets/Configs/ItemSO/weapon_001.asset';s=p.read_text();s=re.sub(r'  icon: .*','  icon: '+sprite('weapon_001_icon'),s);p.write_text(s)
p=root/'Assets/Scenes/SampleScene.unity';s=p.read_text()
# Remove previous theme labels before rebuilding (safe to rerun).
for label_id in [900100,900101,900102,900103,900110,900111,900112,900113]:
 s=re.sub(r'^--- !u!\d+ &'+str(label_id)+r'\n.*?(?=^---|\Z)', '', s, flags=re.M|re.S)
for label_rt in [900101,900111]:
 s=s.replace('  - {fileID: '+str(label_rt)+'}\n','')
for id,name in [(1125455835,'panel'),(589708653,'panel'),(2053258642,'hp_track')]:
 s=edit(s,id,m_Sprite=sprite(name),m_Color=white,m_Type=1)
for id,color in [(824877552,'{r: 0.57, g: 0.77, b: 0.48, a: 1}'),(1635192724,'{r: 0.88, g: 0.76, b: 0.49, a: 1}')]:
 s=edit(s,id,m_Sprite=sprite('bar_fill'),m_Color=color,m_RaycastTarget=0)
s=rect(s,2002907023,pos=(-180,12),size=(320,360))
s=rect(s,1125455833,size=(320,360))
s=rect(s,177343207,pos=(16,-54),size=(-40,-76))
s=rect(s,1283502184,size=(0,0))
s=edit(s,1283502185,m_CellSize='{x: 60, y: 68}',m_Spacing='{x: 8, y: 10}',m_ConstraintCount=4)
s=edit(s,1125455834,m_Horizontal=0,m_ScrollSensitivity=22,m_VerticalScrollbarVisibility=1)
s=rect(s,2102639262,pos=(-12,-54),size=(5,-76))
s=edit(s,2102639264,m_Color='{r: 0.1, g: 0.14, b: 0.2, a: 0.7}') if '&2102639264\n' in s else s
s=rect(s,589708651,pos=(0,45),size=(320,72))
s=edit(s,589708652,m_CellSize='{x: 54, y: 54}',m_Spacing='{x: 8, y: 0}',m_ChildAlignment=4)
s=rect(s,2053258641,pos=(118,28),size=(200,14))
for id in [824877551,1635192723]:s=rect(s,id,size=(190,6))
s=rect(s,1756170780,pos=(0,18),size=(190,20))
s=edit(s,1756170781,m_text='HP',m_fontSize=12,m_fontSizeBase=12,m_fontColor='{r: 0.96, g: 0.94, b: 0.88, a: 1}',m_RaycastTarget=0)
# Add real TMP title and hint, using the scene's existing font/material.
for base,name,text,pos,size,fontsize in [(900100,'BagTitle','INVENTORY',(20,-26),(260,30),18),(900110,'BagHint','EQUIPMENT  /  TAB TO CLOSE',(20,18),(270,18),9)]:
 go=block(s,1756170779); rt=block(s,1756170780); tmp=block(s,1756170781); cr=block(s,1756170782)
 chunk=go+rt+tmp+cr
 for old,new in [(1756170779,base),(1756170780,base+1),(1756170781,base+2),(1756170782,base+3)]:chunk=chunk.replace(str(old),str(new))
 chunk=edit(chunk,base,m_Name=name)
 chunk=edit(chunk,base+1,m_Father='{fileID: 1125455833}',m_Pivot='{x: 0, y: 0.5}')
 anchor=(0,1) if name=='BagTitle' else (0,0)
 chunk=rect(chunk,base+1,pos=pos,size=size,amin=anchor,amax=anchor)
 chunk=edit(chunk,base+2,m_text=text,m_fontSize=fontsize,m_fontSizeBase=fontsize,m_HorizontalAlignment=1,m_characterSpacing=2)
 s=s.replace('--- !u!1660057539',chunk+'--- !u!1660057539') if '--- !u!1660057539' in s else s+chunk
 old=block(s,1125455833);s=s.replace(old,old.replace('  m_Children:\n','  m_Children:\n  - {fileID: '+str(base+1)+'}\n'))
p.write_text(s)
print('Saved sprites, weapon binding, slot prefab and SampleScene UI.')
