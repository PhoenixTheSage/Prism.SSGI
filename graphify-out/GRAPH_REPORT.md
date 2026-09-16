# Graph Report - Prism.SSGI  (2026-09-16)

## Corpus Check
- 31 files · ~9,402 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 383 nodes · 655 edges · 16 communities
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 30 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `8038b97a`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- SSGIPass
- FileShaderCompiler
- Trace.hlsl
- SSGIConfig
- AnomalyHook
- StatusScreen
- GuiScreenSSGIConfig
- HLSL Skill
- VerticalAlignment
- ClientPlugin.Config
- BufferMapping
- Plugin
- AnomalyTerminalHook
- RenderTraceBind
- SSGIStatus
- ClientPlugin

## God Nodes (most connected - your core abstractions)
1. `SSGIPass` - 40 edges
2. `AnomalyHook` - 26 edges
3. `SSGIConfig` - 26 edges
4. `GuiScreenSSGIConfig` - 17 edges
5. `UniformGrid` - 16 edges
6. `Plugin` - 16 edges
7. `FileShaderCompiler` - 14 edges
8. `StatusScreen` - 11 edges
9. `AnomalyTerminalHook` - 11 edges
10. `BufferMapping` - 10 edges

## Surprising Connections (you probably didn't know these)
- `Rich HUD Deferred Config Save` --semantically_similar_to--> `Deferred Config Flush`  [INFERRED] [semantically similar]
  AGENTS.md → README.md
- `HLSL Skill` --semantically_similar_to--> `HLSL Skill`  [INFERRED] [semantically similar]
  .agents/skills/a5c-ai-babysitter-hlsl/README.md → .cursor/skills/a5c-ai-babysitter-hlsl/README.md
- `HLSL Skill` --semantically_similar_to--> `HLSL Skill`  [INFERRED] [semantically similar]
  .agents/skills/a5c-ai-babysitter-hlsl/SKILL.md → .cursor/skills/a5c-ai-babysitter-hlsl/SKILL.md
- `Anomaly Owns Shared Gaps` --conceptually_related_to--> `Anomaly Shader Pack`  [INFERRED]
  AGENTS.md → README.md
- `Space Engineers Plugin Developer` --references--> `Prism.SSGI`  [EXTRACTED]
  AGENTS.md → README.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Anomaly Catalog Gating** — readme_scheduler_done, readme_velocity, readme_lineardepth, readme_hiz, readme_litmips [EXTRACTED 1.00]
- **SSGI AfterLighting Pipeline** — readme_afterlighting, readme_beforefullscreen, readme_trace, readme_svgf, readme_lbuffer [EXTRACTED 1.00]
- **Mirrored HLSL Skill Installs** — agents_skills_a5c_ai_babysitter_hlsl_readme_hlsl_skill, agents_skills_a5c_ai_babysitter_hlsl_skill_hlsl, cursor_skills_a5c_ai_babysitter_hlsl_readme_hlsl_skill, cursor_skills_a5c_ai_babysitter_hlsl_skill_hlsl [INFERRED 0.95]

## Communities (16 total, 0 thin omitted)

### Community 0 - "SSGIPass"
Cohesion: 0.09
Nodes (22): MyRenderContext, DenoiserCb, SSGIPass, AfterFrames, BeforeFrames, DenoiserCompileError, DenoiserError, LastSkip (+14 more)

### Community 1 - "FileShaderCompiler"
Cohesion: 0.09
Nodes (18): FileIncludeHandler, Shadow, FileShaderCompiler, ComputeShader, Device, IDisposable, PixelShader, VertexShader (+10 more)

### Community 2 - "Trace.hlsl"
Cohesion: 0.08
Nodes (34): Anomaly Owns Shared Gaps, CometWorks Skills, Extensibility Slice AI, Framework Gaps, IsolatedMix Energy, Pack Lighting, March LOD, Rich HUD Deferred Config Save (+26 more)

### Community 3 - "SSGIConfig"
Cohesion: 0.08
Nodes (21): QualityPreset, SSGIConfig, DenoiserBlurIterations, DenoiserBlurRadius, DenoiserMaxHistory, Enabled, ExpFactor, GIIntensity (+13 more)

### Community 4 - "AnomalyHook"
Cohesion: 0.15
Nodes (8): AnomalyHook, PackRegistered, RegistryFound, Action, IntPtr, ISrvBindable, MethodInfo, Type

### Community 5 - "StatusScreen"
Cohesion: 0.44
Nodes (3): StatusScreen, IEnumerable, StringBuilder

### Community 6 - "GuiScreenSSGIConfig"
Cohesion: 0.08
Nodes (34): HorizontalAlignment, Center, Left, Right, Item, Column, HorizontalAlignment, Row (+26 more)

### Community 7 - "HLSL Skill"
Cohesion: 0.08
Nodes (28): Compute, DirectX Shaders, GLSL, High-Level Shading Language, HLSL Skill, Shader Optimization, Unreal/Unity Shader Authoring, Compute Shaders (+20 more)

### Community 8 - "VerticalAlignment"
Cohesion: 0.25
Nodes (5): VerticalAlignment, Bottom, Center, Top, ClientPlugin.Gui.Controls

### Community 9 - "ClientPlugin.Config"
Cohesion: 0.07
Nodes (22): Attribute, ConfigPropertyAttribute, Enabled, Name, ToolTip, FloatConfigPropertyAttribute, DefaultValue, Max (+14 more)

### Community 10 - "BufferMapping"
Cohesion: 0.12
Nodes (12): Buffer, BufferMapping, IntPtr, Extensions, MyRenderContext, Random, ClientPlugin.Common, DeviceContext (+4 more)

### Community 11 - "Plugin"
Cohesion: 0.21
Nodes (7): Plugin, AnomalyPackRoot, ShaderDirectory, SSGIConfig, SSGIConfig, IPlugin, IReadOnlyDictionary

### Community 12 - "AnomalyTerminalHook"
Cohesion: 0.32
Nodes (4): AnomalyTerminalHook, MethodInfo, Type, ParameterInfo

### Community 13 - "RenderTraceBind"
Cohesion: 0.44
Nodes (3): RenderTraceBind, MethodInfo, Exception

### Community 14 - "SSGIStatus"
Cohesion: 0.33
Nodes (3): SSGIStatus, CurrentText, StringBuilder

### Community 15 - "ClientPlugin"
Cohesion: 0.29
Nodes (5): ClientPlugin, net10.0, net48, Krafs.Publicizer, Microsoft.NET.Sdk

## Knowledge Gaps
- **90 isolated node(s):** `RegistryFound`, `PackRegistered`, `net48`, `net10.0`, `Krafs.Publicizer` (+85 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 137 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `SSGIPass` connect `SSGIPass` to `ClientPlugin.Config`, `AnomalyHook`, `RenderTraceBind`, `FileShaderCompiler`?**
  _High betweenness centrality (0.190) - this node is a cross-community bridge._
- **Why does `ClientPlugin.Config` connect `ClientPlugin.Config` to `SSGIConfig`?**
  _High betweenness centrality (0.147) - this node is a cross-community bridge._
- **Why does `GuiScreenSSGIConfig` connect `GuiScreenSSGIConfig` to `Plugin`, `ClientPlugin.Config`, `SSGIConfig`?**
  _High betweenness centrality (0.136) - this node is a cross-community bridge._
- **What connects `RegistryFound`, `PackRegistered`, `net48` to the rest of the system?**
  _90 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SSGIPass` be split into smaller, more focused modules?**
  _Cohesion score 0.09408033826638477 - nodes in this community are weakly interconnected._
- **Should `FileShaderCompiler` be split into smaller, more focused modules?**
  _Cohesion score 0.09047619047619047 - nodes in this community are weakly interconnected._
- **Should `Trace.hlsl` be split into smaller, more focused modules?**
  _Cohesion score 0.0766488413547237 - nodes in this community are weakly interconnected._