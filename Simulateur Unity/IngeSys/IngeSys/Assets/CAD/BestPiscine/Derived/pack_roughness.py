import bpy,json,numpy as np,os
spec=json.load(open('Assets/CAD/BestPiscine/Derived/materials-audit.json'))
for m in spec:
 prop=m['textures'].get('_MetallicGlossMap')
 if not prop:continue
 name=prop['texture'];path=os.path.join('Assets/CAD/BestPiscine/Textures',name)
 if 'rough' not in name:continue
 im=bpy.data.images.load(path,check_existing=False);im.colorspace_settings.name='Non-Color';im.scale(1024,1024)
 a=np.empty(1024*1024*4,dtype=np.float32);im.pixels.foreach_get(a);p=a.reshape(-1,4);r=np.clip(p[:,0],0,1).copy();p[:,:3]=0;p[:,3]=1-r
 out=bpy.data.images.new('mask',width=1024,height=1024,alpha=True);out.colorspace_settings.name='Non-Color';out.pixels.foreach_set(a);out.file_format='PNG';out.filepath_raw=os.path.abspath('Assets/CAD/BestPiscine/Derived/'+m['name']+'_MetallicSmoothness.png');out.save();bpy.data.images.remove(out);bpy.data.images.remove(im)
print('PACKED_ROUGHNESS')
