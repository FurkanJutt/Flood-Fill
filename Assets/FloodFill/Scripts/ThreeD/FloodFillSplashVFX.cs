using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloodFill.ThreeD
{
    /// <summary>Bounded, visual-only liquid meshes driven by the existing voxel tween callbacks.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FloodFillSplashVFX : MonoBehaviour
    {
        [Header("Global (sizes and speeds are in voxel widths)")]
        public bool enableSplash = true;
        [Range(0, 1)] public float splashIntensity = .25f;
        [Range(1, 512)] public int maximumActiveParticles = 80;
        [Range(1, 16)] public int maximumBurstsPerWaveStep = 4;
        [Range(1, 256)] public int maximumBurstsPerWave = 256;
        [Range(0, 1)] public float wavefrontSamplingDensity = .6f;
        [Min(0)] public float minimumSpawnSpacing = .65f;
        [Min(0)] public float surfaceOffset = .12f;
        public Color splashColorMultiplier = Color.white;
        [Range(.5f, 1.2f)] public float colorBrightness = 1;
        [Range(0, .15f)] public float glowIntensity = .02f;
        public int randomSeed = 9437;
        public bool debugVisualization;
        [Header("Main Splash")]
        public Vector2 mainSplashSize = new Vector2(.45f, .70f);
        public Vector2 mainSplashLifetime = new Vector2(.32f, .45f);
        [Range(0, 1)] public float mainSplashOpacity = .8f;
        [Range(0, 1)] public float spawnProbability = .7f;
        [Min(0)] public float mainOutwardSpeed = .2f;
        [Min(0)] public float splashLift = .65f;
        [Header("Droplets")]
        public Vector2Int dropletCount = new Vector2Int(3, 6);
        public Vector2 dropletSize = new Vector2(.055f, .11f);
        public Vector2 dropletLifetime = new Vector2(.45f, .70f);
        public Vector2 dropletSpeed = new Vector2(.9f, 1.8f);
        [Range(0, 70)] public float dropletSpread = 40;
        [Range(0, 1)] public float dropletOpacity = .85f;
        [Header("Thin Curved Streaks")]
        public Vector2Int streakCount = new Vector2Int(1, 2);
        public Vector2 streakLength = new Vector2(.4f, .7f);
        [Min(.001f)] public float streakWidth = .035f;
        public Vector2 streakLifetime = new Vector2(.22f, .34f);
        [Range(0, 1)] public float streakProbability = .4f;
        [Header("References")]
        public VoxelBoardManager3D board;
        public Camera viewCamera;

        private const int PendingCapacity = 256, StepCapacity = 256;
        private static readonly Vector3Int[] Faces = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0,0,1), new Vector3Int(0,0,-1) };
        private struct Pending
        {
            public VoxelCell3D cell;
            public Vector3 direction;
            public Color color;
            public bool recolor;
        }
        private struct Particle
        {
            public Vector3 position, velocity, normal, forward;
            public Color color;
            public float age, life, length, width, kind;
        }
        private readonly Dictionary<VoxelCell3D, Pending> pending = new Dictionary<VoxelCell3D, Pending>(PendingCapacity);
        private readonly int[] stepCounts = new int[StepCapacity];
        private readonly Vector3[] stepPositions = new Vector3[StepCapacity * 16];
        private readonly List<Vector3> vertices = new List<Vector3>(2048);
        private readonly List<Vector2> uvs = new List<Vector2>(2048);
        private readonly List<Vector2> kinds = new List<Vector2>(2048);
        private readonly List<Color> colors = new List<Color>(2048);
        private readonly List<int> indices = new List<int>(3072);
        private Particle[] particles;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private System.Random random;
        private VoxelBoardManager3D subscribedBoard;
        private int scheduledBursts;
        private bool previewing;
        public int ActiveParticleCount { get; private set; }
        public int PendingBurstCount => pending.Count;
        public int EmittedBurstCount { get; private set; }
        public Color LastBurstColor { get; private set; }
        public Vector3 LastBurstPosition { get; private set; }
        public Vector3 LastBurstNormal { get; private set; }

        private void Awake()
        {
            Initialize();
        }
        private void Initialize()
        {
            if(mesh==null){mesh = new Mesh { name = "Flood Splash Batched Quads" }; mesh.MarkDynamic();}
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off; meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off; meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            ReloadVFXSettings();
        }
        private void OnEnable()
        {
            if(Application.isPlaying&&(mesh==null||meshRenderer==null||particles==null||random==null))Initialize();
            if (board == null) board = GetComponentInParent<VoxelBoardManager3D>();
            if (viewCamera == null) viewCamera = Camera.main;
            subscribedBoard = board;
            if (subscribedBoard == null) return;
            subscribedBoard.ColorWaveBeginning += BeginWave;
            subscribedBoard.BoardVisualsCleared += StopAllSplashEffects;
            subscribedBoard.WavefrontScheduled += Schedule;
        }
        private void OnDisable()
        {
            if (subscribedBoard != null)
            {
                subscribedBoard.ColorWaveBeginning -= BeginWave;
                subscribedBoard.BoardVisualsCleared -= StopAllSplashEffects;
                subscribedBoard.WavefrontScheduled -= Schedule;
            }
            subscribedBoard = null; StopAllSplashEffects();
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
        private float Range(float low, float high) => Mathf.Lerp(low, Mathf.Max(low, high), (float)random.NextDouble());
        private float Range(Vector2 range) => Range(Mathf.Max(.001f, range.x), range.y);
        private int Count(Vector2Int range) => random.Next(Mathf.Clamp(range.x, 0, 12), Mathf.Clamp(Mathf.Max(range.x, range.y), 0, 12) + 1);
        private void BeginWave() { StopAllSplashEffects(); EmittedBurstCount = 0; }
        private void Schedule(VoxelCell3D cell, Vector3 direction, int step, Color color, bool recolor)
        {
            if (!enableSplash || !isActiveAndEnabled || splashIntensity <= 0 || step < 0 || step >= StepCapacity ||
                pending.Count >= PendingCapacity || scheduledBursts >= maximumBurstsPerWave || pending.ContainsKey(cell)) return;
            int count = stepCounts[step];
            if (count >= Mathf.Clamp(maximumBurstsPerWaveStep, 1, 16)) return;
            // Intensity 0.25 is the authored baseline, not a second 75% rejection gate.
            float density=Mathf.Clamp01(wavefrontSamplingDensity*splashIntensity/.25f);
            if (density<=0 || scheduledBursts>0 && Range(0,1)>density) return;
            if (!TrySurface(cell, out _, out _)) return;
            Vector3 position = transform.InverseTransformPoint(cell.transform.position);
            float spacing = minimumSpawnSpacing * board.VoxelWidth;
            for (int i=0; i<count; i++) if ((position-stepPositions[step*16+i]).sqrMagnitude < spacing*spacing) return;
            stepPositions[step*16+count] = position; stepCounts[step]++; scheduledBursts++;
            pending.Add(cell, new Pending { cell=cell, direction=direction, color=color, recolor=recolor });
            cell.WavefrontReached += Reach;
        }
        private void Reach(VoxelCell3D cell, bool recolor)
        {
            if (!pending.TryGetValue(cell, out var candidate) || candidate.recolor != recolor) return;
            cell.WavefrontReached -= Reach; pending.Remove(cell);
            if (enableSplash && splashIntensity > 0 && TrySurface(cell, out var position, out var normal))
                Burst(position, normal, candidate.direction, candidate.color);
        }
        private bool TrySurface(VoxelCell3D cell, out Vector3 position, out Vector3 normal)
        {
            position=normal=Vector3.zero;
            if (board == null || cell == null || !cell.gameObject.activeInHierarchy) return false;
            float best=.05f; bool found=false;
            Vector3 center=transform.InverseTransformPoint(cell.transform.position);
            Vector3 eye=viewCamera!=null?transform.InverseTransformPoint(viewCamera.transform.position):center+Vector3.one*100;
            foreach (var face in Faces)
            {
                if (board.IsActiveCoordinate(cell.X+face.x,cell.Y+face.y,cell.Z+face.z)) continue;
                // A hollow full grid represents a solid cube: never emit into its unseen cavity.
                if (board.VolumeMode==VoxelBoardManager3D.VoxelVolumeMode.HollowCube &&
                    cell.X+face.x>=0 && cell.X+face.x<board.Width && cell.Y+face.y>=0 && cell.Y+face.y<board.Height && cell.Z+face.z>=0 && cell.Z+face.z<board.Depth) continue;
                Vector3 n=transform.InverseTransformDirection(board.BoardTransform.TransformDirection((Vector3)face)).normalized;
                float score=Vector3.Dot(n,(eye-center).normalized);
                if (score<=best) continue;
                best=score; normal=n;found=true;
            }
            // Resting cell width plus offset clears the existing 1.18x color-wave pop.
            position=center+normal*board.VoxelWidth*(.5f+surfaceOffset);
            return found;
        }
        private void Burst(Vector3 position, Vector3 normal, Vector3 direction, Color selected)
        {
            if (particles == null || !enableSplash) return;
            float size=board!=null?board.VoxelWidth:1;
            Vector3 forward=Vector3.ProjectOnPlane(transform.InverseTransformDirection(board!=null?board.BoardTransform.TransformDirection(direction):direction),normal).normalized;
            if (forward.sqrMagnitude<.01f) forward=Vector3.Cross(normal, Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized;
            Vector3 side=Vector3.Cross(normal,forward).normalized;
            var color=selected*splashColorMultiplier; color.r*=colorBrightness;color.g*=colorBrightness;color.b*=colorBrightness;
            LastBurstColor=selected;LastBurstPosition=position;LastBurstNormal=normal;EmittedBurstCount++;
            if (EmittedBurstCount==1 || Range(0,1)<spawnProbability)
            {
                float width=Range(mainSplashSize)*size;
                Add(position-forward*width*.15f, (normal*mainOutwardSpeed+forward*.22f)*size, normal, forward, color, mainSplashOpacity, Range(mainSplashLifetime), width, width, 0);
            }
            int drops=Count(dropletCount);
            for (int i=0;i<drops;i++)
            {
                float angle=Range(-dropletSpread,dropletSpread)*Mathf.Deg2Rad;
                Vector3 velocity=(normal*.85f+forward*Mathf.Cos(angle)*.45f+side*Mathf.Sin(angle)).normalized*Range(dropletSpeed)*size;
                float width=Range(dropletSize)*size;
                Add(position+(side*Range(-.15f,.15f)+normal*.18f+forward*.15f)*size,velocity,normal,forward,color,dropletOpacity,Range(dropletLifetime),width*1.5f,width,1);
            }
            if (Range(0,1)<streakProbability)
                for(int i=0,count=Count(streakCount);i<count;i++)
                    Add(position+side*Range(-.08f,.08f)*size,(normal*.12f+forward*.4f)*size,normal,forward,color,mainSplashOpacity,Range(streakLifetime),Range(streakLength)*size,Mathf.Max(.001f,streakWidth)*size,2);
            if (debugVisualization)
            {
                Debug.DrawRay(transform.TransformPoint(position),transform.TransformDirection(normal)*size*.4f,selected,.3f);
                Debug.DrawRay(transform.TransformPoint(position),transform.TransformDirection(forward)*size*.4f,Color.white,.3f);
            }
        }
        private void Add(Vector3 position, Vector3 velocity, Vector3 normal, Vector3 forward, Color color, float opacity, float life, float length, float width, float kind)
        {
            int slot=ActiveParticleCount;
            if(slot>=particles.Length)
            {
                if(kind==1)return;
                // Keep the fresh liquid crest at the leading edge when old droplets fill the pool.
                slot=-1;float oldest=-1;
                for(int i=0;i<ActiveParticleCount;i++)
                {
                    if(particles[i].kind!=1)continue;
                    float age=particles[i].age/particles[i].life;
                    if(age>oldest){slot=i;oldest=age;}
                }
                if(slot<0)return;
            }
            else ActiveParticleCount++;
            color.a*=opacity;
            particles[slot]=new Particle { position=position, velocity=velocity, normal=normal, forward=forward, color=color, life=life, length=length, width=width, kind=kind };
        }
        private void LateUpdate()
        {
            if (!enableSplash || splashIntensity<=0) { if(pending.Count>0||ActiveParticleCount>0||previewing)StopAllSplashEffects(); return; }
            if (previewing)
            {
                previewClock+=Time.deltaTime;
                while(previewIndex<previewCount && previewTimes[previewIndex]<=previewClock)
                { var p=previewSamples[previewIndex++];if(TrySurface(p.cell,out var point,out var normal))Burst(point,normal,p.direction,p.color); }
                previewing=previewIndex<previewCount;
            }
            for(int i=ActiveParticleCount-1;i>=0;i--)
            {
                var p=particles[i];p.age+=Time.deltaTime;
                if(p.age>=p.life) { particles[i]=particles[--ActiveParticleCount];continue; }
                // Bend gently back toward the surface without reaching it during the short lifetime.
                p.velocity-=p.normal*(board!=null?board.VoxelWidth:1)*Time.deltaTime*.5f;
                p.position+=p.velocity*Time.deltaTime;particles[i]=p;
            }
            if(ActiveParticleCount>0||meshRenderer.enabled)DrawParticles();
        }
        private void DrawParticles()
        {
            vertices.Clear();uvs.Clear();kinds.Clear();colors.Clear();indices.Clear();
            for(int i=0;i<ActiveParticleCount;i++)
            {
                var p=particles[i];float progress=p.age/p.life;
                float scale=Mathf.Min(1,progress*8)*Mathf.Sqrt(1-progress);
                Vector3 forward=p.forward,side=Vector3.Cross(p.normal,forward).normalized;
                if(p.kind==1 && viewCamera!=null)
                { forward=transform.InverseTransformDirection(viewCamera.transform.up);side=transform.InverseTransformDirection(viewCamera.transform.right); }
                Color tint=p.color;tint.a*=1-progress*progress;tint.r*=1+glowIntensity;tint.g*=1+glowIntensity;tint.b*=1+glowIntensity;
                if(p.kind!=1)
                {
                    DrawFluidRibbon(p,forward,side,scale,progress,tint);
                    continue;
                }
                Vector3 along=forward*p.length*scale*.5f,across=side*p.width*scale*.5f;
                int start=vertices.Count;
                AddVertex(p.position-along-across,new Vector2(0,0),p.kind,tint);
                AddVertex(p.position+along-across,new Vector2(1,0),p.kind,tint);
                AddVertex(p.position+along+across,new Vector2(1,1),p.kind,tint);
                AddVertex(p.position-along+across,new Vector2(0,1),p.kind,tint);
                AddQuad(start,start+1,start+2,start+3);
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetUVs(1,kinds);mesh.SetColors(colors);mesh.SetTriangles(indices,0,true);
            meshRenderer.enabled=ActiveParticleCount>0;
        }
        private void AddVertex(Vector3 position,Vector2 uv,float kind,Color tint)
        {vertices.Add(position);uvs.Add(uv);kinds.Add(new Vector2(kind,0));colors.Add(tint);}
        private void AddQuad(int a,int b,int c,int d)
        {indices.Add(a);indices.Add(b);indices.Add(c);indices.Add(a);indices.Add(c);indices.Add(d);}
        private void DrawFluidRibbon(Particle p,Vector3 forward,Vector3 side,float scale,float progress,Color tint)
        {
            // A real raised, curled surface. A flat decal in the new cube color disappears against that cube.
            const int rows=6;
            int columns=p.kind==0?6:2,start=vertices.Count;
            float lift=p.kind==0?splashLift:.40f;
            float curl=Mathf.Sin(Mathf.Min(1,progress*1.3f)*Mathf.PI)*.35f+.65f;
            for(int row=0;row<rows;row++)
            {
                float u=row/(float)(rows-1);
                for(int column=0;column<columns;column++)
                {
                    float v=column/(float)(columns-1),across=v*2-1;
                    float spread=p.kind==0?(.25f+.75f*u):1;
                    float fingers=p.kind==0?(.8f+.2f*Mathf.Cos(across*8+progress*3)):1;
                    float rise=Mathf.Sin(u*Mathf.PI*.65f)*lift*fingers*curl;
                    float lean=u-u*u*.28f;
                    Vector3 position=p.position+forward*(lean*p.length*scale)+side*(across*p.width*.5f*spread*scale)+p.normal*(rise*p.length*scale);
                    AddVertex(position,new Vector2(u,v),p.kind,tint);
                    if(row>0&&column>0)
                    {
                        int current=start+row*columns+column;
                        AddQuad(current-columns-1,current-columns,current,current-1);
                    }
                }
            }
        }
        [Button("Stop All Splash Effects")]
        public void StopAllSplashEffects()
        {
            foreach(var entry in pending) if(entry.Key!=null)entry.Key.WavefrontReached-=Reach;
            pending.Clear();System.Array.Clear(stepCounts,0,stepCounts.Length);scheduledBursts=0;
            ActiveParticleCount=0;previewing=false;
            if(mesh!=null)mesh.Clear();if(meshRenderer!=null)meshRenderer.enabled=false;
        }
        [Button("Reload VFX Settings")]
        public void ReloadVFXSettings()
        {
            if(!Application.isPlaying)return;
            StopAllSplashEffects();maximumActiveParticles=Mathf.Clamp(maximumActiveParticles,1,512);
            particles=new Particle[maximumActiveParticles];random=new System.Random(randomSeed);
            // Worst case: every slot contains a 6x6 fluid fan, without growing buffers mid-move.
            int vertexCapacity=maximumActiveParticles*36,indexCapacity=maximumActiveParticles*150;
            vertices.Capacity=Mathf.Max(vertices.Capacity,vertexCapacity);uvs.Capacity=Mathf.Max(uvs.Capacity,vertexCapacity);
            kinds.Capacity=Mathf.Max(kinds.Capacity,vertexCapacity);colors.Capacity=Mathf.Max(colors.Capacity,vertexCapacity);
            indices.Capacity=Mathf.Max(indices.Capacity,indexCapacity);
        }
        private Color PreviewColor()
        {
            var game=FindAnyObjectByType<FloodFillGameManager3D>();
            return board.GetPaletteColor(game!=null&&game.SelectedColorIndex>=0?game.SelectedColorIndex:board.CurrentPlayerColor);
        }
        [Button("Preview Splash")]
        public void PreviewSplash()
        {
            if(!Application.isPlaying||board==null||!board.HasActiveBoard)return;
            StopAllSplashEffects();
            for(int x=0;x<board.Width;x++) for(int y=board.Height-1;y>=0;y--) for(int z=0;z<board.Depth;z++)
            {var cell=board.GetCell(x,y,z);if(TrySurface(cell,out var point,out var normal)){Burst(point,normal,Vector3.right,PreviewColor());return;}}
        }
        private readonly Pending[] previewSamples=new Pending[128];
        private readonly float[] previewTimes=new float[128];
        private int previewCount,previewIndex;
        private float previewClock;
        [Button("Preview Full Wave")]
        public void PreviewFullWave()
        {
            if(!Application.isPlaying||board==null||!board.HasActiveBoard)return;
            StopAllSplashEffects();previewCount=previewIndex=0;previewClock=0;
            // Visual-only BFS over occupied cells. No colors, captures, moves or game state are touched.
            var queue=new Queue<VoxelCell3D>();var depths=new Dictionary<VoxelCell3D,int>();
            var origin=board.StartingVoxel;queue.Enqueue(origin);depths.Add(origin,0);Color color=PreviewColor();
            while(queue.Count>0&&previewCount<Mathf.Min(previewSamples.Length,maximumBurstsPerWave))
            {
                var cell=queue.Dequeue();int step=depths[cell];
                if(step<StepCapacity&&stepCounts[step]<maximumBurstsPerWaveStep&&TrySurface(cell,out _,out _)&&Range(0,1)<wavefrontSamplingDensity)
                {previewSamples[previewCount]=new Pending {cell=cell,direction=new Vector3(cell.X-origin.X,cell.Y-origin.Y,cell.Z-origin.Z).normalized,color=color};previewTimes[previewCount++]=step*board.WaveStepDuration;stepCounts[step]++;}
                foreach(var face in Faces)
                {var next=board.GetCell(cell.X+face.x,cell.Y+face.y,cell.Z+face.z);if(next!=null&&!depths.ContainsKey(next)){depths.Add(next,step+1);queue.Enqueue(next);}}
            }
            previewing=previewCount>0;
        }
    }
}
