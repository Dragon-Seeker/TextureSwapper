using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using Logger = UnityEngine.Logger;
using Object = UnityEngine.Object;

namespace io.wispforest.textureswapper.utils;

// A ray caster that will create colliders from meshes in the scene
public class MeshRayCaster(ManualLogSource logger, Func<bool> isDebugMode) {
   
   public Component? raycast(float range) {
      // Collect all MeshRenderer to find possible renderers in the scene
      var allRenderers = Object.FindObjectsOfType<MeshRenderer>();

      if (allRenderers.isEmpty()) return null;
      
      var camera = Camera.main!;

      var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
      var planes = GeometryUtility.CalculateFrustumPlanes(camera);

      var validatedRenderers = new List<MeshRenderer>();
      
      foreach (var renderer in allRenderers) {
         if (!renderer.isVisible) continue;

         var bounds = renderer.bounds;
         
         // First rounds of checks to first see if:
         // 1. The renderer is inside the given Frustum culling field
         // 2. The renderer bounds intersects the look ray
         // 3. Is more than 0.5 from the camera point due to some odd stuff near the player
         if (GeometryUtility.TestPlanesAABB(planes, bounds) && bounds.IntersectRay(ray, out var dist) && dist > 0.5) {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            
            validatedRenderers.Add(renderer);
         }
      }

      var customLayerMaskName = 31;
      
      Dictionary<string, ColliderData> tempColliderInfo = new ();
      
      // Next step is to create a bunch of MeshColliders for the renderers, put them into a custom obj with the
      // orignal object as the parent, and then put it on the top most layer mask.
      foreach (var renderer in validatedRenderers) {
         var colliderInfo = addColliderOnCustomLayer(renderer, customLayerMaskName);

         if (colliderInfo == null) continue;
         
         tempColliderInfo[colliderInfo.key] = colliderInfo;
      }
      
      MeshRenderer? closestRenderer = null;
      
      // Finally attempt a raycast with the given meshes on the custom layer
      if (Physics.Raycast(ray, out RaycastHit hit, range, 1 << customLayerMaskName) && hit.collider is MeshCollider meshCollider) {
         var colliderInfo = tempColliderInfo[meshCollider.name];
            
         closestRenderer = colliderInfo.renderer;
            
         MeshWireframeRenderer.attachRenderer(Color.green, closestRenderer.gameObject, meshCollider.sharedMesh, true);
      }
      
      foreach (var colliderInfo in tempColliderInfo.Values) {
         Object.DestroyImmediate(colliderInfo.collider);
         Object.DestroyImmediate(colliderInfo.colliderObj);
      }
      
      return closestRenderer;
   }
   
   private ColliderData? addColliderOnCustomLayer(MeshRenderer renderer, int layerNumber) {
      var parentObj = renderer.gameObject;
      var sourceMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
      
      var readableMesh = !sourceMesh.isReadable ? createReadableMesh(parentObj.name, sourceMesh) : sourceMesh;

      if (readableMesh == null) return null;
      
      // Create Temporary Collider to use within the upcoming physics ray cast
      var colliderObj = new GameObject("TempColliderHolder");
      colliderObj.transform.SetParent(parentObj.transform);
      
      colliderObj.transform.localPosition = Vector3.zero;
      colliderObj.transform.localRotation = Quaternion.identity;
      colliderObj.transform.localScale = Vector3.one;
      
      colliderObj.layer = layerNumber;
      
      var meshFilter = colliderObj.GetComponent<MeshFilter>();
      Mesh? originalMesh = null;

      // Attempts to resolve issues with trying to create a mesh collider and grabbing an invalid mesh temporarily 
      if (meshFilter != null) {
         originalMesh = meshFilter.sharedMesh;
         meshFilter.sharedMesh = null; 
      }
      
      var collider = colliderObj.AddComponent<MeshCollider>();
      
      collider.sharedMesh = readableMesh;
      collider.convex = false; 
      
      if (isDebugMode()) logger.LogInfo($"[MeshFixer] Successfully rebuilt collider for {parentObj.name}");
      
      if (meshFilter != null) meshFilter.sharedMesh = originalMesh;

      return new ColliderData(colliderObj, collider, renderer);
   }
   
   private Mesh? createReadableMesh(string objName, Mesh sourceMesh) {
      if (isDebugMode()) logger.LogInfo($"Attempting to do a GPU Readback for mesh to get readable mesh info! {objName}");
      
      // Collect Vertex Info for the new Mesh
      var vertBuffer = sourceMesh.GetVertexBuffer(0);
      
      var vertReq = AsyncGPUReadback.Request(vertBuffer);

      AsyncGPUReadback.WaitAllRequests();
      
      if (vertReq.hasError) { 
         if (isDebugMode()) logger.LogError($"[MeshColliderMaker] GPU Readback failed for Vertex info! {objName}"); 
         return null;
      }
      
      if (isDebugMode()) logger.LogInfo($"[MeshColliderMaker] Attempting to extract Vertex info! {objName}"); 
      
      var newVerts = extractPositionsFromRawData(sourceMesh, vertReq.GetData<byte>());

      vertBuffer.Dispose();
      
      if (newVerts == null) {
         if (isDebugMode()) logger.LogError($"[MeshColliderMaker] Vertex info was found to be null due to an error! {objName}");
         return null;
      }
      
      //--
      
      // Collect indices Info of the vertices for the new Mesh
      var indexBuffer = sourceMesh.GetIndexBuffer();
      
      var indexReq = AsyncGPUReadback.Request(indexBuffer);
      
      AsyncGPUReadback.WaitAllRequests();
      
      if (indexReq.hasError) { 
         if (isDebugMode()) logger.LogError($"[MeshColliderMaker] GPU Readback failed for Index info! {objName}"); 
         return null;
      }
      
      if (isDebugMode()) logger.LogInfo($"[MeshColliderMaker] Attempting to extract Indices info! {objName}"); 
      
      var indicesInfo = extractIndicesFromRawData(sourceMesh, indexReq.GetData<byte>());
      
      indexBuffer.Dispose();

      if (indicesInfo == null) {
         if (isDebugMode()) logger.LogError($"[MeshColliderMaker] Index info was found to be null due to an error! {objName}");
         return null;
      }
      // --
      
      if (isDebugMode()) logger.LogInfo($"[MeshColliderMaker] Building new Mesh! {objName}"); 
      
      var targetMesh = new Mesh {
            // 65k+ vertices support if needed
            indexFormat = sourceMesh.indexFormat,
            name = sourceMesh.name + "_Fixed"
      };
   
      targetMesh.SetVertices(newVerts);
      targetMesh.SetIndices(indicesInfo.Value.indices, indicesInfo.Value.topology, 0);
      targetMesh.RecalculateBounds();

      return targetMesh;
   }
   
   private unsafe Vector3[]? extractPositionsFromRawData(Mesh mesh, NativeArray<byte> rawData) {
      // 1. Get the layout to find where "Position" lives
      var descriptors = new List<VertexAttributeDescriptor>(mesh.GetVertexAttributes());

      VertexAttributeDescriptor? descriptor = descriptors.FirstOrDefault(attr => attr.attribute == VertexAttribute.Position);
      
      if (descriptor == null) { 
         if (isDebugMode()) logger.LogError("Could not find Position attribute in mesh layout.");
         return null; 
      }

      var positionDescriptor = descriptor.Value;
      
      var positionOffset = mesh.GetVertexAttributeOffset(positionDescriptor.attribute);
      var format = positionDescriptor.format;
      var streamIndex = positionDescriptor.stream; // Positions are usually in stream 0

      // 2. Prepare for extraction
      var vertexCount = mesh.vertexCount;
      var stride = mesh.GetVertexBufferStride(streamIndex);
      var positions = new Vector3[vertexCount];
      
      //var array = rawData.ToArray();
      var basePointer = (byte*) rawData.GetUnsafeReadOnlyPtr();

      // 3. Iterate and Extract
      // We use a safe-ish approach with BitConverter, but UnsafeUtility is faster if you prefer.
      for (int i = 0; i < vertexCount; i++) {
         //int startByte = (i * stride) + positionOffset;
         var readAddress = basePointer + (i * stride) + positionOffset;

         float x, y, z;
         
         if (format == VertexAttributeFormat.Float32) { // Handle Float32 (Standard)
            x = *(float*) (readAddress);
            y = *(float*) (readAddress + 4);
            z = *(float*) (readAddress + 8);
         } else if (format == VertexAttributeFormat.Float16) { // Handle Float16 (Half-Precision - rare but possible in mobile games)
            x = Mathf.HalfToFloat(*(ushort*) (readAddress));
            y = Mathf.HalfToFloat(*(ushort*) (readAddress + 2));
            z = Mathf.HalfToFloat(*(ushort*) (readAddress + 4));
         } else {
            continue;
         }
         
         positions[i] = new Vector3(x, y, z);
      }

      return positions;
   }
   
   private unsafe (int[] indices, MeshTopology topology)? extractIndicesFromRawData(Mesh mesh, NativeArray<byte> rawIndexData) {
      var totalIndexCount = 0;
      MeshTopology? topology = null;
      for (var i = 0; i < mesh.subMeshCount; i++) {
         var subMesh = mesh.GetSubMesh(i);
         var subMeshTopology = subMesh.topology;
         
         if (!topology.HasValue) {
            topology = subMeshTopology;
         } else if (topology != subMeshTopology) {
            if (isDebugMode()) logger.LogError("Could not get indices for mesh as the mesh has differing topology's across sub meshes.");
            return null;
         }
         
         totalIndexCount += subMesh.indexCount;
      }

      if (totalIndexCount == 0 || topology == null) return null;
      
      var indices = new int[totalIndexCount];

      //var array = rawIndexData.ToArray();
      void* voidPtr = rawIndexData.GetUnsafeReadOnlyPtr();

      // 2. Parse the data 
      if (mesh.indexFormat == IndexFormat.UInt16) { // UInt16 - 2 bytes per index
         ushort* shortPtr = (ushort*)voidPtr;
         for (int i = 0; i < totalIndexCount; i++) {
            indices[i] = (int)shortPtr[i];
         }
      } else { // UInt32 - 4 bytes per index
         uint* intPtr = (uint*)voidPtr;
         for (int i = 0; i < totalIndexCount; i++) {
            indices[i] = (int)intPtr[i];
         }
      }

      return (indices, topology.Value);
   }
}

public class ColliderData {
   public string key { get; }
   public GameObject colliderObj { get; }
   public MeshCollider collider { get; }
   public MeshRenderer renderer { get; }
   
   public ColliderData(GameObject colliderObj, MeshCollider collider, MeshRenderer renderer) {
      this.colliderObj = colliderObj;
      this.collider = collider;
      this.renderer = renderer;
      this.key = collider.GetHashCode().ToString();
      collider.name = this.key;
   }
}

public class MeshWireframeRenderer : MonoBehaviour {
   private Color wireColor = Color.green;
   
   private Material? lineMaterial;
   private Vector3[] renderVerts;
   private List<int> renderLines;
   private bool isReady;
   
   public static MeshWireframeRenderer attachRenderer(Color wireColor, GameObject targetObj, Mesh mesh, bool shortLifeSpan = false) {
      var debugVis = targetObj.AddComponent<MeshWireframeRenderer>();
      debugVis.setup(wireColor, mesh);
    
      // Optional: Destroy visualizer after 10 seconds so it doesn't clutter the screen forever
      if(shortLifeSpan) Destroy(debugVis, 10f);

      return debugVis;
   }

   public void setup(Color wireColor, Mesh mesh) { 
      if (mesh == null || !mesh.isReadable) return;
      this.wireColor = wireColor; 
      this.renderVerts = mesh.vertices; 

      var triangles = mesh.triangles; 
      renderLines = new List<int>(triangles.Length * 2);

      // Triangle A-B-C
      for (int i = 0; i < triangles.Length; i += 3) {
         // Lines A-B
         renderLines.Add(triangles[i]);
         renderLines.Add(triangles[i + 1]);

         // B-C
         renderLines.Add(triangles[i + 1]);
         renderLines.Add(triangles[i + 2]);

         // C-A
         renderLines.Add(triangles[i + 2]);
         renderLines.Add(triangles[i]);
      }
      
      isReady = true;
   }
   
   private Material getLineMaterial() {
      if (lineMaterial != null) return lineMaterial;
      
      // Use the built-in "Hidden/Internal-Colored" shader which is standard for Gizmos
      // If that fails, fallback to Sprites-Default
      var shader = Shader.Find("Hidden/Internal-Colored");
      if (!shader) shader = Shader.Find("Sprites/Default");
      
      // Turn on "ZTest Always" to make it X-Ray (visible through walls)
      // Optional: Add additive blending for "glowing" look
      lineMaterial = new Material(shader);
      lineMaterial.hideFlags = HideFlags.HideAndDontSave;

      lineMaterial.SetInt("_ZTest", (int) CompareFunction.Always);
      lineMaterial.SetInt("_SrcBlend", (int) BlendMode.SrcAlpha);
      lineMaterial.SetInt("_DstBlend", (int) BlendMode.OneMinusSrcAlpha);
      lineMaterial.SetInt("_Cull", (int) CullMode.Off);
      lineMaterial.SetInt("_ZWrite", 0);

      return lineMaterial;
   }
   
   private void OnRenderObject() { 
      if (!isReady) return;

      var material = getLineMaterial();
      
      material.SetPass(0);
      material.SetColor("_Color", wireColor);

     // 2. Push the object's transformation matrix
      GL.PushMatrix();
      GL.MultMatrix(transform.localToWorldMatrix);

     // 3. Draw the lines
      GL.Begin(GL.LINES);
      GL.Color(wireColor);

      for (int i = 0; i < renderLines.Count; i+=2) {
         GL.Vertex(renderVerts[renderLines[i]]);
         GL.Vertex(renderVerts[renderLines[i+1]]);
      }
      
      GL.End(); 
      GL.PopMatrix();
   }
   
   private void OnDestroy() { 
      if (lineMaterial != null) Destroy(lineMaterial);
   }
}