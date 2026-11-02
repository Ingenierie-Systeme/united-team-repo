import bpy,json,os
from mathutils import Vector, Matrix
cx=1.7557831;cy=2.4163821;dz=8.326045513153076
excluded={'Cube.007','Cube.012','Cube.013'}
original=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name not in excluded]
deps=bpy.context.evaluated_depsgraph_get();meshes=[]
for obj in original:
 evaluated=obj.evaluated_get(deps)
 mesh=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=deps)
 mesh.transform(Matrix.Translation(Vector((-cx,-cy,dz))) @ obj.matrix_world)
 new=bpy.data.objects.new('EXPORT_'+obj.name,mesh);bpy.context.collection.objects.link(new)
 meshes.append((obj.name,new))
for obj in list(bpy.context.scene.objects):
 if not obj.name.startswith('EXPORT_'):bpy.data.objects.remove(obj,do_unlink=True)
for name,obj in meshes:obj.name=name
meshes=[o for _,o in meshes]
# Exact boolean on the derived scene only; input .blend is never saved.
bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,-5))
cutter=bpy.context.object;cutter.name='CaveAccessCutter';cutter.dimensions=(3,3,14)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
floor=bpy.data.objects['Cube'];mod=floor.modifiers.new('CaveAccess_3m','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
bpy.context.view_layer.objects.active=floor
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.data.objects.remove(cutter,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:obj.select_set(True)
bpy.context.view_layer.objects.active=floor
bpy.ops.export_scene.fbx(filepath=os.path.abspath('Assets/CAD/BestPiscine/Derived/BestPiscineWithCaveAccess.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_mesh_modifiers=True)
json.dump({'sourceBasinCenter':[cx,cy],'floorTranslation':dz,'basinSize':[10.100923538208008,15.844802379608154],'floor':1,'rim':8.177817106246948,'excluded':sorted(excluded)},open('Assets/CAD/BestPiscine/Derived/geometry-audit.json','w'),indent=2)
print('EXPORTED_BEST_POOL')
