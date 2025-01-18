<div align="center">

# Ceres
Powerful visual scripting toolkit for Unity.

![banner](./Docs/Images/ceres_banner.png)

<b>*Still in earlier development and may have frequent API changes, 
do not use it in any production environment*</b>

</div>

## Dependencies

Add following dependencies to `manifest.json`.

```json
  "dependencies": {
    "com.kurisu.chris": "https://github.com/AkiKurisu/Chris.git",
    "com.cysharp.unitask":"2.5.3",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }

```

## HighLights

- Generic and delegate support
- Graph and C# Integration
- Editor debugging
- Easy implementation

## Contents

[Concept of Ceres](./Docs/ceres_concept.md)
> Introducing the core concepts of Ceres.

[Code Generation in Ceres](./Docs/ceres_codegen.md)
> Introducing the code generation inside Ceres.

## Platform

Unity 2022.3 LTS or later.

Support Mono, IL2CPP.

## Implementation

### Ceres.Flow

Powerful visual scripting solution inspired from Unreal's Blueprint.
  
Included in this repository. 

See [Startup Flow](./Docs/flow_startup.md).

![ceres_flow](./Docs/Images/ceres_flow.png)
 
### Next Gen Dialogue

AI powered dialogue visual designer for Unity.

Is migrating to Ceres version, see [Next-Gen-Dialogue/ceres_main](https://github.com/AkiKurisu/Next-Gen-Dialogue/tree/ceres_main).

![ceres_ngd](./Docs/Images/ceres_ngd.png)

## Reference


[Chris](https://github.com/AkiKurisu/Chris) 

>Support Ceres to serialize any object and edit them in editor, 
also providing contextual event used in Flow.

[UniTask](https://github.com/Cysharp/UniTask) 

>Support Ceres to execute node in async.
