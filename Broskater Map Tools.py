bl_info = {
    "name": "BroSkater Map Tools",
    "author": "You + ChatGPT",
    "version": (0, 1),
    "blender": (3, 0, 0),
    "location": "View3D > Sidebar > BroSkater",
    "description": "Tag edges and faces as grindable or vert ramps for Unity export.",
    "category": "Object",
}

import bpy
import bmesh

def ensure_vertex_group(obj, group_name):
    vg = obj.vertex_groups.get(group_name)
    if not vg:
        vg = obj.vertex_groups.new(name=group_name)
    return vg

def ensure_vertex_colors(obj, color_name):
    # Check if it already exists
    color_attr = obj.data.color_attributes.get(color_name)
    if not color_attr:
        print(f"Color attribute '{color_name}' not found, creating...")
        # Try to create it
        try:
            color_attr_new = obj.data.color_attributes.new(
                name=color_name,
                type='FLOAT_COLOR',
                domain='POINT'
            )
            # Important: Re-fetch it after creation to ensure we have the correct reference
            color_attr = obj.data.color_attributes.get(color_name)
            if color_attr:
                print(f"Successfully created and retrieved '{color_name}'.")
            else:
                 print(f"ERROR: Failed to retrieve '{color_name}' immediately after creation.")
                 return None # Indicate failure
        except Exception as e:
            print(f"ERROR: Failed to create color attribute '{color_name}': {e}")
            return None # Indicate failure
            
    # Return the found or newly created attribute
    return color_attr

def tag_edges_as_grindable(context):
    obj = context.active_object
    if not obj or obj.type != 'MESH':
        print("No active mesh object selected.")
        return
        
    if obj.mode != 'EDIT':
        print("Must be in Edit Mode.")
        return

    # --- Step 1: Read Selection in Edit Mode --- 
    bm = bmesh.from_edit_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    grind_verts_indices = set()

    selected_edges = [edge for edge in bm.edges if edge.select]
    if not selected_edges:
         print("No edges selected.")
         # No need to free bm from edit mesh
         return

    for edge in selected_edges:
        for vert in edge.verts:
            grind_verts_indices.add(vert.index)
            
    # We have the indices, no longer need edit mode bmesh
    # bm.free() # Don't free edit mesh bmesh

    # --- Step 2: Switch to Object Mode for Modifications --- 
    bpy.ops.object.mode_set(mode='OBJECT')
    
    # --- Step 3: Modify Vertex Group --- 
    vg = ensure_vertex_group(obj, "GRIND")
    all_vert_indices = [v.index for v in obj.data.vertices]
    try:
        vg.remove(all_vert_indices)
    except RuntimeError:
        pass 
    if grind_verts_indices:
        vg.add(list(grind_verts_indices), 1.0, 'REPLACE')

    # --- Step 4: Modify Vertex Colors --- 
    color_attr = ensure_vertex_colors(obj, "GrindWeights")
    if not color_attr:
        print("ERROR: Failed to ensure GrindWeights color attribute exists. Aborting color setting.")
        bpy.ops.object.mode_set(mode='EDIT') # Go back to edit mode
        return
        
    num_verts = len(obj.data.vertices)
    # Ensure color data size matches vertex count (important after potential creation)
    if len(color_attr.data) != num_verts:
         print(f"Warning [Grind]: Color attribute data size ({len(color_attr.data)}) != vertex count ({num_verts}). Attempting resize implicitly.")
         # In newer Blender, accessing might resize implicitly, but let's be cautious
         # Explicit resize isn't straightforward. Rely on access/assignment to handle it.

    # Clear all vertex colors
    print(f"Clearing {num_verts} entries in GrindWeights...")
    for v_idx in range(num_verts):
         if v_idx < len(color_attr.data): # Check against actual color data size
             color_attr.data[v_idx].color = (0, 0, 0, 0)
         # No else needed, we iterated based on vertex count primarily

    # Set colors for selected vertices
    print(f"Setting GrindWeights color for {len(grind_verts_indices)} vertices...")
    for vert_idx in grind_verts_indices:
        if vert_idx < len(color_attr.data):
            color_attr.data[vert_idx].color = (1, 1, 1, 1) 
        else:
             print(f"Warning [Grind]: Vertex index {vert_idx} out of range for color data (size {len(color_attr.data)}).")
             
    obj.data.update() 

    # --- Step 5: Return to Edit Mode --- 
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.context.tool_settings.mesh_select_mode = (False, True, False) 
    print(f"Tagged {len(grind_verts_indices)} vertices from {len(selected_edges)} edges as GRIND (vertex group + colors 'GrindWeights')")

def tag_faces_as_vert(context):
    obj = context.active_object
    if not obj or obj.type != 'MESH':
        print("No active mesh object selected.")
        return

    if obj.mode != 'EDIT':
        print("Must be in Edit Mode.")
        return
        
    # --- Step 1: Read Selection in Edit Mode --- 
    bm = bmesh.from_edit_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table() 
    
    vert_verts_indices = set()
    selected_faces = [f for f in bm.faces if f.select]

    if not selected_faces:
        print("No faces selected.")
        # No need to free bm from edit mesh
        return

    for face in selected_faces:
        for vert in face.verts:
            vert_verts_indices.add(vert.index)
            
    # We have the indices, no longer need edit mode bmesh
    # bm.free() # Don't free edit mesh bmesh

    # --- Step 2: Switch to Object Mode for Modifications --- 
    bpy.ops.object.mode_set(mode='OBJECT') 

    # --- Step 3: Modify Vertex Group --- 
    vg = ensure_vertex_group(obj, "VERT")
    all_vert_indices = [v.index for v in obj.data.vertices]
    try:
        vg.remove(all_vert_indices)
    except RuntimeError:
        pass 
    if vert_verts_indices:
        vg.add(list(vert_verts_indices), 1.0, 'REPLACE')

    # --- Step 4: Modify Vertex Colors --- 
    color_attr = ensure_vertex_colors(obj, "VertWeights")
    if not color_attr:
        print("ERROR: Failed to ensure VertWeights color attribute exists. Aborting color setting.")
        bpy.ops.object.mode_set(mode='EDIT')
        return

    num_verts = len(obj.data.vertices)
    # Ensure color data size matches vertex count
    if len(color_attr.data) != num_verts:
         print(f"Warning [Vert]: Color attribute data size ({len(color_attr.data)}) != vertex count ({num_verts}). Attempting resize implicitly.")

    # Clear all vertex colors
    print(f"Clearing {num_verts} entries in VertWeights...")
    for v_idx in range(num_verts):
         if v_idx < len(color_attr.data):
             color_attr.data[v_idx].color = (0, 0, 0, 0) 

    # Set colors for selected vertices
    print(f"Setting VertWeights color for {len(vert_verts_indices)} vertices...")
    for vert_idx in vert_verts_indices:
        if vert_idx < len(color_attr.data):
            color_attr.data[vert_idx].color = (0, 1, 0, 1) 
        else:
             print(f"Warning [Vert]: Vertex index {vert_idx} out of range for color data (size {len(color_attr.data)}).")
                
    obj.data.update()

    # --- Step 5: Return to Edit Mode --- 
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.context.tool_settings.mesh_select_mode = (False, False, True) # Ensure face select mode
    print(f"Tagged {len(vert_verts_indices)} vertices from {len(selected_faces)} faces as VERT (vertex group + colors 'VertWeights')")


class BSKT_OT_MakeGrindable(bpy.types.Operator):
    bl_idname = "bskater.make_grindable"
    bl_label = "Make Grindable"
    bl_description = "Tag selected edges as grindable"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        tag_edges_as_grindable(context)
        return {'FINISHED'}

class BSKT_OT_MakeVert(bpy.types.Operator):
    bl_idname = "bskater.make_vert"
    bl_label = "Make Vert"
    bl_description = "Tag selected faces as vert ramps"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        tag_faces_as_vert(context)
        return {'FINISHED'}

class BSKT_PT_Panel(bpy.types.Panel):
    bl_label = "BroSkater Map Tools"
    bl_idname = "BSKT_PT_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'BroSkater'

    @classmethod
    def poll(cls, context):
        return context.object and context.object.mode == 'EDIT'

    def draw(self, context):
        layout = self.layout
        layout.operator("bskater.make_grindable", icon="EDGESEL")
        layout.operator("bskater.make_vert", icon="FACESEL")

        # Add export reminder
        box = layout.box()
        box.label(text="Export Settings:", icon="EXPORT")
        box.label(text="✓ Selected Objects")
        box.label(text="✓ Vertex Colors")

classes = (
    BSKT_OT_MakeGrindable,
    BSKT_OT_MakeVert,
    BSKT_PT_Panel,
)

def register():
    for cls in classes:
        bpy.utils.register_class(cls)

def unregister():
    for cls in classes:
        bpy.utils.unregister_class(cls)

if __name__ == "__main__":
    register()
