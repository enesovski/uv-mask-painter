#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering; 

namespace Artifika.MaskPainter
{
    [EditorTool("Mask Painter Tool")]
    public sealed class MaskPainterTool : EditorTool
    {
        private MaskPaintable currentPaintable;
        private MaskAsset currentMask;
        private bool isPainting = false;
        private Vector2 lastMousePosition;
        
        private float brushRadius = 3f;
        private float brushStrength = 0.5f;
        private float brushHardness = 0.5f;
        private Color brushColor = Color.blue;
        private int selectedChannel = 0;
        private int selectedTool = 0;

        private Color32[] pixelBuffer;           
        private RectInt dirtyRect;             
        private bool hasDirty;
        private double nextUploadTime;

        private const double UPDATE_INTERVAL = 1.0 / 30.0; // 30 Hz

        public static MaskPainterTool Active { get; private set; }

        public override void OnActivated()
        {
            Active = this;
            SceneView.RepaintAll();
        }

        public override void OnWillBeDeactivated()
        {
            if (Active == this) Active = null;
        }
    
        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView)) return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            HandleInput(sceneView);
            DrawBrushPreview(sceneView);
        }

        public void OnEnable()
        {
            SceneView.RepaintAll();
        }

        public void OnDisable()
        {
            currentPaintable = null;
            currentMask = null;
            isPainting = false;
        }

        private void HandleInput(SceneView sceneView)
        {
            Event e = Event.current;

            if (e.type == EventType.ScrollWheel)
            {
                if (e.control)
                {
                    AdjustRadius(-e.delta.y * 0.1f); 
                    e.Use();
                }
                else if (e.shift)
                {
                    AdjustStrength(-e.delta.y * 0.05f);
                    e.Use();
                }
            }

            if (isAdjustDragging && e.type == EventType.MouseDrag)
            {
                Vector2 d = e.delta;
                dragAccum += d;

                float sizeSensitivity = 0.01f;   
                float hardSensitivity = 0.0025f; 

                brushRadius = Mathf.Clamp(startRadius + dragAccum.x * sizeSensitivity, 0.01f, 100f);
                brushHardness = Mathf.Clamp01(startHardness - dragAccum.y * hardSensitivity);

                sceneView.Repaint();
                e.Use();
            }

            if (!TryGetPaintableUnderMouse(e.mousePosition, out currentPaintable, out currentMask))
            {
                if (currentPaintable != null)
                {
                    currentPaintable = null;
                    currentMask = null;
                }
                return;
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        EnsureBuffer();               
                        isPainting = true;
                        lastMousePosition = e.mousePosition;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (e.button == 0)
                    {
                        isPainting = false;
                        FlushDirty(true);             
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (isPainting && e.button == 0)
                    {
                        Vector2 currentPos = e.mousePosition;
                        float distance = Vector2.Distance(currentPos, lastMousePosition);

                        int steps = 1;
                        var col = currentPaintable != null ? currentPaintable.GetCollider() : null;
                        var mc = col as MeshCollider;
                        if (mc != null)
                        {
                            Ray rayA = HandleUtility.GUIPointToWorldRay(lastMousePosition);
                            Ray rayB = HandleUtility.GUIPointToWorldRay(currentPos);
                            RaycastHit hitA, hitB;
                            if (mc.Raycast(rayA, out hitA, Mathf.Infinity) && mc.Raycast(rayB, out hitB, Mathf.Infinity))
                            {
                                float worldDist = Vector3.Distance(hitA.point, hitB.point);
                                float spacing = Mathf.Max(brushRadius * 0.5f, 1e-4f); 
                                steps = Mathf.Max(1, Mathf.CeilToInt(worldDist / spacing));
                            }
                            else
                            {
                                steps = Mathf.Max(1, Mathf.RoundToInt(distance / 5f)); 
                            }
                        }
                        else
                        {
                            steps = Mathf.Max(1, Mathf.RoundToInt(distance / 5f)); // fallback
                        }

                        for (int i = 0; i <= steps; i++)
                        {
                            float t = (float)i / steps;
                            Vector2 interpolatedPos = Vector2.Lerp(lastMousePosition, currentPos, t);
                            PaintAtPosition(interpolatedPos);
                        }
                        
                        lastMousePosition = currentPos;
                        e.Use();

                        FlushDirty(false);            
                        e.Use();

                    }
                    break;

                case EventType.MouseMove:
                    sceneView.Repaint();
                    break;
            }

            if (e.type == EventType.KeyDown)
            {
                HandleKeyboardShortcuts(e);
            }
        }

        private bool TryGetPaintableUnderMouse(Vector2 mousePos, out MaskPaintable paintable, out MaskAsset mask)
        {
            paintable = null; mask = null;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            RaycastHit hit;

            if (currentPaintable != null)
            {
                var col = currentPaintable.GetCollider();
                var mc = col as MeshCollider;
                if (mc != null && mc.Raycast(ray, out hit, Mathf.Infinity))
                {
                    paintable = currentPaintable;
                    mask = currentPaintable.mask;
                    return mask != null;
                }
            }

            if (Physics.Raycast(ray, out hit))
            {
                var candidate = hit.collider.GetComponent<MaskPaintable>();
                var mc = hit.collider as MeshCollider;
                if (candidate != null && mc != null && candidate.mask != null)
                {
                    paintable = candidate;
                    mask = candidate.mask;
                    return true;
                }
            }
            return false;
        }


        private void PaintAtPosition(Vector2 mousePosition)
        {
            if (currentPaintable == null || currentMask == null) return;

            var col = currentPaintable.GetCollider();
            var mc = col as MeshCollider;
            if (mc == null) return; 

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            RaycastHit hit;
            if (!mc.Raycast(ray, out hit, Mathf.Infinity)) return;

            if (selectedTool == 3)
                PerformFillOperation(hit.textureCoord);
            else
                PerformPaintOperation(hit);
        }

        private static byte LerpByte(byte a, byte b, float t)
        {
            return (byte)(a + (b - a) * t);
        }

        private void PerformPaintOperation(RaycastHit hit)
        {
            if (currentMask?.Texture == null) return;
            EnsureBuffer();

            Vector2 uv = hit.textureCoord;

            int cx = Mathf.Clamp(Mathf.FloorToInt(uv.x * currentMask.width), 0, currentMask.width - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt(uv.y * currentMask.height), 0, currentMask.height - 1);

            float R = Mathf.Max(0.0001f, brushRadius);

            if (!TryComputeSurfaceFrame(hit, out var tu, out var tv))
            {
                int r = Mathf.Clamp(Mathf.RoundToInt(R * 100f), 1, 1024);
                int fallbackMinX = Mathf.Max(0, cx - r);
                int fallbackMaxX = Mathf.Min(currentMask.width - 1, cx + r);
                int fallbackMinY = Mathf.Max(0, cy - r);
                int fallbackMaxY = Mathf.Min(currentMask.height - 1, cy + r);
                return;
            }

            float g11 = Vector3.Dot(tu, tu);
            float g22 = Vector3.Dot(tv, tv);
            float g12 = Vector3.Dot(tu, tv);

            float R2 = R * R;

            float duMax = R / Mathf.Max(tu.magnitude, 1e-8f);
            float dvMax = R / Mathf.Max(tv.magnitude, 1e-8f);

            int tightMinX = Mathf.Max(0, Mathf.FloorToInt((uv.x - duMax) * currentMask.width));
            int tightMaxX = Mathf.Min(currentMask.width - 1, Mathf.CeilToInt((uv.x + duMax) * currentMask.width));
            int tightMinY = Mathf.Max(0, Mathf.FloorToInt((uv.y - dvMax) * currentMask.height));
            int tightMaxY = Mathf.Min(currentMask.height - 1, Mathf.CeilToInt((uv.y + dvMax) * currentMask.height));

            for (int py = tightMinY; py <= tightMaxY; py++)
            {
                float v = (py + 0.5f) / currentMask.height - uv.y;

                for (int px = tightMinX; px <= tightMaxX; px++)
                {
                    float u = (px + 0.5f) / currentMask.width - uv.x;

                    float d2 = g11 * u * u + 2f * g12 * u * v + g22 * v * v;
                    if (d2 > R2) continue;

                    float t = 1f - d2 / R2;                  // 0..1
                    if (brushHardness > 0f) t = Mathf.Pow(t, 1f + brushHardness * 2f);
                    float paint = brushStrength * t;
                    if (paint <= 0f) continue;

                    int i = py * currentMask.width + px;
                    var c = pixelBuffer[i];

                    switch (selectedTool)
                    {
                        case 0: // Paint
                            if (selectedChannel == 0) c.r = LerpByte(c.r, 255, paint);
                            else if (selectedChannel == 1) c.g = LerpByte(c.g, 255, paint);
                            else if (selectedChannel == 2) c.b = LerpByte(c.b, 255, paint);
                            else c.a = LerpByte(c.a, 255, paint);
                            break;

                        case 1: // Erase
                            if (selectedChannel == 0) c.r = LerpByte(c.r, 0, paint);
                            else if (selectedChannel == 1) c.g = LerpByte(c.g, 0, paint);
                            else if (selectedChannel == 2) c.b = LerpByte(c.b, 0, paint);
                            else c.a = LerpByte(c.a, 0, paint);
                            break;

                        case 2: //Smooth 
                            PerformSmoothOperation(px, py, ref c, selectedChannel, paint);
                            break;
                    }

                    pixelBuffer[i] = c;
                }
            }

            MarkDirty(tightMinX, tightMinY, tightMaxX, tightMaxY);
        }
        private void PerformSmoothOperation(int px, int py, ref Color32 c, int selectedChannel, float paint)
        {
            int acc = 0;
            int count = 0;

            for (int ny = py - 1; ny <= py + 1; ny++)
            {
                if (ny < 0 || ny >= currentMask.height) continue;
                for (int nx = px - 1; nx <= px + 1; nx++)
                {
                    if (nx < 0 || nx >= currentMask.width) continue;

                    int ni = ny * currentMask.width + nx;
                    var nc = pixelBuffer[ni];
                    int v = selectedChannel == 0 ? nc.r :
                            selectedChannel == 1 ? nc.g :
                            selectedChannel == 2 ? nc.b : nc.a;
                    acc += v;
                    count++;
                }
            }

            if (count == 0) return;

            byte avg = (byte)(acc / count);

            byte cur = selectedChannel == 0 ? c.r :
                       selectedChannel == 1 ? c.g :
                       selectedChannel == 2 ? c.b : c.a;

            byte smoothed = LerpByte(cur, avg, paint);

            if (selectedChannel == 0) c.r = smoothed;
            else if (selectedChannel == 1) c.g = smoothed;
            else if (selectedChannel == 2) c.b = smoothed;
            else c.a = smoothed;
        }


        private float GetChannelValue(Color color, int channel)
        {
            switch (channel)
            {
                case 0: return color.r;
                case 1: return color.g;
                case 2: return color.b;
                case 3: return color.a;
                default: return color.r;
            }
        }

        private Color SetChannelValue(Color color, int channel, float value)
        {
            Color newColor = color;
            switch (channel)
            {
                case 0: newColor.r = value; break;
                case 1: newColor.g = value; break;
                case 2: newColor.b = value; break;
                case 3: newColor.a = value; break;
            }
            return newColor;
        }

        private void PerformFillOperation(Vector2 uv)
        {
            if (currentMask?.Texture == null) return;

            int x = Mathf.RoundToInt(uv.x * currentMask.width);
            int y = Mathf.RoundToInt(uv.y * currentMask.height);
            
            x = Mathf.Clamp(x, 0, currentMask.width - 1);
            y = Mathf.Clamp(y, 0, currentMask.height - 1);

            Color[] pixels = currentMask.Texture.GetPixels();
            
            int targetIndex = y * currentMask.width + x;
            Color targetColor = pixels[targetIndex];
            float targetValue = GetChannelValue(targetColor, selectedChannel);
            
            bool[,] visited = new bool[currentMask.width, currentMask.height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(x, y));
            
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                
                if (current.x < 0 || current.x >= currentMask.width || 
                    current.y < 0 || current.y >= currentMask.height ||
                    visited[current.x, current.y])
                    continue;
                
                int index = current.y * currentMask.width + current.x;
                Color currentColor = pixels[index];
                float currentValue = GetChannelValue(currentColor, selectedChannel);
                
                if (Mathf.Abs(currentValue - targetValue) > 0.01f)
                    continue;
                
                visited[current.x, current.y] = true;
                
                Color newColor = currentColor;
                float fillValue = 1f; 
                newColor = SetChannelValue(newColor, selectedChannel, fillValue);
                pixels[index] = newColor;
                
                queue.Enqueue(new Vector2Int(current.x + 1, current.y));
                queue.Enqueue(new Vector2Int(current.x - 1, current.y));
                queue.Enqueue(new Vector2Int(current.x, current.y + 1));
                queue.Enqueue(new Vector2Int(current.x, current.y - 1));
            }
            
            currentMask.Texture.SetPixels(pixels);
            currentMask.Texture.Apply(false, false);

            pixelBuffer = currentMask.Texture.GetPixels32();
            ResetDirty();

            EditorUtility.SetDirty(currentMask);
        }
        static float ComputeLift(Vector3 worldPos)
        {
            return 0.002f * HandleUtility.GetHandleSize(worldPos);
        }


        private void DrawBrushPreview(SceneView sceneView)
        {
            if (currentPaintable == null) return;

            var e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!Physics.Raycast(ray, out var hit) ||
                hit.collider.GetComponent<MaskPaintable>() != currentPaintable)
                return;

            float R = Mathf.Max(0.0001f, brushRadius);
            Vector3 n = hit.normal;

            Color front = brushColor; front.a = 0.9f;
            Color back = brushColor; back.a = 0.20f;

            DrawDashedDisc(hit.point, n, R, kPreviewDashes, kDashFill, front, back);

            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = Color.white;
            Handles.DrawWireDisc(hit.point, n, R * (1f - brushHardness));

            Vector3 t = AnyPerp(n);
            Vector3 b = Vector3.Cross(n, t);
            float tick = Mathf.Min(R * 0.15f, kMaxTickSize);
            Handles.DrawLine(hit.point + t * R, hit.point + t * (R + tick));
            Handles.DrawLine(hit.point - t * R, hit.point - t * (R + tick));
            Handles.DrawLine(hit.point + b * R, hit.point + b * (R + tick));
            Handles.DrawLine(hit.point - b * R, hit.point - b * (R + tick));

            float lift = ComputeLift(hit.point);
            CompareFunction old = Handles.zTest;
            Handles.zTest = CompareFunction.Always;             
            Color fillCol = brushColor; fillCol.a = 0.15f;        
            Handles.color = fillCol;
            Handles.DrawSolidDisc(hit.point + n * lift, n, R);  
            Handles.zTest = old;

            Handles.BeginGUI();
            Vector2 p = HandleUtility.WorldToGUIPoint(hit.point + n * (R + tick + 0.05f));
            GUI.Label(new Rect(p.x + 8, p.y - 12, 280, 30),
                $"R {R:0.00} m   H {brushHardness:0.00}   S {brushStrength:0.00}   {GetToolSymbol()}  {ChannelLetter()}");
            Handles.EndGUI();

            Handles.Label(hit.point + n * 0.1f, GetToolSymbol());

            Handles.zTest = CompareFunction.Always; 
        }

        private bool TryComputeSurfaceFrame(RaycastHit hit, out Vector3 tu, out Vector3 tv)
        {
            tu = tv = default;

            var mc = hit.collider as MeshCollider;
            if (mc == null || mc.sharedMesh == null || hit.triangleIndex < 0) return false;

            var mesh = mc.sharedMesh;
            var tris = mesh.triangles;
            var verts = mesh.vertices;
            var uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0) return false;

            int t = hit.triangleIndex * 3;
            int i0 = tris[t + 0];
            int i1 = tris[t + 1];
            int i2 = tris[t + 2];

            var tfm = mc.transform;
            Vector3 p0 = tfm.TransformPoint(verts[i0]);
            Vector3 p1 = tfm.TransformPoint(verts[i1]);
            Vector3 p2 = tfm.TransformPoint(verts[i2]);

            Vector2 w0 = uvs[i0];
            Vector2 w1 = uvs[i1];
            Vector2 w2 = uvs[i2];

            Vector3 dp1 = p1 - p0;
            Vector3 dp2 = p2 - p0;
            Vector2 duv1 = w1 - w0;
            Vector2 duv2 = w2 - w0;

            float det = duv1.x * duv2.y - duv1.y * duv2.x;
            if (Mathf.Abs(det) < 1e-12f) return false;  

            float invDet = 1.0f / det;

            tu = (dp1 * duv2.y - dp2 * duv1.y) * invDet; // dP/du
            tv = (-dp1 * duv2.x + dp2 * duv1.x) * invDet; // dP/dv

            return true;
        }

        private string GetToolSymbol()
        {
            switch (selectedTool)
            {
                case 0: return "P"; // Paint
                case 1: return "E"; // Erase
                case 2: return "S"; // Smooth
                case 3: return "F"; // Fill
                default: return "?";
            }
        }

        private void HandleKeyboardShortcuts(Event e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Q:
                    brushRadius = Mathf.Max(0.1f, brushRadius - 0.5f);
                    e.Use();
                    break;
                    
                case KeyCode.E:
                    brushRadius = Mathf.Min(50f, brushRadius + 0.5f);
                    e.Use();
                    break;
                    
                case KeyCode.Alpha1:
                    selectedTool = 0; // Paint
                    e.Use();
                    break;
                    
                case KeyCode.Alpha2:
                    selectedTool = 1; // Erase
                    e.Use();
                    break;
                    
                case KeyCode.Alpha3:
                    selectedTool = 2; // Smooth
                    e.Use();
                    break;
                    
                case KeyCode.Alpha4:
                    selectedTool = 3; // Fill
                    e.Use();
                    break;
            }
        }

        public void SetBrushSettings(float radius, float strength, float hardness, Color color)
        {
            brushRadius = radius;
            brushStrength = strength;
            brushHardness = hardness;
            brushColor = color;
            
            SceneView.RepaintAll();
        }

        public void SetToolSettings(int tool, int channel)
        {
            selectedTool = tool;
            selectedChannel = channel;
            Debug.Log($"Tool settings updated: Tool={tool}, Channel={channel}");
        }

        public void SetCurrentPaintable(MaskPaintable paintable)
        {
            currentPaintable = paintable;
            currentMask = paintable?.mask;
        }

        private void EnsureBuffer()
        {
            if (currentMask == null || currentMask.Texture == null) return;
            if (pixelBuffer == null || pixelBuffer.Length != currentMask.width * currentMask.height)
                pixelBuffer = currentMask.Texture.GetPixels32();  
            ResetDirty();
        }

        private void ResetDirty()
        {
            dirtyRect = new RectInt(currentMask.width, currentMask.height, 0, 0); 
            hasDirty = false;
        }

        private void MarkDirty(int minX, int minY, int maxX, int maxY)
        {
            minX = Mathf.Clamp(minX, 0, currentMask.width - 1);
            maxX = Mathf.Clamp(maxX, 0, currentMask.width - 1);
            minY = Mathf.Clamp(minY, 0, currentMask.height - 1);
            maxY = Mathf.Clamp(maxY, 0, currentMask.height - 1);

            var r = new RectInt(minX, minY, (maxX - minX + 1), (maxY - minY + 1));

            if (!hasDirty)
            {
                dirtyRect = r;
                hasDirty = true;
                return;
            }

            int unionXMin = Mathf.Min(dirtyRect.x, r.x);
            int unionYMin = Mathf.Min(dirtyRect.y, r.y);
            int unionXMax = Mathf.Max(dirtyRect.x + dirtyRect.width, r.x + r.width);
            int unionYMax = Mathf.Max(dirtyRect.y + dirtyRect.height, r.y + r.height);

            dirtyRect = new RectInt(unionXMin, unionYMin, unionXMax - unionXMin, unionYMax - unionYMin);
            hasDirty = true;
        }
        private void FlushDirty(bool force = false)
        {
            if (!hasDirty) return;
            double now = EditorApplication.timeSinceStartup;
            if (!force && now < nextUploadTime) return;

            int w = dirtyRect.width;
            int h = dirtyRect.height;
            if (w <= 0 || h <= 0) return;

            var tmp = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                int src = (dirtyRect.y + row) * currentMask.width + dirtyRect.x;
                int dst = row * w;
                System.Array.Copy(pixelBuffer, src, tmp, dst, w);
            }

            currentMask.Texture.SetPixels32(dirtyRect.x, dirtyRect.y, w, h, tmp); 
            currentMask.Texture.Apply(false, false);                               
            EditorUtility.SetDirty(currentMask);

            ResetDirty();
            nextUploadTime = now + UPDATE_INTERVAL;
        }


        const int kPreviewDashes = 48;      
        const float kDashFill = 0.65f;      
        const float kMaxTickSize = 0.10f;   

        bool isAdjustDragging;
        Vector2 dragAccum;
        float startRadius, startHardness;

        char ChannelLetter() => selectedChannel == 0 ? 'R' : selectedChannel == 1 ? 'G' : selectedChannel == 2 ? 'B' : 'A';

        public float RadiusStep() => Mathf.Max(0.01f, brushRadius * 0.10f); 

        public void AdjustRadius(float d) { brushRadius = Mathf.Clamp(brushRadius + d, 0.01f, 100f); SceneView.RepaintAll(); }
        public void AdjustStrength(float d) { brushStrength = Mathf.Clamp01(brushStrength + d); SceneView.RepaintAll(); }
        public void AdjustHardness(float d) { brushHardness = Mathf.Clamp01(brushHardness + d); SceneView.RepaintAll(); }

        public void BeginAdjustDrag()
        {
            isAdjustDragging = true;
            dragAccum = Vector2.zero;
            startRadius = brushRadius;
            startHardness = brushHardness;
            EditorGUIUtility.SetWantsMouseJumping(1); 
        }
        public void EndAdjustDrag()
        {
            isAdjustDragging = false;
            EditorGUIUtility.SetWantsMouseJumping(0);
        }

        static Vector3 AnyPerp(Vector3 n)
        {
            Vector3 t = Vector3.Cross(n, Vector3.up);
            if (t.sqrMagnitude < 1e-6f) t = Vector3.Cross(n, Vector3.right);
            return t.normalized;
        }

        static void DrawDashedDisc(Vector3 center, Vector3 normal, float radius, int dashes, float dashFraction, Color front, Color back)
        {
            var old = Handles.zTest;
            var start = AnyPerp(normal);
            float step = 360f / dashes;
            float arc = step * dashFraction;

            // front (depth pass)
            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = front;
            for (int i = 0; i < dashes; i++)
            {
                var dir = Quaternion.AngleAxis(step * i, normal) * start;
                Handles.DrawWireArc(center, normal, dir, arc, radius);
            }

            Handles.zTest = CompareFunction.Greater;
            Handles.color = back;
            for (int i = 0; i < dashes; i++)
            {
                var dir = Quaternion.AngleAxis(step * i, normal) * start;
                Handles.DrawWireArc(center, normal, dir, arc, radius);
            }

            Handles.zTest = old;
        }

    }
}

#endif
