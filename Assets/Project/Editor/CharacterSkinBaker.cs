using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Railbound.Editor
{
    /// <summary>
    /// Turns a rigged humanoid model (a .glb / .fbx with Mixamo bone names) into a "skin" prefab that drops into the existing
    /// player-character pipeline.
    ///
    /// The game already drives the soldier model <c>Anim_Idle_MIL_R5</c> (Player_1) from the gameplay rig with
    /// <c>CharacterSkinRetargeter</c>, hand IK, leg IK and ragdoll. Rather than calibrate a second retargeter for every model,
    /// the baker moves the NEW model's meshes onto the SAME skeleton: every vertex is transformed into the MIL-R5 rest pose
    /// (scale, facing and position aligned from the bones) and the bone weights are re-pointed, by bone name, at the MIL-R5 bones.
    /// The result is a copy of the MIL-R5 prefab whose skinned meshes are the new model's, so the whole animation pipeline works
    /// unchanged. The output is written to Assets/Project/character/Skins.
    /// </summary>
    public static class CharacterSkinBaker
    {
        public const string MilPrefabPath = "Assets/Project/character/Player_1/Prefab/Anim_Idle_MIL_R5.prefab";
        public const string OutputDir = "Assets/Project/character/Skins";

        private static string Norm(string boneName)
        {
            // glTF import appends the node index ("mixamorig:Hips_64"); some rigs use another namespace ("swat:Hips")
            string n = Regex.Replace(boneName, @"_\d+$", "");
            int colon = n.IndexOf(':');
            return colon > 0 ? "mixamorig:" + n.Substring(colon + 1) : n;
        }

        /// <summary>
        /// Puts the model's skeleton into its true bind pose (the pose the mesh was skinned in). A model exported together with an
        /// animation stands in that animation's first frame instead; baking from it would bake the stance into the shape.
        /// </summary>
        private static void ForceBindPose(GameObject root)
        {
            var bindL2W = new Dictionary<Transform, Matrix4x4>();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                var bp = smr.sharedMesh.bindposes; var bones = smr.bones; Matrix4x4 sw = smr.transform.localToWorldMatrix;
                for (int j = 0; j < bones.Length && j < bp.Length; j++)
                    if (bones[j] != null && !bindL2W.ContainsKey(bones[j])) bindL2W[bones[j]] = (bp[j] * sw.inverse).inverse;   // bone bind local-to-world
            }
            var ordered = new List<Transform>(root.GetComponentsInChildren<Transform>(true));   // parents come before their children
            foreach (var t in ordered)
                if (bindL2W.TryGetValue(t, out var m)) { t.position = new Vector3(m.m03, m.m13, m.m23); t.rotation = m.rotation; }
        }

        private static Vector3 Pos(Matrix4x4 m) { return new Vector3(m.m03, m.m13, m.m23); }

        /// <summary>Bakes one skin. Returns a report line; the prefab is <c>OutputDir/Skin_&lt;id&gt;.prefab</c>.</summary>
        /// <param name="materialOverrides">Optional: materials to use instead of the model's own, by the name of the original material.</param>
        public static string Bake(string id, string sourcePath, Dictionary<string, Material> materialOverrides = null)
        {
            var log = new StringBuilder();
            var milAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MilPrefabPath);
            var srcAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (milAsset == null || srcAsset == null) return "missing asset (mil=" + (milAsset != null) + ", source=" + (srcAsset != null) + ")";

            EnsureFolder(OutputDir);
            EnsureFolder(OutputDir + "/Meshes");

            var mil = (GameObject)PrefabUtility.InstantiatePrefab(milAsset);
            PrefabUtility.UnpackPrefabInstance(mil, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            mil.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var src = (GameObject)Object.Instantiate(srcAsset);
            src.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ForceBindPose(src);

            try
            {
                // ---- MIL-R5 bind-pose bone matrices (world space, root at the origin) and bone transforms by name
                var milW2L = new Dictionary<string, Matrix4x4>();
                var milBone = new Dictionary<string, Transform>();
                foreach (var t in mil.GetComponentsInChildren<Transform>(true)) if (!milBone.ContainsKey(t.name)) milBone[t.name] = t;
                var milSmrs = mil.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in milSmrs)
                {
                    var bp = smr.sharedMesh.bindposes; var bones = smr.bones;
                    Matrix4x4 smrL2W = smr.transform.localToWorldMatrix;
                    for (int j = 0; j < bones.Length && j < bp.Length; j++)
                        if (bones[j] != null && !milW2L.ContainsKey(bones[j].name)) milW2L[bones[j].name] = bp[j] * smrL2W.inverse;   // bind = boneW2L * smrL2W
                }

                // ---- source bind-pose bone matrices
                var srcSmrs = src.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (srcSmrs.Length == 0) return "source has no skinned meshes (it is not rigged)";
                // The imported model stands in its bind pose, so a bone's current matrix is its bind matrix.
                var srcL2W = new Dictionary<string, Matrix4x4>();
                foreach (var t in src.GetComponentsInChildren<Transform>(true)) { string n = Norm(t.name); if (!srcL2W.ContainsKey(n)) srcL2W[n] = t.localToWorldMatrix; }

                bool Need(string n, out Vector3 milPos, out Vector3 srcPos)
                {
                    milPos = srcPos = Vector3.zero;
                    if (!milW2L.TryGetValue(n, out var mw) || !srcL2W.TryGetValue(n, out var sw)) return false;
                    milPos = Pos(mw.inverse); srcPos = Pos(sw);
                    return true;
                }

                if (!Need("mixamorig:Hips", out var mHips, out var sHips) || !Need("mixamorig:Head", out var mHead, out var sHead)
                    || !Need("mixamorig:LeftFoot", out var mLF, out var sLF) || !Need("mixamorig:RightFoot", out var mRF, out var sRF)
                    || !Need("mixamorig:LeftUpLeg", out var mLU, out var sLU) || !Need("mixamorig:RightUpLeg", out var mRU, out var sRU))
                    return "the source skeleton does not have the standard Mixamo bones (Hips, Head, feet, upper legs)";

                float milH = mHead.y - (mLF.y + mRF.y) * 0.5f, srcH = sHead.y - (sLF.y + sRF.y) * 0.5f;
                float scale = srcH > 0.0001f ? milH / srcH : 1f;
                Vector3 milLat = mRU - mLU; milLat.y = 0f;
                Vector3 srcLat = sRU - sLU; srcLat.y = 0f;
                float yaw = Vector3.SignedAngle(srcLat, milLat, Vector3.up);
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 srcHipsScaled = rot * (sHips * scale);
                Vector3 trans = new Vector3(mHips.x - srcHipsScaled.x, (mLF.y + mRF.y) * 0.5f - ((sLF.y + sRF.y) * 0.5f * scale), mHips.z - srcHipsScaled.z);
                Matrix4x4 align = Matrix4x4.TRS(trans, rot, Vector3.one * scale);
                log.AppendLine("align: scale=" + scale.ToString("F3") + " yaw=" + yaw.ToString("F1") + " trans=" + trans);

                // ---- pose normalisation: the model may stand in a relaxed pose while the MIL-R5 skeleton binds in a T-pose. Re-pose its
                // shoulders, arms and legs (forward kinematics on its own skeleton) so their directions match the MIL-R5 bind pose;
                // the vertices are then skinned from that pose, so animating the shared skeleton bends the limbs the right way.
                var srcT = new Dictionary<string, Transform>();
                foreach (var t in src.GetComponentsInChildren<Transform>(true)) { string n0 = Norm(t.name); if (!srcT.ContainsKey(n0)) srcT[n0] = t; }
                var tgtPos = new Dictionary<Transform, Vector3>(); var tgtRot = new Dictionary<Transform, Quaternion>();
                foreach (var t in srcT.Values) { tgtPos[t] = t.position; tgtRot[t] = t.rotation; }
                Quaternion invRot = Quaternion.Inverse(rot);
                string[][] chains =
                {
                    new[] { "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "LeftHandMiddle1" },
                    new[] { "RightShoulder", "RightArm", "RightForeArm", "RightHand", "RightHandMiddle1" },
                    new[] { "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase" },
                    new[] { "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase" }
                };
                // the fingers too: a relaxed (curled) hand would otherwise be bound as if it were the straight T-pose hand
                var allChains = new List<string[]>(chains);
                foreach (var side in new[] { "Left", "Right" })
                    foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
                        allChains.Add(new[] { side + "Hand" + finger + "1", side + "Hand" + finger + "2", side + "Hand" + finger + "3", side + "Hand" + finger + "4" });
                int reposed = 0;
                foreach (var chain in allChains)
                {
                    for (int c = 0; c < chain.Length - 1; c++)
                    {
                        string a = "mixamorig:" + chain[c], b = "mixamorig:" + chain[c + 1];
                        if (!srcT.TryGetValue(a, out var ta) || !srcT.TryGetValue(b, out var tb) || !milW2L.ContainsKey(a) || !milW2L.ContainsKey(b)) continue;
                        Vector3 cur = tgtPos[tb] - tgtPos[ta];
                        Vector3 des = invRot * (Pos(milW2L[b].inverse) - Pos(milW2L[a].inverse));
                        if (cur.sqrMagnitude < 1e-8f || des.sqrMagnitude < 1e-8f) continue;
                        Quaternion q = Quaternion.FromToRotation(cur, des);
                        Vector3 pivot = tgtPos[ta];
                        foreach (var d in ta.GetComponentsInChildren<Transform>(true))
                        {
                            if (!tgtPos.ContainsKey(d)) { tgtPos[d] = d.position; tgtRot[d] = d.rotation; }
                            tgtPos[d] = pivot + q * (tgtPos[d] - pivot);
                            tgtRot[d] = q * tgtRot[d];
                        }
                        reposed++;
                    }
                }
                log.AppendLine("re-posed limb links: " + reposed);

                // ---- remove the MIL-R5 meshes (the skeleton stays)
                foreach (var smr in milSmrs) Object.DestroyImmediate(smr.gameObject);

                var hips = milBone["mixamorig:Hips"];
                int made = 0, missingBones = 0;
                foreach (var smr in srcSmrs)
                {
                    var srcMesh = smr.sharedMesh;
                    if (srcMesh == null) continue;
                    // Where every vertex really stands: skin it on the CPU with the model's current (standing) pose, exactly what the GPU
                    // does (sum of weight * bone.localToWorld * bindpose). This does not depend on how the importer set up transforms.
                    var srcBp = srcMesh.bindposes; var srcBn = smr.bones; var bw = srcMesh.boneWeights;
                    var skinM = new Matrix4x4[srcBn.Length];
                    for (int j = 0; j < srcBn.Length; j++)
                    {
                        if (srcBn[j] == null || j >= srcBp.Length) { skinM[j] = Matrix4x4.identity; continue; }
                        Matrix4x4 rest = srcBn[j].localToWorldMatrix * srcBp[j];                           // mesh space -> rest world
                        Matrix4x4 target = tgtPos.ContainsKey(srcBn[j])
                            ? Matrix4x4.TRS(tgtPos[srcBn[j]], tgtRot[srcBn[j]], srcBn[j].lossyScale)       // the bone in the re-posed skeleton
                            : srcBn[j].localToWorldMatrix;
                        skinM[j] = target * srcBn[j].worldToLocalMatrix * rest;                             // rest world -> bone local -> target world
                    }
                    Matrix4x4 nrm = Matrix4x4.Rotate(align.rotation);

                    var mesh = new Mesh { name = id + "_" + smr.name };
                    mesh.indexFormat = srcMesh.indexFormat;
                    var v = srcMesh.vertices; var n = srcMesh.normals; var tg = srcMesh.tangents;
                    bool hasN = n.Length == v.Length, hasT = tg.Length == v.Length, hasW = bw.Length == v.Length;
                    for (int i = 0; i < v.Length; i++)
                    {
                        Vector3 pw = Vector3.zero, nw = Vector3.zero, tw = Vector3.zero; float wsum = 0f;
                        if (hasW)
                        {
                            var w = bw[i];
                            void Add(int idx, float wt)
                            {
                                if (wt <= 0f || idx >= skinM.Length) return;
                                pw += wt * skinM[idx].MultiplyPoint3x4(v[i]); wsum += wt;
                                if (hasN) nw += wt * skinM[idx].MultiplyVector(n[i]);
                                if (hasT) tw += wt * skinM[idx].MultiplyVector(new Vector3(tg[i].x, tg[i].y, tg[i].z));
                            }
                            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                        }
                        if (wsum <= 0f) { pw = skinM.Length > 0 ? skinM[0].MultiplyPoint3x4(v[i]) : v[i]; if (hasN) nw = n[i]; if (hasT) tw = new Vector3(tg[i].x, tg[i].y, tg[i].z); }
                        v[i] = align.MultiplyPoint3x4(pw);
                        if (hasN) n[i] = nrm.MultiplyVector(nw).normalized;
                        if (hasT) { Vector3 tv = nrm.MultiplyVector(tw).normalized; tg[i] = new Vector4(tv.x, tv.y, tv.z, tg[i].w); }
                    }
                    mesh.vertices = v;
                    if (hasN) mesh.normals = n;
                    if (hasT) mesh.tangents = tg;
                    mesh.uv = srcMesh.uv; mesh.uv2 = srcMesh.uv2; mesh.colors = srcMesh.colors;
                    mesh.subMeshCount = srcMesh.subMeshCount;
                    for (int s = 0; s < srcMesh.subMeshCount; s++) mesh.SetTriangles(srcMesh.GetTriangles(s), s);
                    mesh.boneWeights = srcMesh.boneWeights;

                    // the weights keep their indices; the bones array now holds the MIL-R5 bones with the MIL-R5 bind matrices
                    var srcBones = srcBn;
                    var newBones = new Transform[srcBones.Length];
                    var newBind = new Matrix4x4[srcBones.Length];
                    for (int j = 0; j < srcBones.Length; j++)
                    {
                        Transform walk = srcBones[j];
                        Transform found = null; Matrix4x4 w2l = Matrix4x4.identity;
                        while (walk != null)
                        {
                            string nn = Norm(walk.name);
                            if (milBone.TryGetValue(nn, out var mb) && milW2L.TryGetValue(nn, out w2l)) { found = mb; break; }
                            walk = walk.parent;
                        }
                        if (found == null) { found = hips; w2l = milW2L["mixamorig:Hips"]; missingBones++; }
                        newBones[j] = found;
                        newBind[j] = w2l;
                    }
                    mesh.bindposes = newBind;
                    mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, OutputDir + "/Meshes/" + id + "_" + Sanitize(smr.name) + "_" + made + ".asset");

                    var go = new GameObject(smr.name);
                    go.transform.SetParent(mil.transform, false);
                    var ns = go.AddComponent<SkinnedMeshRenderer>();
                    ns.sharedMesh = mesh;
                    var mats = smr.sharedMaterials;
                    if (materialOverrides != null) for (int mi = 0; mi < mats.Length; mi++) if (mats[mi] != null && materialOverrides.TryGetValue(mats[mi].name, out var om) && om != null) mats[mi] = om;
                    ns.sharedMaterials = mats;
                    ns.bones = newBones;
                    ns.rootBone = hips;
                    // localBounds is in the ROOT BONE's local space (the MIL-R5 hips are scaled x100): express the world-space mesh bounds there,
                    // generously, or the renderer is culled in the wrong place
                    var wb = mesh.bounds; wb.Expand(0.6f);
                    var lb = new Bounds(hips.worldToLocalMatrix.MultiplyPoint3x4(wb.center), Vector3.zero);
                    for (int cx = -1; cx <= 1; cx += 2) for (int cy = -1; cy <= 1; cy += 2) for (int cz = -1; cz <= 1; cz += 2)
                        lb.Encapsulate(hips.worldToLocalMatrix.MultiplyPoint3x4(wb.center + Vector3.Scale(wb.extents, new Vector3(cx, cy, cz))));
                    ns.localBounds = lb;
                    ns.updateWhenOffscreen = false;
                    ns.quality = SkinQuality.Auto;
                    ns.shadowCastingMode = smr.shadowCastingMode;
                    made++;
                }
                log.AppendLine("meshes=" + made + " bones not found (fell back to hips)=" + missingBones);

                // loose (non-skinned) accessories of the source that hang on a bone
                int rigid = 0;
                foreach (var mr in src.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Transform p = mr.transform.parent; Transform target = null; Transform sourceBone = null;
                    while (p != null) { if (milBone.TryGetValue(Norm(p.name), out var mb2) && Norm(p.name).StartsWith("mixamorig:")) { target = mb2; sourceBone = p; break; } p = p.parent; }
                    if (target == null)
                    {
                        // not parented to a bone (hair, a prop): hang it on the bone nearest to it
                        Vector3 where = align.MultiplyPoint3x4(mr.bounds.center); float bestD = float.MaxValue;
                        foreach (var kv in milW2L) { if (!kv.Key.StartsWith("mixamorig:") || !milBone.ContainsKey(kv.Key)) continue; float d = (Pos(kv.Value.inverse) - where).sqrMagnitude; if (d < bestD) { bestD = d; target = milBone[kv.Key]; } }
                        if (target == null) continue;
                    }
                    var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    var go = new GameObject(mr.name);
                    go.transform.SetParent(target, false);
                    float toTarget = scale / Mathf.Max(0.0001f, target.lossyScale.x);
                    if (sourceBone != null)
                    {
                        // a mesh that hangs on a bone in the source (hair on the head) keeps its pose RELATIVE TO THAT BONE. Taking its world
                        // bind pose instead added the small difference between the two skeletons' bone rotations: Cammy's hair mesh has its
                        // pivot at her feet, so a 6 degree tilt of the head put the hair about 20 cm beside the head.
                        Matrix4x4 rel = sourceBone.worldToLocalMatrix * mr.transform.localToWorldMatrix;
                        go.transform.localPosition = (Vector3)rel.GetColumn(3) * (sourceBone.lossyScale.x * toTarget);
                        go.transform.localRotation = rel.rotation;
                        go.transform.localScale = Vector3.one * (mr.transform.lossyScale.x * toTarget);
                    }
                    else
                    {
                        Matrix4x4 world = align * mr.transform.localToWorldMatrix;   // not on a bone: it stands where it stands in the bind pose
                        go.transform.position = world.GetColumn(3);
                        go.transform.rotation = world.rotation;
                        go.transform.localScale = Vector3.one * (mr.transform.lossyScale.x * toTarget);
                    }
                    go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var rmats = mr.sharedMaterials;
                    if (materialOverrides != null) for (int mi = 0; mi < rmats.Length; mi++) if (rmats[mi] != null && materialOverrides.TryGetValue(rmats[mi].name, out var om2) && om2 != null) rmats[mi] = om2;
                    go.AddComponent<MeshRenderer>().sharedMaterials = rmats;
                    rigid++;
                }
                if (rigid > 0) log.AppendLine("rigid accessories attached to bones: " + rigid);

                string prefabPath = OutputDir + "/Skin_" + id + ".prefab";
                mil.name = "Skin_" + id;
                PrefabUtility.SaveAsPrefabAsset(mil, prefabPath);
                log.AppendLine("saved " + prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(mil);
                Object.DestroyImmediate(src);
                AssetDatabase.SaveAssets();
            }
            return log.ToString();
        }

        private static string Sanitize(string s) { return Regex.Replace(s, @"[^\w\-\.]", "_"); }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
